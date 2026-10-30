using Bunit;
using Microsoft.Extensions.DependencyInjection;
using OneADay.Components.Pages;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The admin page's shape: the form always in view, every section under it collapsible,
/// and all of them collapsed until the author opens one. And what a report shows there.
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
        Services.AddSingleton(_env.NewMetricsStore());
        Services.AddSingleton(new JudgedAnswerStore(_env));
        Services.AddSingleton(new AgreedVerdictStore(_env));
    }

    [Fact]
    public void Every_section_below_the_form_collapses_and_starts_collapsed()
    {
        var page = Render<Admin>();

        var sections = page.FindAll("details.oad-admin-section");

        Assert.Equal(
            new[] { "Scheduled & past teasers", "Visitor suggestions", "Recycling box", "Site metrics", "Reported issues", "Answers Jev judged" },
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
        // it has to survive the section being collapsed. The number, not just its label:
        // "scheduled ahead" alone would still be there with the count dropped.
        Services.GetRequiredService<TeaserStore>().Save(new BrainTeaser
        {
            Date = AppTime.Today.AddDays(3),
            Difficulty = Difficulty.Easy,
            Question = "a question for later",
            Answer = "later",
        });
        var page = Render<Admin>();

        var recycling = page.FindAll("details.oad-admin-section")[2];

        Assert.Contains("1 scheduled ahead", recycling.QuerySelector("summary")!.TextContent);
    }

    [Fact]
    public void A_twenty_four_report_shows_its_hand_and_keeps_it_on_disk()
    {
        Services.GetRequiredService<IssueStore>().Add(new IssueReport
        {
            Category = IssueCategories.Evaluation,
            Details = "my answer was marked wrong",
            PageUrl = "/twentyfour",
            TwentyFourHand = [2, 5, 7, 9],
        });

        var page = Render<Admin>();

        Assert.Contains("Twenty Four hand: 2 5 7 9", page.Find(".oad-issue-context").TextContent);
        Assert.Equal([2, 5, 7, 9], _env.NewIssueStore().GetAll().Single().TwentyFourHand);
    }

    [Fact]
    public void A_suggestion_shows_who_to_credit_or_that_no_name_was_given()
    {
        // The second is written as the file was before the form asked for a name: no field at
        // all, which has to load as no name rather than fail.
        _env.WriteDataFile("suggestions.json", """
            {
              "Suggestions": [
                { "Id": "11111111-1111-1111-1111-111111111111", "SubmittedAt": "2026-10-30T09:00:00",
                  "Difficulty": "Easy", "Question": "named", "SolutionAndHint": "s", "AuthorName": "Sam Rivera" },
                { "Id": "22222222-2222-2222-2222-222222222222", "SubmittedAt": "2026-10-29T09:00:00",
                  "Difficulty": "Hard", "Question": "from before", "SolutionAndHint": "s" }
              ],
              "Log": []
            }
            """);
        Services.AddSingleton(_env.NewSuggestionStore());

        var page = Render<Admin>();

        Assert.Equal(
            ["From Sam Rivera", "No name given"],
            page.FindAll(".oad-suggestion-author").Select(a => a.TextContent.Trim()));
    }

    [Fact]
    public void Using_a_suggestion_carries_its_name_into_the_bank()
    {
        var suggestions = _env.NewSuggestionStore();
        suggestions.Add(new TeaserSuggestion { Question = "a visitor's riddle", SolutionAndHint = "s", AuthorName = "Sam Rivera" },
            Guid.NewGuid(), null, AppTime.Today);
        Services.AddSingleton(suggestions);
        var page = Render<Admin>();

        page.FindAll("button").Single(b => b.TextContent.Trim() == "Use in form above").Click();

        Assert.Equal("Sam Rivera", page.Find("#suggested-by").GetAttribute("value"));
        page.Find("#answer").Change("an answer");
        page.Find(".oad-admin-actions button.oad-btn-green").Click();
        Assert.Equal("Sam Rivera", _env.NewTeaserStore().GetAll().Single(t => t.Question == "a visitor's riddle").SuggestedBy);
    }

    [Fact]
    public void Editing_a_credited_teaser_keeps_its_credit_and_an_emptied_one_saves_as_none()
    {
        var teasers = Services.GetRequiredService<TeaserStore>();
        var credited = teasers.GetAll().Single();
        credited.SuggestedBy = "Sam Rivera";
        teasers.Save(credited);
        var page = Render<Admin>();

        page.FindAll("button").First(b => b.TextContent.Trim() == "Edit").Click();
        page.Find(".oad-admin-actions button.oad-btn-green").Click();
        Assert.Equal("Sam Rivera", _env.NewTeaserStore().GetAll().Single().SuggestedBy);

        page.FindAll("button").First(b => b.TextContent.Trim() == "Edit").Click();
        page.Find("#suggested-by").Change(" \u3164 ");   // looks blank, and is saved as none
        page.Find(".oad-admin-actions button.oad-btn-green").Click();
        Assert.Null(_env.NewTeaserStore().GetAll().Single().SuggestedBy);
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
