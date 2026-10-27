using Bunit;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneADay.Components;
using OneADay.Components.Pages;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// What pages and boxes are called: a page carries the menu's name for it, and an answer box
/// is named by the heading above it, so a screen reader says what the box is for.
/// </summary>
public class PageNamingTests : BunitContext
{
    private readonly TestEnvironment _env = new();

    public PageNamingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDataProtection();
        Services.AddScoped<ProtectedLocalStorage>();
    }

    [Fact]
    public void The_suggestion_page_is_called_what_the_menu_calls_it()
    {
        // It was "Contact us" in its heading and tab while the menu said "Suggest a teaser"
        // (cold review, 2026-10-15).
        Services.AddSingleton(_env.NewSuggestionStore());
        Services.AddSingleton(new EmailNotifier(Options.Create(new EmailOptions()), NullLogger<EmailNotifier>.Instance));
        Services.AddSingleton<IHttpContextAccessor>(FixedHttpContextAccessor.From("203.0.113.7"));
        Services.AddSingleton(IpHasher.WithKey("page-naming-tests-fixed-key-0000000000"));

        var page = Render<Contact>();

        Assert.Equal("Suggest a teaser", page.Find(".oad-banner").TextContent.Trim());
        var tab = Render(page.FindComponent<PageTitle>().Instance.ChildContent!);
        Assert.Equal("Stumpty · Suggest a teaser", tab.Markup.Trim());
    }

    [Fact]
    public void The_daily_answer_box_is_named_by_its_heading()
    {
        Services.AddSingleton(_env.NewStatsStore());
        Services.AddScoped<CurrentTeaserContext>();
        Services.AddMetrics(_env);

        var page = Render<ChallengeView>(p => p.Add(c => c.Teaser, TeaserFactory.On("2026-08-12")));
        var box = page.Find("textarea.cv-input");

        var label = page.Find($"#{box.GetAttribute("aria-labelledby")}");
        Assert.Equal("Submission:", label.TextContent.Trim());
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
