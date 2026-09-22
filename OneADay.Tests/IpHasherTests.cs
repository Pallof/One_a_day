using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The keyed hash that stands between suggestions.json and a list of visitor IP addresses.
///
/// <para>Two properties carry the whole design: the hash must be <b>deterministic</b>, or the
/// per-IP cap stops recognising a repeat visitor, and it must be <b>key-dependent</b>, or it
/// is a plain hash of a 2³² keyspace and inverts in minutes. A test suite that only checked
/// the first would pass on the exact implementation this replaces.</para>
/// </summary>
public class IpHasherTests
{
    private const string GoodKey = "a-long-enough-test-key-000000000000";

    private sealed class Host(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "OneADay.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Config(string? key) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [IpHasher.KeyName] = key })
            .Build();

    // ---- the hash itself -------------------------------------------------------

    [Fact]
    public void The_same_address_always_hashes_the_same_way()
    {
        // Without this the daily cap can't recognise a repeat visitor at all.
        var hasher = IpHasher.WithKey(GoodKey);

        Assert.Equal(hasher.Hash("203.0.113.7"), hasher.Hash("203.0.113.7"));
    }

    [Fact]
    public void Different_addresses_hash_differently()
    {
        var hasher = IpHasher.WithKey(GoodKey);

        Assert.NotEqual(hasher.Hash("203.0.113.7"), hasher.Hash("203.0.113.8"));
    }

    [Fact]
    public void The_key_changes_the_hash()
    {
        // The point of the whole class. An unkeyed implementation passes every other test
        // here and fails this one.
        var one = IpHasher.WithKey(GoodKey);
        var two = IpHasher.WithKey("a-different-key-of-sufficient-length");

        Assert.NotEqual(one.Hash("203.0.113.7"), two.Hash("203.0.113.7"));
    }

    [Fact]
    public void The_hash_is_not_the_unkeyed_one()
    {
        // Guards against a regression to plain SHA-256, which would still be deterministic
        // and still differ per address — so nothing above would catch it.
        const string ip = "203.0.113.7";
        var unkeyed = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ip)));

        Assert.NotEqual(unkeyed, IpHasher.WithKey(GoodKey).Hash(ip));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_address_hashes_to_null(string? ip)
    {
        // SuggestionStore reads null as "no IP layer for this submission" and falls back to
        // the per-device limit, rather than treating every anonymous visitor as one person.
        Assert.Null(IpHasher.WithKey(GoodKey).Hash(ip));
    }

    // ---- the startup refusal ---------------------------------------------------

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Without_a_key_the_live_site_refuses_to_start(string environment)
    {
        // Fails closed: a misspelt environment name is not Development, so it refuses too.
        Assert.NotNull(IpHasher.ReasonToRefuse(new Host(environment), Config(null)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")]
    public void A_missing_or_token_key_refuses_too(string key)
    {
        // A one-character key would silence the refusal while leaving the hash as good as
        // unkeyed, which is the failure this check exists to prevent.
        Assert.NotNull(IpHasher.ReasonToRefuse(new Host("Production"), Config(key)));
    }

    [Fact]
    public void A_real_key_starts()
    {
        // The control case. Without it, a check that refused everything would pass above.
        Assert.Null(IpHasher.ReasonToRefuse(new Host("Production"), Config(GoodKey)));
    }

    [Fact]
    public void Development_starts_without_a_key()
    {
        // A fresh clone has no user-secrets and should still just run.
        Assert.Null(IpHasher.ReasonToRefuse(new Host("Development"), Config(null)));
    }

    [Fact]
    public void Development_without_a_key_still_gets_a_working_hasher()
    {
        var hasher = IpHasher.Create(new Host("Development"), Config(null));

        Assert.NotNull(hasher.Hash("203.0.113.7"));
        Assert.Equal(hasher.Hash("203.0.113.7"), hasher.Hash("203.0.113.7"));
    }

    [Fact]
    public void The_refusal_says_which_variable_to_set()
    {
        // Whoever reads this is standing on a server that won't start. Unlike the
        // Development-mode guard, naming the variable here is the fix, not a bypass.
        var reason = IpHasher.ReasonToRefuse(new Host("Production"), Config(null));

        Assert.NotNull(reason);
        Assert.Contains(IpHasher.EnvironmentVariable, reason);
    }
}
