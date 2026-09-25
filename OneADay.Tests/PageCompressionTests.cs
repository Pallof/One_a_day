using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneADay.Services;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace OneADay.Tests;

/// <summary>
/// Pages go out compressed. Runs real requests on a loopback port, through the same
/// AddPageCompression and UsePageCompression the app uses.
///
/// <para>The stakes: the host bills for what leaves the server, and compression halves every
/// page, which halves the most a flood of page requests could cost (PRD 11).</para>
/// </summary>
public class PageCompressionTests
{
    private static readonly string Page =
        string.Concat(Enumerable.Repeat("<p class=\"oad-box\">Challenge of the day</p>\n", 200));

    private sealed record Reply(string? Encoding, byte[] Body);

    private static async Task<Reply> Send(string? acceptEncoding, bool overHttps = false)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddPageCompression();

        await using var app = builder.Build();
        if (overHttps)
        {
            // What UseProxyHeaders does behind Cloudflare and Fly: the request is HTTPS from
            // here on.
            app.Use((context, next) =>
            {
                context.Request.Scheme = "https";
                return next(context);
            });
        }
        app.UsePageCompression();
        app.Run(context =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            return context.Response.WriteAsync(Page);
        });
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None })
        {
            BaseAddress = new Uri(address),
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        if (acceptEncoding is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Encoding", acceptEncoding);
        }
        using var response = await http.SendAsync(request);
        var reply = new Reply(response.Content.Headers.ContentEncoding.SingleOrDefault(),
                              await response.Content.ReadAsByteArrayAsync());

        await app.StopAsync();
        return reply;
    }

    private static string Gunzip(byte[] body)
    {
        using var gzip = new GZipStream(new MemoryStream(body), CompressionMode.Decompress);
        using var text = new StreamReader(gzip, Encoding.UTF8);
        return text.ReadToEnd();
    }

    [Fact]
    public async Task A_page_goes_out_compressed_to_anyone_who_accepts_gzip()
    {
        // Cloudflare asks this way, as does every browser.
        var reply = await Send("gzip, deflate, br");

        Assert.Equal("gzip", reply.Encoding);
        Assert.Equal(Page, Gunzip(reply.Body));   // the same page, just smaller
        Assert.True(reply.Body.Length < Page.Length / 2);
    }

    [Fact]
    public async Task Pages_are_compressed_over_HTTPS_too()
    {
        // The trap: compression skips HTTPS unless told otherwise, and behind Cloudflare and
        // Fly every request is HTTPS. Without this, the feature would do nothing in production
        // while every plain-HTTP test above passed.
        var reply = await Send("gzip", overHttps: true);

        Assert.Equal("gzip", reply.Encoding);
    }

    [Fact]
    public async Task A_client_that_doesnt_ask_gets_the_plain_page()
    {
        var reply = await Send(acceptEncoding: null);

        Assert.Null(reply.Encoding);
        Assert.Equal(Page, Encoding.UTF8.GetString(reply.Body));
    }

    [Fact]
    public void Pages_are_compressed_at_gzips_best_setting()
    {
        // Measured on the home page: 4.8 KB at the best setting against 6.0 KB at the
        // fastest, for 0.03 ms more CPU. The savings in PRD 11 assume the best.
        var level = new ServiceCollection().AddPageCompression().BuildServiceProvider()
            .GetRequiredService<IOptions<GzipCompressionProviderOptions>>().Value.Level;

        Assert.Equal(CompressionLevel.Optimal, level);
    }

    [Fact]
    public void Compression_comes_before_anything_that_writes_a_page()
    {
        // Proven against the real Program.cs, like the lock's order check. Below the not-found
        // page or the pages themselves, those responses would go out uncompressed — the flood
        // this exists to shrink, with nothing failing to say so.
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "OneADay", "Program.cs"));
        var pipeline = Regex.Matches(program, @"^\s*app\.(?:Use|Map)\w*(?:<\w+>)?\(", RegexOptions.Multiline)
            .Select(m => m.Value.Trim())
            .ToList();

        Assert.Contains("builder.Services.AddPageCompression();", program);
        var compression = pipeline.IndexOf("app.UsePageCompression(");
        Assert.True(compression >= 0, "UsePageCompression isn't in the pipeline");
        foreach (var writer in new[] { "app.UseStatusCodePagesWithReExecute(", "app.MapStaticAssets(",
                                       "app.UseStaticFiles(", "app.MapOneClickUnsubscribe(",
                                       "app.MapRazorComponents<App>(" })
        {
            var index = pipeline.IndexOf(writer);
            Assert.True(index >= 0, $"{writer} isn't in Program.cs any more; update this list");
            Assert.True(compression < index, $"compression must come before {writer}");
        }
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
