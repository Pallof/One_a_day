using System.Net;
using Bunit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneADay.Components;
using OneADay.Components.Layout;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The first door: a browser asking the server for <c>/admin</c>. Runs the real gate on a
/// loopback port, with a stand-in for the rest of the app behind it, so a request that
/// gets past the gate is visible as "reached the app".
/// </summary>
public class AdminGateTests
{
    private const string Reached = "reached the app";

    private static async Task<(HttpStatusCode Status, string Body)> Get(string environment, string path)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        await using var app = builder.Build();
        app.UseAdminOnlyInDevelopment();
        app.Run(context => context.Response.WriteAsync(Reached));
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var http = new HttpClient { BaseAddress = new Uri(address) };
        var response = await http.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        await app.StopAsync();
        return (response.StatusCode, body);
    }

    [Theory]
    [InlineData("Production", "/admin")]
    [InlineData("Production", "/admin/")]
    [InlineData("Production", "/ADMIN")]
    [InlineData("Production", "/admin/anything")]
    [InlineData("Staging", "/admin")]   // fails closed: anything that isn't the author's machine
    public async Task Outside_development_every_admin_address_is_not_found(string environment, string path)
    {
        var (status, body) = await Get(environment, path);

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.DoesNotContain(Reached, body);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/yesterday")]
    [InlineData("/administrator")]   // shares the letters, not the path segment
    public async Task The_rest_of_the_live_site_is_untouched(string path)
    {
        var (status, body) = await Get("Production", path);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(Reached, body);
    }

    [Fact]
    public async Task On_the_authors_machine_admin_is_reachable()
    {
        // The control case: without it, a gate that blocked everything would pass the
        // tests above.
        var (status, body) = await Get("Development", "/admin");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(Reached, body);
    }
}

/// <summary>
/// The second door: in-app navigation, which never asks the server for a URL. And the menu
/// link, which shouldn't advertise a page that isn't there.
/// </summary>
public class AdminRoutingTests : BunitContext
{
    private readonly TestEnvironment _env = new();

    private void RunningIn(string environment)
    {
        _env.EnvironmentName = environment;
        Services.AddSingleton<IWebHostEnvironment>(_env);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>
    /// Register everything before calling this: bUnit locks its services the first time
    /// one is read, and both lines here read one.
    /// </summary>
    private IRenderedComponent<Routes> NavigateInApp(string path)
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        Services.GetRequiredService<NavigationManager>().NavigateTo(path);
        return Render<Routes>();
    }

    [Fact]
    public void On_the_live_site_the_router_never_builds_the_admin_page()
    {
        // No admin services are registered, deliberately. If the router built the page at
        // all, its injections would fail — and this test with them.
        RunningIn("Production");

        var page = NavigateInApp("admin");

        Assert.Contains("Not Found", page.Markup);
        Assert.DoesNotContain("Add a brain teaser", page.Markup);
        Assert.Empty(page.FindAll("button.oad-btn-green"));
    }

    [Fact]
    public void On_the_authors_machine_the_router_builds_it_answers_and_all()
    {
        RunningIn("Development");
        _env.SeedTeasers(new BrainTeaser
        {
            Date = new DateOnly(2026, 9, 1),
            Difficulty = Difficulty.Medium,
            Question = "a question",
            Answer = "a secret answer",
        });
        Services.AddSingleton(_env.NewTeaserStore());
        Services.AddSingleton(_env.NewStatsStore());
        Services.AddSingleton(_env.NewSuggestionStore());
        Services.AddSingleton(new ImageStore(_env));
        Services.AddSingleton(_env.NewIssueStore());
        Services.AddSingleton(_env.NewRotationStore());

        var page = NavigateInApp("admin");

        Assert.Contains("Add a brain teaser", page.Markup);
        // The table lists every answer, scheduled days included — the reason this page
        // must never exist on the live site.
        Assert.Contains("a secret answer", page.Markup);
    }

    [Theory]
    [InlineData("Production", false)]
    [InlineData("Development", true)]
    public void The_menu_offers_admin_only_on_the_authors_machine(string environment, bool offered)
    {
        RunningIn(environment);
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        // Off the home page, which would also add the sign-up dialog and its services.
        Services.GetRequiredService<NavigationManager>().NavigateTo("about");

        var layout = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(_ => { })));

        Assert.Equal(offered, layout.FindAll("a[href='admin']").Count > 0);
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
