using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using OneADay.Components.Pages;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The unsubscribe link: one click from the email, nothing more to press — but only once a
/// real browser is on the page. <see cref="RendererInfo"/> is how the page tells the two
/// apart: the prerender (all a link scanner ever fetches) isn't interactive; a browser's
/// live connection is.
/// </summary>
public class UnsubscribePageTests : BunitContext
{
    private static readonly DateTime T0 = new(2026, 9, 11, 16, 0, 0, DateTimeKind.Utc);

    private readonly TestEnvironment _env = new();
    private readonly SubscriberStore _store;

    public UnsubscribePageTests()
    {
        _store = _env.NewSubscriberStore();
        Services.AddSingleton(_store);
    }

    private string ConfirmedToken()
    {
        var token = _store.Request("reader@example.com", T0).Token!;
        _store.Confirm(token, T0);
        return token;
    }

    private IRenderedComponent<Unsubscribe> Open(string? token, bool inBrowser)
    {
        SetRendererInfo(new RendererInfo(inBrowser ? "Server" : "Static", isInteractive: inBrowser));
        Services.GetRequiredService<NavigationManager>().NavigateTo($"unsubscribe?token={token}");
        return Render<Unsubscribe>();
    }

    [Fact]
    public void Opening_the_link_in_a_browser_unsubscribes_with_no_click()
    {
        var token = ConfirmedToken();

        var page = Open(token, inBrowser: true);

        Assert.Null(_store.FindByToken(token));
        Assert.Contains("Sorry to see you go", page.Markup);
        Assert.Empty(page.FindAll("button"));   // nothing left to press
    }

    [Fact]
    public void The_prerender_a_link_scanner_sees_changes_nothing()
    {
        var token = ConfirmedToken();

        var page = Open(token, inBrowser: false);

        Assert.NotNull(_store.FindByToken(token));
        Assert.DoesNotContain("Sorry to see you go", page.Markup);
    }

    [Theory]
    [InlineData("not-a-real-token")]
    [InlineData("")]
    public void A_used_or_broken_link_says_so_instead_of_claiming_success(string token)
    {
        // Deleting on unsubscribe means a used link and a mangled one look identical here.
        // "You're unsubscribed" would be a false promise to someone still receiving mail.
        var page = Open(token, inBrowser: true);

        Assert.Contains("You're not subscribed", page.Markup);
        Assert.DoesNotContain("Sorry to see you go", page.Markup);
    }

    [Fact]
    public void A_second_visit_after_unsubscribing_is_harmless()
    {
        var token = ConfirmedToken();
        Open(token, inBrowser: true);

        var again = Open(token, inBrowser: true);

        Assert.Contains("You're not subscribed", again.Markup);
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
