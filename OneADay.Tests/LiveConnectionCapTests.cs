using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The cap on live connections per visitor address. Runs real requests on a loopback port, with
/// each request naming its visitor the way Cloudflare does, because the cap is about requests
/// held open at the same time — which no unit test can express.
///
/// <para>The stakes (security review, 2026-09-28): the machine takes 300 connections at once,
/// and Cloudflare's rate limit only slows how fast they're opened. Without this, one script
/// could hold all 300 and lock every real visitor out.</para>
/// </summary>
public class LiveConnectionCapTests
{
    // ---- which visitors share a cap ------------------------------------------------------

    [Fact]
    public void An_ipv4_address_is_its_own_key()
    {
        Assert.Equal("203.0.113.7", VisitorAddress.Key(IPAddress.Parse("203.0.113.7")));
        Assert.NotEqual(VisitorAddress.Key(IPAddress.Parse("203.0.113.7")),
                        VisitorAddress.Key(IPAddress.Parse("203.0.113.8")));
    }

    [Fact]
    public void An_ipv4_visitor_written_as_ipv6_is_the_same_visitor()
    {
        Assert.Equal("203.0.113.7", VisitorAddress.Key(IPAddress.Parse("::ffff:203.0.113.7")));
    }

    [Fact]
    public void One_ipv6_household_is_one_key()
    {
        // A home gets a /64 and its devices pick addresses from it at will. Counted address by
        // address, one script could walk past any per-address limit.
        var home = VisitorAddress.Key(IPAddress.Parse("2001:db8:1:2::1"));

        Assert.Equal(home, VisitorAddress.Key(IPAddress.Parse("2001:db8:1:2:abcd:ef01:2345:6789")));
        Assert.NotEqual(home, VisitorAddress.Key(IPAddress.Parse("2001:db8:1:3::1")));
    }

    [Fact]
    public void No_address_means_no_key()
    {
        Assert.Null(VisitorAddress.Key(null));
    }

    // ---- the cap, over real requests -------------------------------------------------------

    [Fact]
    public async Task One_address_holds_its_cap_and_the_next_connection_is_turned_away()
    {
        await using var server = await HubServer.Start(maxPerAddress: 3);

        for (var i = 0; i < 3; i++)
        {
            Assert.Null(await server.Open("203.0.113.7"));   // held open
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, await server.Open("203.0.113.7"));
    }

    [Fact]
    public async Task Another_address_is_unaffected()
    {
        await using var server = await HubServer.Start(maxPerAddress: 1);
        await server.Open("203.0.113.7");

        Assert.Equal(HttpStatusCode.TooManyRequests, await server.Open("203.0.113.7"));
        Assert.Null(await server.Open("198.51.100.42"));
    }

    [Fact]
    public async Task Closing_a_connection_makes_room_again()
    {
        await using var server = await HubServer.Start(maxPerAddress: 1);
        await server.Open("203.0.113.7");
        Assert.Equal(HttpStatusCode.TooManyRequests, await server.Open("203.0.113.7"));

        await server.CloseOldest();

        Assert.Null(await server.Open("203.0.113.7"));
    }

    [Fact]
    public async Task An_ipv6_household_shares_one_cap()
    {
        await using var server = await HubServer.Start(maxPerAddress: 1);
        await server.Open("2001:db8:1:2::1");

        Assert.Equal(HttpStatusCode.TooManyRequests, await server.Open("2001:db8:1:2::ffff"));
        Assert.Null(await server.Open("2001:db8:1:3::1"));
    }

    [Fact]
    public async Task Pages_are_never_counted_or_turned_away()
    {
        // Only the live connection is capped. A visitor at their cap can still load pages.
        await using var server = await HubServer.Start(maxPerAddress: 1);
        await server.Open("203.0.113.7");

        Assert.Equal(HttpStatusCode.OK, await server.Get("/", "203.0.113.7"));
        Assert.Equal(HttpStatusCode.OK, await server.Get("/_blazorish", "203.0.113.7"));
    }

    [Fact]
    public async Task Switched_off_nothing_is_turned_away()
    {
        await using var server = await HubServer.Start(maxPerAddress: 0);

        for (var i = 0; i < 5; i++)
        {
            Assert.Null(await server.Open("203.0.113.7"));
        }
    }

    [Fact]
    public void Ten_per_address_is_the_default()
    {
        Assert.Equal(10, new LiveConnectionOptions().MaxPerAddress);
    }

    // ---- against the real files ------------------------------------------------------------

    [Fact]
    public void The_cap_sits_after_the_proxy_headers_and_before_the_pages()
    {
        // Above UseProxyHeaders every visitor has Cloudflare's address, and the eleventh visitor
        // anywhere on the site would be turned away. Below MapRazorComponents it never runs.
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "OneADay", "Program.cs"));
        var pipeline = Regex.Matches(program, @"^\s*app\.(?:Use|Map)\w*(?:<\w+>)?\(", RegexOptions.Multiline)
            .Select(m => m.Value.Trim())
            .ToList();

        Assert.Contains("builder.Services.Configure<LiveConnectionOptions>(", program);
        var cap = pipeline.IndexOf("app.UseLiveConnectionCap(");
        Assert.True(cap >= 0, "UseLiveConnectionCap isn't in the pipeline");
        Assert.True(pipeline.IndexOf("app.UseProxyHeaders(") is var proxy && proxy >= 0 && proxy < cap,
            "the cap must come after UseProxyHeaders");
        Assert.True(cap < pipeline.IndexOf("app.MapRazorComponents<App>("),
            "the cap must come before MapRazorComponents");
    }

    [Fact]
    public void The_production_settings_cap_each_address_at_ten()
    {
        using var doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(FindRepoRoot(), "OneADay", "appsettings.json")));

        Assert.Equal(10, doc.RootElement.GetProperty(LiveConnectionOptions.Section)
            .GetProperty(nameof(LiveConnectionOptions.MaxPerAddress)).GetInt32());
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, ".gitignore")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Couldn't find the repo root.");
    }

    /// <summary>
    /// A server whose live connections stay open until the test closes them, the way a real one
    /// stays open for as long as its page does.
    /// </summary>
    private sealed class HubServer : IAsyncDisposable
    {
        private sealed class Connection
        {
            public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource Closed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task<HttpResponseMessage> Response { get; set; } = Task.FromResult(new HttpResponseMessage());
        }

        private readonly ConcurrentDictionary<string, Connection> _connections = new();
        private readonly ConcurrentQueue<Connection> _held = new();
        private WebApplication _app = null!;
        private HttpClient _http = null!;

        public static async Task<HubServer> Start(int maxPerAddress)
        {
            var server = new HubServer();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            // The test names each request's visitor, as Cloudflare does in front of the real site.
            builder.Services.Configure<ProxyOptions>(o => { o.Enabled = true; o.TrustAllProxies = true; });
            builder.Services.Configure<LiveConnectionOptions>(o => o.MaxPerAddress = maxPerAddress);

            var app = builder.Build();
            app.UseProxyHeaders();

            // Outside the cap, so "finished" means the cap has already counted the close.
            app.Use(async (context, next) =>
            {
                await next(context);
                if (server.Find(context) is { } finished)
                {
                    finished.Finished.TrySetResult();
                }
            });

            app.UseLiveConnectionCap();
            app.Run(async context =>
            {
                if (server.Find(context) is { } connection)
                {
                    connection.Entered.TrySetResult();
                    await connection.Closed.Task;   // open until the test closes it
                }
                await context.Response.WriteAsync("ok");
            });

            await app.StartAsync();
            server._app = app;
            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            server._http = new HttpClient { BaseAddress = new Uri(address) };
            return server;
        }

        private Connection? Find(HttpContext context) =>
            context.Request.Path.StartsWithSegments(LiveConnectionCap.HubPath)
            && _connections.TryGetValue(context.Request.Query["test"].ToString(), out var connection)
                ? connection
                : null;

        /// <summary>
        /// Opens a live connection from <paramref name="visitor"/>. Null while it's held open;
        /// otherwise the status it was turned away with.
        /// </summary>
        public async Task<HttpStatusCode?> Open(string visitor)
        {
            var id = Guid.NewGuid().ToString("N");
            var connection = _connections[id] = new Connection();
            var request = new HttpRequestMessage(HttpMethod.Get, $"{LiveConnectionCap.HubPath}?test={id}");
            request.Headers.Add("X-Forwarded-For", visitor);
            connection.Response = _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            var first = await Task.WhenAny(connection.Response, connection.Entered.Task,
                                           Task.Delay(TimeSpan.FromSeconds(10)));
            if (first == connection.Entered.Task)
            {
                _held.Enqueue(connection);
                return null;
            }
            if (first == connection.Response)
            {
                using var refused = await connection.Response;
                return refused.StatusCode;
            }
            throw new TimeoutException("The server neither took the connection nor turned it away.");
        }

        /// <summary>Closes the longest-held connection, and waits until the cap has counted it.</summary>
        public async Task CloseOldest()
        {
            Assert.True(_held.TryDequeue(out var connection), "no connection is open");
            connection.Closed.TrySetResult();
            using var response = await connection.Response;
            await connection.Finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        public async Task<HttpStatusCode> Get(string path, string visitor)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Forwarded-For", visitor);
            using var response = await _http.SendAsync(request);
            return response.StatusCode;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var connection in _connections.Values)
            {
                connection.Closed.TrySetResult();
            }
            await _app.StopAsync();
            await _app.DisposeAsync();
            _http.Dispose();
        }
    }
}
