using System.Security.Cryptography;
using System.Text;

namespace OneADay.Services;

/// <summary>
/// Turns a visitor's IP address into a value the suggestion form can count without the
/// stored file becoming a list of IP addresses.
/// </summary>
/// <remarks>
/// <para>The cap in <see cref="SuggestionStore"/> needs to recognise a repeat address, which
/// takes a <i>deterministic</i> function — the same address must always give the same value.
/// The obvious choice, a plain hash, doesn't work here: IPv4 is only 2³² addresses, and a
/// graphics card walks that entire space in minutes. PRD 06 records one of these hashes
/// being reversed <b>by hand, in six guesses</b>. A plain hash of an IP address is not
/// anonymisation; it's the address with a hat on.</para>
///
/// <para>HMAC with a server-side key keeps the determinism — so the cap behaves exactly as
/// before — while making the value useless to anyone who doesn't hold the key. Inverting it
/// now means guessing 256 bits rather than walking four billion.</para>
///
/// <para>This matters more once forwarded headers are on (<see cref="ProxyOptions"/>). Behind
/// a proxy without them the stored value is a hash of <i>the proxy's</i> address, which tells
/// an attacker nothing. The moment real visitor addresses arrive, the weak hash starts
/// recording real people.</para>
/// </remarks>
public sealed class IpHasher
{
    /// <summary>Configuration key holding the secret. Never in appsettings.json.</summary>
    public const string KeyName = "Privacy:IpHashKey";

    /// <summary>The same key as an environment variable, which is how a host sets it.</summary>
    public const string EnvironmentVariable = "Privacy__IpHashKey";

    /// <summary>
    /// Shortest key accepted. Not a cryptographic threshold — a fence against "x" being set
    /// to make a refusal go away, which would leave the hash as good as unkeyed.
    /// </summary>
    public const int MinimumKeyLength = 32;

    private readonly byte[] _key;

    private IpHasher(byte[] key) => _key = key;

    /// <summary>Why the app must not start, or null when it may.</summary>
    /// <remarks>
    /// Development is exempt so a fresh clone runs with no setup; it gets a random key per
    /// start instead. Everywhere else this refuses rather than quietly falling back to an
    /// unkeyed hash — a fallback would leave the app looking protected while storing
    /// recoverable addresses, which is the failure mode this codebase keeps designing out
    /// (see <see cref="DevelopmentModeGuard"/> and <see cref="TeaserStore"/>).
    /// </remarks>
    public static string? ReasonToRefuse(IHostEnvironment env, IConfiguration config)
    {
        if (env.IsDevelopment())
        {
            return null;
        }

        const string risk = "The suggestion form stores a hash of each visitor's IP address to " +
                            "enforce its daily cap. Without a secret key that hash covers only " +
                            "2^32 possible addresses and inverts in minutes, so suggestions.json " +
                            "would hold visitor IP addresses in all but name.";

        var key = config[KeyName]?.Trim();

        if (string.IsNullOrEmpty(key))
        {
            return $"Refusing to start without an IP hash key. {risk} Set the " +
                   $"{EnvironmentVariable} environment variable to a long random string and " +
                   "start again. See PRD 06.";
        }

        if (key.Length < MinimumKeyLength)
        {
            return $"Refusing to start: the IP hash key is too short. {risk} " +
                   $"{EnvironmentVariable} must be at least {MinimumKeyLength} characters. " +
                   "See PRD 06.";
        }

        return null;
    }

    /// <summary>
    /// Builds the hasher from configuration. Call <see cref="ReasonToRefuse"/> first: outside
    /// Development a missing key is a refusal, so the random fallback here is only ever
    /// reached on the author's machine.
    /// </summary>
    public static IpHasher Create(IHostEnvironment env, IConfiguration config)
    {
        var key = config[KeyName]?.Trim();
        return string.IsNullOrEmpty(key)
            ? new IpHasher(RandomNumberGenerator.GetBytes(MinimumKeyLength))
            : new IpHasher(Encoding.UTF8.GetBytes(key));
    }

    /// <summary>A hasher with a known key. For tests, and for nothing else.</summary>
    public static IpHasher WithKey(string key) => new(Encoding.UTF8.GetBytes(key));

    /// <summary>
    /// The keyed hash of an address, or null when there is no address to hash — which is
    /// what <see cref="SuggestionStore"/> reads as "no IP layer for this submission".
    /// </summary>
    public string? Hash(string? ip) =>
        string.IsNullOrWhiteSpace(ip)
            ? null
            : Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(ip)));
}
