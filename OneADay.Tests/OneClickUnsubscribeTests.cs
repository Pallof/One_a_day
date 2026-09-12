using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneADay.Components;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The one-click unsubscribe endpoint, over real HTTP on a loopback port.
///
/// <para>Routing mistakes only show up once endpoints are composed, so this maps the
/// endpoint next to the Blazor pages the same way Program.cs does. That pairing is what
/// produced an AmbiguousMatchException — a 500 to every mail client's Unsubscribe button
/// — while the page still worked in a browser and every other test passed.</para>
///
/// <para>No mail can leave this host: it registers no mailer and no background services,
/// and its content root is a throwaway directory, never the real App_Data.</para>
/// </summary>
public sealed class OneClickUnsubscribeTests : IAsyncLifetime
{
    private static readonly DateTime T0 = new(2026, 9, 11, 16, 0, 0, DateTimeKind.Utc);

    private readonly TestEnvironment _env = new();
    private WebApplication _app = null!;
    private HttpClient _http = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = _env.ContentRootPath,
            ApplicationName = typeof(App).Assembly.GetName().Name,
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");   // any free port
        builder.Logging.ClearProviders();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddSingleton<SubscriberStore>();

        _app = builder.Build();
        _app.UseAntiforgery();
        _app.MapOneClickUnsubscribe();
        _app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        await _app.StartAsync();

        var address = _app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _http = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.DisposeAsync();
        _env.Dispose();
    }

    private SubscriberStore Store => _app.Services.GetRequiredService<SubscriberStore>();

    private string ConfirmedSubscriberToken()
    {
        var token = Store.Request("reader@example.com", T0).Token!;
        Store.Confirm(token, T0);
        return token;
    }

    /// <summary>Exactly what RFC 8058 says a mail client sends.</summary>
    private Task<HttpResponseMessage> OneClickPost(string token) =>
        _http.PostAsync($"/unsubscribe?token={token}",
            new FormUrlEncodedContent([new("List-Unsubscribe", "One-Click")]));

    [Fact]
    public async Task One_click_post_unsubscribes_and_answers_200()
    {
        var token = ConfirmedSubscriberToken();

        var response = await OneClickPost(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(Store.FindByToken(token));
    }

    [Fact]
    public async Task Unknown_token_gets_the_same_200()
    {
        // Same answer either way, so the endpoint can't be used to test which tokens exist.
        var response = await OneClickPost("not-a-real-token");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_plain_fetch_of_the_link_unsubscribes_nobody()
    {
        // A GET with no browser behind it is exactly what a mail filter's link scanner
        // does, often the moment the digest arrives. It must reach the page, not the POST
        // endpoint, and the page must not act on it: unsubscribing waits for a live
        // browser (UnsubscribePageTests covers that half).
        var token = ConfirmedSubscriberToken();

        var html = await _http.GetStringAsync($"/unsubscribe?token={token}");

        Assert.Contains("Unsubscribing…", html);
        Assert.DoesNotContain("Sorry to see you go", html);
        Assert.NotNull(Store.FindByToken(token));
    }
}
