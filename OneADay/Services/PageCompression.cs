using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;

namespace OneADay.Services;

/// <summary>
/// Compresses pages before they leave the server.
/// </summary>
/// <remarks>
/// <para>The host bills for data sent, and a page is 8–12 KB uncompressed. Cloudflare
/// compresses what it hands visitors, but the bill counts what leaves this server, before
/// Cloudflare. Measured on the published build on 2026-09-25: gzip at its best setting makes
/// every page about 2.4 times smaller for under 15% more CPU, which lowers the most a flood of
/// page requests could cost by the same 2.4 times. See PRD 11.</para>
///
/// <para><b>gzip only, at its best setting.</b> Brotli came out within 1% of the size for more
/// CPU, and every client — Cloudflare included — accepts gzip. The scripts and styles aren't
/// compressed twice: MapStaticAssets serves copies compressed when the site is built, and a
/// response that already carries an encoding is left alone.</para>
///
/// <para><b>Compression can leak a secret</b> — the "BREACH" attack — when a page shows back
/// text a visitor controls alongside that secret. The page comes out a little smaller whenever
/// the visitor's text matches part of the secret, so someone who can see response sizes learns
/// it a character at a time. It takes both halves on the same page. No Stumpty page shows back
/// its address: the only two that read it, Unsubscribe and ConfirmSubscription, use the token
/// to find a subscriber and never display it. A page that ever echoes its address next to
/// anything secret must be left out of compression.</para>
/// </remarks>
public static class PageCompression
{
    public static IServiceCollection AddPageCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            // Behind Cloudflare and Fly every request is HTTPS once the proxy headers are read,
            // and compression skips HTTPS unless told otherwise. Left off, this whole feature
            // would do nothing in production while every local run looked fine. The remarks
            // above are why switching it on is safe here.
            options.EnableForHttps = true;
            options.Providers.Add<GzipCompressionProvider>();
        });

        // Measured on the home page: 4.8 KB at the best setting against 6.0 KB at the fastest,
        // for 0.03 ms more CPU on a page that takes about 0.6 ms to draw.
        services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Optimal);
        return services;
    }

    /// <summary>Compresses every response that can be, on its way out.</summary>
    /// <remarks>
    /// <b>Register this before anything that writes a page</b> — the not-found and error pages,
    /// the static files, and the pages themselves. Below them, their responses would go out
    /// uncompressed with nothing to show for it. PageCompressionTests pins the order against
    /// Program.cs.
    /// </remarks>
    public static WebApplication UsePageCompression(this WebApplication app)
    {
        app.UseResponseCompression();
        return app;
    }
}
