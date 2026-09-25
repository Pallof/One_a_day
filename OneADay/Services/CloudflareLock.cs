using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace OneADay.Services;

/// <summary>
/// Settings for turning away every request that didn't come through Cloudflare.
/// </summary>
/// <remarks>
/// <para>Cloudflare's rate limit and cache only cover traffic that passes through Cloudflare.
/// The server also has an address of its own on the host — every Fly app answers at
/// <c>name.fly.dev</c> — and a request sent there skips both. That matters because the host
/// bills for data sent, and a bot picks the biggest file it can find. Measured on the
/// published build on 2026-09-24: one machine re-downloading the 196 KB Blazor script that
/// way could run up about $1,100 a month, while through Cloudflare the same file comes from
/// Cloudflare's cache and costs nothing. See PRD 11.</para>
///
/// <para><b>On unless a host switches it off</b>, so a production start without the secret
/// refuses rather than running unprotected. The author's machine has no Cloudflare in front
/// and switches it off in appsettings.Development.json.</para>
/// </remarks>
public sealed class CloudflareLockOptions
{
    public const string Section = "CloudflareLock";

    /// <summary>Configuration key holding the secret. Never in appsettings.json.</summary>
    public const string SecretKey = "CloudflareLock:Secret";

    /// <summary>The same key as an environment variable, which is how a host sets it.</summary>
    public const string EnvironmentVariable = "CloudflareLock__Secret";

    /// <summary>
    /// Shortest secret accepted — the same fence as <see cref="IpHasher.MinimumKeyLength"/>,
    /// against something short being set just to make the refusal go away.
    /// </summary>
    public const int MinimumSecretLength = 32;

    /// <summary>Whether the lock is on. Defaults to on, so a missing setting fails closed.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The value Cloudflare's header rule sets. From the environment, never a file.</summary>
    public string? Secret { get; set; }

    /// <summary>Why the app must not start, or null when it may.</summary>
    /// <remarks>
    /// Without a secret the lock can't tell Cloudflare's requests from anyone else's. Starting
    /// anyway would mean turning every visitor away or letting everyone through, and the second
    /// looks exactly like a working site — so neither is allowed.
    /// </remarks>
    public string? ReasonToRefuse()
    {
        if (!Enabled)
        {
            return null;
        }

        const string risk = "The lock turns away every request that didn't come through " +
                            "Cloudflare, and it needs the secret Cloudflare's header carries to " +
                            "tell the two apart.";

        var secret = Secret?.Trim();

        if (string.IsNullOrEmpty(secret))
        {
            return $"Refusing to start: {Section}:Enabled is on, but there's no secret. {risk} " +
                   $"Set the {EnvironmentVariable} environment variable to the same value as " +
                   $"Cloudflare's header rule, or set {Section}__Enabled to false on a host with " +
                   "no Cloudflare in front of it. See PRD 11.";
        }

        if (secret.Length < MinimumSecretLength)
        {
            return $"Refusing to start: the Cloudflare lock's secret is too short. {risk} " +
                   $"{EnvironmentVariable} must be at least {MinimumSecretLength} characters. " +
                   "See PRD 11.";
        }

        return null;
    }
}

/// <summary>Wires <see cref="CloudflareLockOptions"/> into the request pipeline.</summary>
public static class CloudflareLock
{
    /// <summary>
    /// The header Cloudflare's rule sets. The name is public — it's in this repository — and
    /// needn't be secret; the value is.
    /// </summary>
    public const string HeaderName = "X-Origin-Verify";

    /// <summary>
    /// Turns away any request that doesn't carry Cloudflare's secret: an empty 403, and
    /// nothing else in the pipeline runs.
    /// </summary>
    /// <remarks>
    /// <b>Register this first</b> — above even <see cref="ProxyHeaders.UseProxyHeaders"/>.
    /// Anything above it runs for the requests it turns away, and below the static-file
    /// middleware the site's biggest files, the ones worth attacking, would go out before the
    /// check. CloudflareLockTests pins the order against Program.cs.
    /// </remarks>
    public static WebApplication UseCloudflareLock(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<CloudflareLockOptions>>().Value;
        if (!options.Enabled)
        {
            return app;
        }

        var expected = Digest(options.Secret!.Trim());
        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(CloudflareLock));
        var traffic = app.Services.GetService<TrafficMonitor>();
        var warned = 0;

        app.Use((context, next) =>
        {
            if (CarriesSecret(context.Request.Headers, expected))
            {
                return next(context);
            }

            // Counted for the traffic alert: cheap to refuse, but a stream of these means
            // someone has found the server's own address.
            traffic?.RecordTurnedAway();

            // Once, then quiet. A stream of these is the attack being turned away, and logging
            // each one would spend the CPU the lock is there to save. The one line is still
            // worth having: if real visitors are refused too, the rule and the secret differ.
            if (Interlocked.Exchange(ref warned, 1) == 0)
            {
                logger.LogWarning(
                    "Turned away a request without Cloudflare's {Header} header — expected for " +
                    "anything that skips Cloudflare. If real visitors are turned away too, " +
                    "Cloudflare's header rule and {Variable} don't match (PRD 11). Further " +
                    "rejections aren't logged.",
                    HeaderName, CloudflareLockOptions.EnvironmentVariable);
            }

            // An empty 403 is about 100 bytes on the wire; the file asked for could be 196 KB.
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        });

        return app;
    }

    /// <summary>Whether the headers carry exactly one value, and it equals the secret.</summary>
    /// <remarks>
    /// Compared as SHA-256 digests in constant time.
    /// <see cref="CryptographicOperations.FixedTimeEquals"/> only hides timing between inputs
    /// of the same length, and digests always are.
    /// </remarks>
    private static bool CarriesSecret(IHeaderDictionary headers, byte[] expected)
    {
        var values = headers[HeaderName];
        return values.Count == 1
               && !string.IsNullOrEmpty(values[0])
               && CryptographicOperations.FixedTimeEquals(Digest(values[0]!), expected);
    }

    private static byte[] Digest(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
