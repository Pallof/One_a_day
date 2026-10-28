using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneADay.Components.Pages;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// Admin's "Answers Jev judged" (PRD 02): Jev's verdicts the author hasn't ruled on yet, and the
/// two rulings, which go into the question's answers in teasers.json.
/// </summary>
public class JudgedAnswersAdminTests : BunitContext
{
    private readonly TestEnvironment _env = new();
    private static readonly Guid PlaceId = Guid.Parse("7d0f6a52-2c4e-4c35-9a3e-0c1d5b7e9f11");

    public JudgedAnswersAdminTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _env.SeedTeasers(new BrainTeaser
        {
            Id = PlaceId,
            Date = new DateOnly(2026, 9, 1),
            Question = "You pass the runner in second place. Where are you now?",
            Answer = "second; 2nd",
            NotAccepted = "first",
        });
        _env.WriteDataFile(JudgedAnswerStore.FileName, JsonSerializer.Serialize(new[]
        {
            Judged("right behind the leader", 0.95),
            Judged("2nd!", 0.97),           // the checker has passed it since
            Judged("First", 0.97),          // the author has ruled it wrong since
            Judged("second; or first", 0.40),
        }));
        Services.AddSingleton(_env.NewTeaserStore());
        Services.AddSingleton(_env.NewStatsStore());
        Services.AddSingleton(_env.NewSuggestionStore());
        Services.AddSingleton(new ImageStore(_env));
        Services.AddSingleton(_env.NewIssueStore());
        Services.AddSingleton(_env.NewRotationStore());
        Services.AddSingleton(_env.NewMetricsStore());
        Services.AddSingleton(new JudgedAnswerStore(_env));
        Services.AddSingleton(Options.Create(new JevOptions()));
    }

    private static JudgedAnswer Judged(string typed, double chance) => new()
    {
        TeaserId = PlaceId,
        Submission = typed,
        Chance = chance,
        JudgedAt = new DateTime(2026, 9, 2, 8, 0, 0),
    };

    private BrainTeaser Saved() => Services.GetRequiredService<TeaserStore>().GetById(PlaceId)!;

    private static IEnumerable<string> Listed(IRenderedComponent<Admin> page) =>
        page.FindAll(".jv-judged .jv-typed").Select(p => p.TextContent.Trim());

    [Fact]
    public void Lists_only_the_verdicts_still_waiting_for_a_ruling()
    {
        var page = Render<Admin>();

        Assert.Equal(["right behind the leader", "second; or first"], Listed(page));
        var section = page.FindAll("details.oad-admin-section").Single(s => s.TextContent.Contains("Answers Jev judged"));
        Assert.Contains("2 to review", section.QuerySelector("summary")!.TextContent);
    }

    [Theory]
    // Right or wrong comes from the chance and today's bar, so moving the bar relabels the list
    [InlineData(0.7, "Counts as right", "Counts as wrong")]
    [InlineData(0.96, "Counts as wrong", "Counts as wrong")]
    [InlineData(0.3, "Counts as right", "Counts as right")]
    public void Each_verdict_is_labelled_by_todays_bar(double bar, string at95, string at40)
    {
        Services.AddSingleton(Options.Create(new JevOptions { Threshold = bar }));   // the last registration wins
        var page = Render<Admin>();

        var labels = page.FindAll(".jv-judged .jv-verdict").Select(s => s.TextContent.Trim());

        Assert.Equal([at95, at40], labels);
    }

    [Fact]
    public void Adding_to_the_accepted_answers_lets_the_checker_pass_it()
    {
        var page = Render<Admin>();

        page.FindAll(".jv-judged")[0].QuerySelector("button.jv-accept")!.Click();

        Assert.True(Saved().AcceptsAnswer("Right behind the leader"));
        Assert.Equal("second", Saved().AcceptedAnswers()[0]);   // the answer the site shows is unchanged
        Assert.Equal(["second; or first"], Listed(page));
    }

    [Fact]
    public void A_semicolon_in_what_was_typed_doesnt_split_it_into_two_answers()
    {
        // Added as typed, "first" would have become an accepted answer of its own.
        var page = Render<Admin>();

        page.FindAll(".jv-judged")[1].QuerySelector("button.jv-accept")!.Click();

        Assert.True(Saved().AcceptsAnswer("second; or first"));
        Assert.False(Saved().AcceptsAnswer("or first"));
    }

    [Fact]
    public void Ruling_it_wrong_beats_Jev_and_clears_it_from_the_list()
    {
        var page = Render<Admin>();

        page.FindAll(".jv-judged")[0].QuerySelector("button.jv-rule-out")!.Click();

        Assert.True(Saved().RulesOut("right behind the leader"));
        Assert.True(Saved().RulesOut("first"));   // the earlier ruling is kept
        Assert.Equal(["second; or first"], Listed(page));
    }

    [Fact]
    public void Editing_and_saving_a_question_keeps_the_answers_ruled_wrong()
    {
        // Edit copies the question into the form field by field; a field left out of the copy
        // is wiped by the next save.
        var page = Render<Admin>();

        page.FindAll("button.oad-btn-plain").First(b => b.TextContent.Trim() == "Edit").Click();
        page.Find("button.oad-btn-green").Click();

        Assert.Equal("first", Saved().NotAccepted);
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
