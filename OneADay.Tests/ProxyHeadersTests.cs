using System.Net;
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

    /// <summary>
    /// The address and scheme the app ends up seeing, for one request carrying forwarded
    /// headers.
    /// </summary>
    /// <param name="sender">Where the request appears to come from. The test's own requests
    /// come from loopback, which the framework trusts by default — so a test that never moves
    /// the sender elsewhere can't tell "trusted by our settings" from "trusted anyway"
    /// (test audit, 2026-09-28).</param>
    private static async Task<(string Address, string Scheme)> Seen(
        Action<ProxyOptions> configure, string? sender = null,
        string forwardedFor = Forwarded, string? forwardedProto = null)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.Configure(configure);

        await using var app = builder.Build();
        if (sender is not null)
        {
            app.Use((context, next) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(sender);   // a proxy there
                return next(context);
            });
        }
        app.UseProxyHeaders();
        app.Run(context => context.Response.WriteAsync(
            $"{context.Connection.RemoteIpAddress?.ToString() ?? "none"} {context.Request.Scheme}"));
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var http = new HttpClient { BaseAddress = new Uri(address) };
        http.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        if (forwardedProto is not null)
        {
            http.DefaultRequestHeaders.Add("X-Forwarded-Proto", forwardedProto);
        }
        var seen = (await http.GetStringAsync("/")).Split(' ');

        await app.StopAsync();
        return (seen[0], seen[1]);
    }

    /// <summary>The address the app ends up seeing, for a request carrying X-Forwarded-For.</summary>
    private static async Task<string> AddressSeen(Action<ProxyOptions> configure, string? sender = null) =>
        (await Seen(configure, sender)).Address;

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
        // For hosts that don't document a fixed proxy address — so from any sender, not
        // just the loopback the framework trusts anyway.
        var seen = await AddressSeen(o =>
        {
            o.Enabled = true;
            o.TrustAllProxies = true;
        }, sender: "203.0.113.9");

        Assert.Equal(Forwarded, seen);
    }

    [Fact]
    public async Task A_known_network_is_believed()
    {
        var seen = await AddressSeen(o =>
        {
            o.Enabled = true;
            o.KnownNetworks = ["10.0.0.0/8"];
        }, sender: "10.1.2.3");

        Assert.Equal(Forwarded, seen);
    }

    [Fact]
    public async Task A_sender_outside_the_known_networks_is_ignored()
    {
        // The other half of the test above. Naming a network must trust only that network:
        // dropped entirely, the lists would be empty, which this middleware reads as
        // "trust everyone".
        var seen = await AddressSeen(o =>
        {
            o.Enabled = true;
            o.KnownNetworks = ["10.0.0.0/8"];
        });

        Assert.NotEqual(Forwarded, seen);
    }

    [Fact]
    public async Task The_forwarded_scheme_is_believed_too()
    {
        // Cloudflare and Fly end HTTPS and pass the request on as plain HTTP. Without the
        // forwarded scheme the app would redirect every request to HTTPS, forever.
        var (_, scheme) = await Seen(o =>
        {
            o.Enabled = true;
            o.TrustAllProxies = true;
        }, sender: "203.0.113.9", forwardedProto: "https");

        Assert.Equal("https", scheme);
    }

    [Fact]
    public async Task Only_as_many_hops_as_the_limit_are_walked_back()
    {
        // A client can put anything at the front of X-Forwarded-For; each real proxy adds the
        // address it saw at the end. With one proxy, one hop back is the truth, and walking
        // further would hand the client the address it wrote itself.
        var forged = await Seen(o =>
        {
            o.Enabled = true;
            o.TrustAllProxies = true;
            o.ForwardLimit = 1;
        }, sender: "203.0.113.9", forwardedFor: $"192.0.2.66, {Forwarded}");

        Assert.Equal(Forwarded, forged.Address);
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
