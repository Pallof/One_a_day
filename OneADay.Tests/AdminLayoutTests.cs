using Bunit;
using Microsoft.Extensions.DependencyInjection;
using OneADay.Components.Pages;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The admin page's shape: the form always in view, every section under it collapsible,
/// and all of them collapsed until the author opens one.
/// </summary>
public class AdminLayoutTests : BunitContext
{
    private readonly TestEnvironment _env = new();

    public AdminLayoutTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _env.SeedTeasers(new BrainTeaser
        {
            Date = new DateOnly(2026, 9, 1),
            Difficulty = Difficulty.Medium,
            Question = "a question",
            Answer = "an answer",
        });
        Services.AddSingleton(_env.NewTeaserStore());
        Services.AddSingleton(_env.NewStatsStore());
        Services.AddSingleton(_env.NewSuggestionStore());
        Services.AddSingleton(new ImageStore(_env));
        Services.AddSingleton(_env.NewIssueStore());
        Services.AddSingleton(_env.NewRotationStore());
    }

    [Fact]
    public void Every_section_below_the_form_collapses_and_starts_collapsed()
    {
        var page = Render<Admin>();

        var sections = page.FindAll("details.oad-admin-section");

        Assert.Equal(
            new[] { "Scheduled & past teasers", "Visitor suggestions", "Recycling box", "Reported issues" },
            sections.Select(s => s.QuerySelector("summary > span")!.TextContent.Trim()));
        Assert.All(sections, s => Assert.False(s.HasAttribute("open")));
    }

    [Fact]
    public void The_form_itself_never_collapses()
    {
        var page = Render<Admin>();

        Assert.Null(page.Find("#question").Closest("details"));
    }

    [Fact]
    public void A_collapsed_section_still_shows_what_needs_attention()
    {
        // The recycling box's count is the only warning that the queue is running dry, so
        // it has to survive the section being collapsed.
        var page = Render<Admin>();

        var recycling = page.FindAll("details.oad-admin-section")[2];

        Assert.Contains("scheduled ahead", recycling.QuerySelector("summary")!.TextContent);
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
