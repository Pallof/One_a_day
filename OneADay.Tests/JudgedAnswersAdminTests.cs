using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneADay.Components.Pages;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// Admin's "Answers Jev judged" (PRD 02): Jev's verdicts still in force that the author hasn't
/// dealt with, "Agree with Jev", which only clears the row, and "Rule it wrong", which goes into
/// teasers.json. Nothing a visitor typed can be made an accepted answer from here.
/// </summary>
public class JudgedAnswersAdminTests : BunitContext
{
    private readonly TestEnvironment _env = new();
    private static readonly Guid PlaceId = Guid.Parse("7d0f6a52-2c4e-4c35-9a3e-0c1d5b7e9f11");

    private static readonly BrainTeaser Place = new()
    {
        Id = PlaceId,
        Date = new DateOnly(2026, 9, 1),
        Question = "You pass the runner in second place. Where are you now?",
        Answer = "second; 2nd",
        NotAccepted = "first",
    };

    public JudgedAnswersAdminTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _env.SeedTeasers(Place);
        _env.WriteDataFile(JudgedAnswerStore.FileName, JsonSerializer.Serialize(new[]
        {
            Judged("right behind the leader", 0.95),
            Judged("2nd!", 0.97),                            // the checker has passed it since
            Judged("First", 0.97),                           // the author has ruled it wrong since
            Judged("second; or first", 0.40),
            Judged("behind the leader", 0.85),
            Judged("near the front", 0.80, against: "0123456789ABCDEF"),   // made before an edit
        }));
        Services.AddSingleton(_env.NewTeaserStore());
        Services.AddSingleton(_env.NewStatsStore());
        Services.AddSingleton(_env.NewSuggestionStore());
        Services.AddSingleton(new ImageStore(_env));
        Services.AddSingleton(_env.NewIssueStore());
        Services.AddSingleton(_env.NewRotationStore());
        Services.AddSingleton(_env.NewMetricsStore());
        Services.AddSingleton(new JudgedAnswerStore(_env));
        Services.AddSingleton(new AgreedVerdictStore(_env));
        Services.AddSingleton(Options.Create(new JevOptions()));
    }

    private static JudgedAnswer Judged(string typed, double chance, string? against = null) => new()
    {
        TeaserId = PlaceId,
        Submission = typed,
        Chance = chance,
        Against = against ?? Place.Fingerprint(),
        JudgedAt = new DateTime(2026, 9, 2, 8, 0, 0),
    };

    private BrainTeaser Saved() => Services.GetRequiredService<TeaserStore>().GetById(PlaceId)!;

    private static IEnumerable<string> Listed(IRenderedComponent<Admin> page) =>
        page.FindAll(".jv-judged .jv-typed").Select(p => p.TextContent.Trim());

    private static void Press(IRenderedComponent<Admin> page, int row, string button) =>
        page.FindAll(".jv-judged")[row].QuerySelector($"button.{button}")!.Click();

    [Fact]
    public void Lists_only_the_verdicts_still_in_force_and_waiting()
    {
        var page = Render<Admin>();

        Assert.Equal(["right behind the leader", "second; or first", "behind the leader"], Listed(page));
        var section = page.FindAll("details.oad-admin-section").Single(s => s.TextContent.Contains("Answers Jev judged"));
        // Worded as news, not a chore: nothing here waits on the author (author's decision, 2026-10-28)
        Assert.Contains("3 new from Jev", section.QuerySelector("summary")!.TextContent);
    }

    [Theory]
    // Right or wrong comes from the chance and today's bar, so moving the bar relabels the list
    [InlineData(0.7, "Counts as right", "Counts as wrong", "Counts as right")]
    [InlineData(0.96, "Counts as wrong", "Counts as wrong", "Counts as wrong")]
    [InlineData(0.3, "Counts as right", "Counts as right", "Counts as right")]
    public void Each_verdict_is_labelled_by_todays_bar(double bar, string at95, string at40, string at85)
    {
        Services.AddSingleton(Options.Create(new JevOptions { Threshold = bar }));   // the last registration wins
        var page = Render<Admin>();

        var labels = page.FindAll(".jv-judged .jv-verdict").Select(s => s.TextContent.Trim());

        Assert.Equal([at95, at40, at85], labels);
    }

    [Fact]
    public void Rule_it_wrong_is_offered_only_where_the_answer_counts_as_right()
    {
        // An answer already counted wrong needs no ruling (author's decision, 2026-10-28).
        var page = Render<Admin>();

        var offered = page.FindAll(".jv-judged").Select(row => row.QuerySelector("button.jv-rule-out") is not null);

        Assert.Equal([true, false, true], offered);   // 95%, 40% and 85% at the 70% bar
    }

    [Fact]
    public void No_button_puts_what_a_visitor_typed_into_the_accepted_answers()
    {
        // The author's decision, 2026-10-28: accepted answers stay in their own words. Each
        // button is pressed, each on the first row of a fresh page. A low bar shows them all.
        Services.AddSingleton(Options.Create(new JevOptions { Threshold = 0.3 }));
        var buttons = Render<Admin>().FindAll(".jv-judged")[0].QuerySelectorAll("button").Length;
        Assert.True(buttons >= 2);

        for (var i = 0; i < buttons; i++)
        {
            Render<Admin>().FindAll(".jv-judged")[0].QuerySelectorAll("button")[i].Click();
        }

        Assert.Equal(Place.Answer, Saved().Answer);
    }

    [Fact]
    public void Agreeing_with_Jev_clears_the_row_and_changes_nothing_in_the_question()
    {
        var page = Render<Admin>();

        Press(page, 0, "jv-agree");

        Assert.Equal(["second; or first", "behind the leader"], Listed(page));
        Assert.Equal(Place.Answer, Saved().Answer);
        Assert.Equal(Place.NotAccepted, Saved().NotAccepted);
        // Remembered on disk, as after a restart.
        Assert.True(new AgreedVerdictStore(_env).Contains(Judged("right behind the leader", 0.95)));
    }

    [Fact]
    public void An_agreement_is_with_that_verdict_only()
    {
        // Asked again after the question was edited, the new verdict comes back for review.
        var agreed = new AgreedVerdictStore(_env);
        agreed.Add(Judged("right behind the leader", 0.95));

        Assert.True(agreed.Contains(Judged("Right behind the leader!", 0.95)));
        Assert.False(agreed.Contains(Judged("right behind the leader", 0.95, against: "FEDCBA9876543210")));
    }

    [Fact]
    public void Ruling_it_wrong_beats_Jev_and_clears_it_from_the_list()
    {
        var page = Render<Admin>();

        Press(page, 0, "jv-rule-out");

        Assert.True(Saved().RulesOut("right behind the leader"));
        Assert.True(Saved().RulesOut("first"));   // the earlier ruling is kept
        Assert.Equal(["second; or first", "behind the leader"], Listed(page));
    }

    [Fact]
    public void A_semicolon_in_what_was_typed_doesnt_split_it_into_two_rulings()
    {
        // Ruled as typed, "or first" would have become a ruling of its own. At 40% it only
        // counts as right, and so offers the ruling, under a low bar.
        Services.AddSingleton(Options.Create(new JevOptions { Threshold = 0.3 }));
        var page = Render<Admin>();

        Press(page, 1, "jv-rule-out");

        Assert.True(Saved().RulesOut("second; or first"));
        Assert.False(Saved().RulesOut("or first"));
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

    [Fact]
    public void A_ruling_made_while_the_question_is_open_in_the_form_survives_saving_it()
    {
        // The form holds its own copy of the question; saving it must not put back the
        // rulings it had before.
        var page = Render<Admin>();
        page.FindAll("button.oad-btn-plain").First(b => b.TextContent.Trim() == "Edit").Click();

        Press(page, 0, "jv-rule-out");
        page.Find("button.oad-btn-green").Click();

        Assert.True(Saved().RulesOut("right behind the leader"));
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
