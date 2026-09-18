using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// Whether a forwarded address is believed. Runs real requests on a loopback port, because
/// trust here is decided by <i>who sent</i> the header — which no unit test can express.
///
/// <para>The stakes: believed when it shouldn't be, any visitor can name their own address and
/// walk past the per-IP cap. Not believed when it should be, every visitor collapses into the
/// proxy's address and the suggestion form closes for the entire internet after three
/// submissions a day.</para>
/// </summary>
public class ProxyHeadersTests
{
    private const string Forwarded = "198.51.100.42";

    /// <summary>The address the app ends up seeing, for a request carrying X-Forwarded-For.</summary>
    private static async Task<string> AddressSeen(Action<ProxyOptions> configure)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.Configure(configure);

        await using var app = builder.Build();
        app.UseProxyHeaders();
        app.Run(context => context.Response.WriteAsync(
            context.Connection.RemoteIpAddress?.ToString() ?? "none"));
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var http = new HttpClient { BaseAddress = new Uri(address) };
        http.DefaultRequestHeaders.Add("X-Forwarded-For", Forwarded);
        var seen = await http.GetStringAsync("/");

        await app.StopAsync();
        return seen;
    }

    [Fact]
    public async Task Switched_off_a_forwarded_address_is_ignored()
    {
        // The author's machine, and every test. Nothing sets these headers locally, so
        // believing them would let any client name its own address.
        var seen = await AddressSeen(o => o.Enabled = false);

        Assert.NotEqual(Forwarded, seen);
    }

    [Fact]
    public async Task A_sender_that_is_not_a_known_proxy_is_ignored()
    {
        // The request comes from loopback; the only trusted proxy is somewhere else. This
        // also pins the decision that explicit configuration REPLACES the middleware's
        // built-in trust of loopback — appending to it would make this pass for the wrong
        // reason, and make the configuration look authoritative when it wasn't.
        var seen = await AddressSeen(o =>
        {
            o.Enabled = true;
            o.KnownProxies = ["10.1.2.3"];
        });

        Assert.NotEqual(Forwarded, seen);
    }

    [Fact]
    public async Task A_known_proxy_is_believed()
    {
        var seen = await AddressSeen(o =>
        {
            o.Enabled = true;
            o.KnownProxies = ["127.0.0.1"];
        });

        Assert.Equal(Forwarded, seen);
    }

    [Fact]
    public async Task Trusting_every_proxy_believes_the_header()
    {
        // For hosts that don't document a fixed proxy address.
        var seen = await AddressSeen(o =>
        {
            o.Enabled = true;
            o.TrustAllProxies = true;
        });

        Assert.Equal(Forwarded, seen);
    }

    [Fact]
    public async Task A_known_network_is_believed()
    {
        var seen = await AddressSeen(o =>
        {
            o.Enabled = true;
            o.KnownNetworks = ["127.0.0.0/8"];
        });

        Assert.Equal(Forwarded, seen);
    }

    // ---- the startup refusal ---------------------------------------------------

    [Fact]
    public void Announcing_a_proxy_without_trusting_one_refuses_to_start()
    {
        // The silent trap: the middleware would run, trust nothing, ignore every header, and
        // leave both failures in place with no error to chase.
        var reason = new ProxyOptions { Enabled = true }.ReasonToRefuse();

        Assert.NotNull(reason);
        Assert.Contains(ProxyOptions.Section, reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_trusted_source_or_no_proxy_at_all_starts(bool trustAll)
    {
        // Two control cases: switched off entirely, and switched on with a source named.
        Assert.Null(new ProxyOptions { Enabled = false }.ReasonToRefuse());
        Assert.Null(new ProxyOptions
        {
            Enabled = true,
            TrustAllProxies = trustAll,
            KnownProxies = trustAll ? [] : ["10.1.2.3"],
        }.ReasonToRefuse());
    }
}
