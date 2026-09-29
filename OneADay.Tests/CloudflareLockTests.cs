using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneADay.Services;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OneADay.Tests;

/// <summary>
/// The Cloudflare-only lock: a request that didn't come through Cloudflare gets an empty 403
/// and nothing else. Runs real requests on a loopback port, like ProxyHeadersTests.
///
/// <para>The stakes: Cloudflare's rate limit and cache only cover traffic that passes through
/// it. Without the lock, anyone using the host's own address for the server could make it
/// send its biggest file nonstop — about $1,100 a month in data charges from one machine,
/// measured 2026-09-24 (PRD 11).</para>
/// </summary>
public class CloudflareLockTests
{
    private const string Secret = "a-test-secret-that-is-long-enough-0123456789";
    private const string Page = "the whole page";

    private sealed record Reply(HttpStatusCode Status, string Body);

    /// <summary>A server behind the lock, holding the test secret, on a loopback port.</summary>
    private static async Task<(WebApplication App, Uri Address)> Start(Action<CloudflareLockOptions>? configure)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.Configure<CloudflareLockOptions>(o =>
        {
            o.Enabled = true;
            o.Secret = Secret;
            configure?.Invoke(o);
        });

        var app = builder.Build();
        app.UseCloudflareLock();
        app.Run(context => context.Response.WriteAsync(Page));
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return (app, new Uri(address));
    }

    /// <summary>What a request gets back when it sends <paramref name="header"/>, or no header if null.</summary>
    private static async Task<Reply> Send(string? header, Action<CloudflareLockOptions>? configure = null)
    {
        var (app, address) = await Start(configure);
        await using var _ = app;

        using var http = new HttpClient { BaseAddress = address };
        if (header is not null)
        {
            http.DefaultRequestHeaders.TryAddWithoutValidation(CloudflareLock.HeaderName, header);
        }
        using var response = await http.GetAsync("/");
        var reply = new Reply(response.StatusCode, await response.Content.ReadAsStringAsync());

        await app.StopAsync();
        return reply;
    }

    /// <summary>
    /// The status for a request carrying these header lines exactly as written. HttpClient folds
    /// a repeated header into one line, so two separate copies take a raw request.
    /// </summary>
    private static async Task<HttpStatusCode> SendLines(params string[] headerLines)
    {
        var (app, address) = await Start(configure: null);
        await using var _ = app;

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(address.Host, address.Port);
        var stream = tcp.GetStream();
        var request = "GET / HTTP/1.1\r\nHost: localhost\r\n"
                      + string.Concat(headerLines.Select(line => line + "\r\n"))
                      + "Connection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request));
        using var reader = new StreamReader(stream, Encoding.ASCII);
        var statusLine = await reader.ReadLineAsync();   // "HTTP/1.1 403 Forbidden"

        await app.StopAsync();
        return (HttpStatusCode)int.Parse(statusLine!.Split(' ')[1]);
    }

    [Fact]
    public async Task A_request_through_Cloudflare_gets_the_page()
    {
        // The control case: without it, everything below would pass on a lock that turned
        // every visitor away.
        var reply = await Send(Secret);

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.Equal(Page, reply.Body);
    }

    [Fact]
    public async Task A_request_that_skips_Cloudflare_gets_an_empty_403()
    {
        // The whole point: nothing worth downloading. The empty body is also the proof that
        // nothing behind the lock ran — no page, no file, no not-found page.
        var reply = await Send(header: null);

        Assert.Equal(HttpStatusCode.Forbidden, reply.Status);
        Assert.Empty(reply.Body);
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("")]
    [InlineData("a-test-secret")]                                    // a prefix of the secret
    [InlineData(Secret + "x")]                                       // the secret, then more
    [InlineData("A-TEST-SECRET-THAT-IS-LONG-ENOUGH-0123456789")]     // the secret, other case
    [InlineData(Secret + ", something-else")]                        // alongside another value
    public async Task Anything_but_the_exact_secret_is_turned_away(string header)
    {
        // Each case stands for a looser comparison that would let a guesser in: a prefix,
        // a "contains", a case-insensitive match.
        var reply = await Send(header);

        Assert.Equal(HttpStatusCode.Forbidden, reply.Status);
        Assert.Empty(reply.Body);
    }

    [Fact]
    public async Task A_second_copy_of_the_header_is_turned_away()
    {
        // Two copies — the secret and anything else — mean something on the way added a
        // header instead of replacing it, as a Cloudflare rule set to "add" rather than "set
        // static" would. Refused rather than guessed at, even with the secret first.
        // (The row above sends two values on one line; nothing sent two lines until the
        // test audit of 2026-09-28.)
        var header = CloudflareLock.HeaderName;

        Assert.Equal(HttpStatusCode.Forbidden, await SendLines($"{header}: {Secret}", $"{header}: something-else"));
        Assert.Equal(HttpStatusCode.OK, await SendLines($"{header}: {Secret}"));   // the control
    }

    [Fact]
    public async Task Switched_off_every_request_gets_the_page()
    {
        // The author's machine: no Cloudflare in front, so no header ever arrives.
        var reply = await Send(header: null, o => o.Enabled = false);

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.Equal(Page, reply.Body);
    }

    // ---- the startup refusal ---------------------------------------------------

    [Fact]
    public void With_no_settings_at_all_the_lock_is_on_and_refuses_to_start()
    {
        // Fails closed: a host that forgets the whole section gets a refused start, not an
        // unlocked server that looks exactly like a working one.
        var options = new CloudflareLockOptions();

        Assert.True(options.Enabled);
        Assert.NotNull(options.ReasonToRefuse());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short-secret")]
    public void A_missing_or_short_secret_refuses_to_start(string? secret)
    {
        var reason = new CloudflareLockOptions { Secret = secret }.ReasonToRefuse();

        Assert.NotNull(reason);
        Assert.Contains(CloudflareLockOptions.EnvironmentVariable, reason);
    }

    [Fact]
    public void A_secret_of_only_spaces_refuses_however_long_it_is()
    {
        // Forty spaces would pass the length rule if nobody trimmed them — and then the lock
        // would check every request against forty spaces (test audit, 2026-09-28).
        var reason = new CloudflareLockOptions { Secret = new string(' ', 40) }.ReasonToRefuse();

        Assert.NotNull(reason);
    }

    [Fact]
    public void A_long_enough_secret_or_the_lock_switched_off_starts()
    {
        Assert.Null(new CloudflareLockOptions { Secret = Secret }.ReasonToRefuse());
        Assert.Null(new CloudflareLockOptions { Enabled = false }.ReasonToRefuse());
    }

    // ---- against the real files --------------------------------------------------

    [Fact]
    public void The_lock_is_the_first_thing_every_request_meets()
    {
        // Proven against the real Program.cs, like the .gitignore check in SubscriptionTests.
        // Placed below MapStaticAssets, the lock would let the biggest files through — the
        // exact path it exists to close — while every test above still passed.
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "OneADay", "Program.cs"));
        var pipeline = Regex.Matches(program, @"^\s*app\.(?:Use|Map)\w*\(", RegexOptions.Multiline)
            .Select(m => m.Value.Trim())
            .ToList();

        Assert.NotEmpty(pipeline);
        Assert.Equal("app.UseCloudflareLock(", pipeline[0]);
    }

    [Fact]
    public void Production_settings_lock_the_server_and_hold_no_secret()
    {
        // appsettings.json is what a host starts from, so the lock is on there; the author's
        // machine switches it off. The secret is in neither — it belongs to the environment,
        // like every other secret here.
        var main = Settings("appsettings.json");
        var development = Settings("appsettings.Development.json");

        Assert.True(main.GetProperty("Enabled").GetBoolean());
        Assert.False(development.GetProperty("Enabled").GetBoolean());
        Assert.False(main.TryGetProperty("Secret", out _));
        Assert.False(development.TryGetProperty("Secret", out _));
    }

    private static JsonElement Settings(string file)
    {
        using var doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(FindRepoRoot(), "OneADay", file)));
        return doc.RootElement.GetProperty(CloudflareLockOptions.Section).Clone();
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
}
