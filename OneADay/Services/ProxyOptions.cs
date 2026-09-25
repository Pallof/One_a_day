using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using System.Net;

namespace OneADay.Services;

/// <summary>
/// How to recover a visitor's real address when the app runs behind a reverse proxy or CDN.
/// </summary>
/// <remarks>
/// <para>Kestrel reports whoever opened the connection to it. Behind nginx, Cloudflare,
/// Fly.io or Azure App Service that is <b>the proxy</b>, so every visitor collapses into a
/// single address. Two separate things break at once:</para>
/// <list type="number">
/// <item>The suggestion form's three-per-IP cap
/// (<see cref="SuggestionStore.MaxPerIpPerDay"/>) sees one address for everyone, so three
/// submissions from any three people close the form for <b>the entire internet</b> until
/// midnight Pacific — every day.</item>
/// <item><c>UseHttpsRedirection</c> sees the inward hop as plain HTTP and redirects to
/// HTTPS; the proxy terminates TLS and forwards plain HTTP again. The site stops loading
/// at all, with <c>ERR_TOO_MANY_REDIRECTS</c>.</item>
/// </list>
/// <para><b>Off by default.</b> There is no proxy on the author's machine, and trusting a
/// forwarded header that nothing sets would let a client name its own address. Each host
/// opts in. See PRD 06 and PRD 11.</para>
/// </remarks>
public sealed class ProxyOptions
{
    public const string Section = "Proxy";

    /// <summary>Whether the app runs behind a proxy at all.</summary>
    public bool Enabled { get; set; }

    /// <summary>Trust forwarded headers from any address.</summary>
    /// <remarks>
    /// For hosts whose proxy address isn't fixed or documented. The cost is that a client
    /// can name its own address and step past the per-IP cap — which PRD 06 already accepts
    /// as reachable ("rotates VPN exits → unlimited"), and which the honeypot and the
    /// five-second compose floor still stand in front of. Prefer <see cref="KnownProxies"/>
    /// or <see cref="KnownNetworks"/> whenever the host documents them.
    /// </remarks>
    public bool TrustAllProxies { get; set; }

    /// <summary>Exact proxy addresses to trust, e.g. <c>10.0.0.1</c>.</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>Proxy networks to trust, in CIDR form, e.g. <c>10.0.0.0/8</c>.</summary>
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>
    /// How many proxy hops to walk back through. One is right for a single proxy in front of
    /// the app; raising it lets a client forge the hops beyond the real ones.
    /// </summary>
    public int ForwardLimit { get; set; } = 1;

    /// <summary>Whether anything is actually trusted to send forwarded headers.</summary>
    public bool HasTrustedSource =>
        TrustAllProxies || KnownProxies.Length > 0 || KnownNetworks.Length > 0;

    /// <summary>Why the app must not start, or null when it may.</summary>
    /// <remarks>
    /// Switching this on without naming a trusted source is the trap worth failing on: the
    /// middleware would run, trust only loopback, silently ignore every forwarded header,
    /// and leave both failures above in place with no error to chase. A refusal at startup
    /// costs one deploy; the silent version costs a form that closes for everyone and nobody
    /// knowing why.
    /// </remarks>
    public string? ReasonToRefuse() =>
        Enabled && !HasTrustedSource
            ? $"Refusing to start: {Section}:Enabled is on, but nothing is trusted to send " +
              $"forwarded headers. Set {Section}:KnownProxies or {Section}:KnownNetworks to " +
              $"the proxy in front of this app, or {Section}:TrustAllProxies to true if the " +
              "host doesn't document a fixed address. Left as it is, the forwarded headers " +
              "would be ignored without any error: the suggestion form would close for every " +
              "visitor after three submissions a day, and HTTPS redirection would loop. " +
              "See PRD 11."
            : null;
}

/// <summary>Wires <see cref="ProxyOptions"/> into the request pipeline.</summary>
public static class ProxyHeaders
{
    /// <summary>
    /// Rewrites the connection's address and scheme from the proxy's forwarded headers.
    /// </summary>
    /// <remarks>
    /// <b>Register this early — after only <see cref="CloudflareLock.UseCloudflareLock"/> and
    /// <see cref="TrafficCount.UseTrafficCount"/>.</b>
    /// Anything that reads the scheme or the address must run after it — <c>UseHsts</c> and
    /// <c>UseHttpsRedirection</c> both read the scheme, and placing this below them leaves the
    /// redirect loop in place while the code looks right.
    /// </remarks>
    public static WebApplication UseProxyHeaders(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<ProxyOptions>>().Value;
        if (!options.Enabled)
        {
            return app;
        }

        var forwarded = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = options.ForwardLimit,
        };

        if (options.TrustAllProxies)
        {
            // Empty lists are how this middleware is told "don't check the sender".
            forwarded.KnownProxies.Clear();
            forwarded.KnownIPNetworks.Clear();
        }
        else
        {
            // Replace the defaults rather than adding to them. The middleware starts out
            // trusting loopback, so appending would leave anything on the machine itself
            // trusted no matter what is configured here — and would make the configuration
            // look authoritative when it isn't.
            forwarded.KnownProxies.Clear();
            forwarded.KnownIPNetworks.Clear();

            foreach (var proxy in options.KnownProxies)
            {
                forwarded.KnownProxies.Add(IPAddress.Parse(proxy));
            }
            foreach (var network in options.KnownNetworks)
            {
                forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        }

        app.UseForwardedHeaders(forwarded);
        return app;
    }
}
