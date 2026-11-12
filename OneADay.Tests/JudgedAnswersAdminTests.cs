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
/// dealt with, "Agree with Jev", which only clears the row, and "Rule it wrong" and "Accept",
/// which go into teasers.json. Nothing a visitor typed becomes an accepted answer as it is:
/// Accept opens a box for the author to reword it in first.
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
    public void No_single_press_puts_what_a_visitor_typed_into_the_accepted_answers()
    {
        // The author's decision, 2026-10-28: accepted answers stay in their own words, so Accept
        // only opens a box to reword it in. Each kind of button is pressed once, each on a fresh
        // page. At the 70% bar the rows show all three kinds between them.
        var kinds = Render<Admin>().FindAll(".jv-judged button").Select(b => b.ClassName!).Distinct().ToList();
        Assert.True(kinds.Count >= 3);

        foreach (var kind in kinds)
        {
            Render<Admin>().FindAll(".jv-judged button").First(b => b.ClassName == kind).Click();
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
    public void Brackets_in_what_was_typed_dont_rule_out_each_side_on_its_own()
    {
        // PR 74 review: ruled as typed, "second place (or third)" also ruled out a bare
        // "second place", a right answer, and "or third".
        _env.WriteDataFile(JudgedAnswerStore.FileName, JsonSerializer.Serialize(new[] { Judged("second place (or third)", 0.95) }));
        Services.AddSingleton(new JudgedAnswerStore(_env));
        var page = Render<Admin>();

        Press(page, 0, "jv-rule-out");

        Assert.True(Saved().RulesOut("second place (or third)"));
        Assert.False(Saved().RulesOut("second place"));
        Assert.False(Saved().RulesOut("or third"));
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

    // ----- Accept: passing an answer Jev turned down, in the author's own words -----

    private static readonly Guid OtherId = Guid.Parse("3b9e1c44-8a2d-4f6e-b1c7-5d4e3f2a1b09");

    private static readonly BrainTeaser Other = new()
    {
        Id = OtherId,
        Date = new DateOnly(2026, 9, 3),
        Question = "A made-up question for these tests?",
        Answer = "alpha; beta",
    };

    /// <summary>Adds a second question with one verdict of its own, which counts as wrong.</summary>
    private void WithOtherQuestion()
    {
        _env.SeedTeasers(Place, Other);
        _env.WriteDataFile(JudgedAnswerStore.FileName, JsonSerializer.Serialize(new[]
        {
            Judged("right behind the leader", 0.95),
            Judged("second; or first", 0.40),
            Judged("behind the leader", 0.85),
            new JudgedAnswer
            {
                TeaserId = OtherId, Submission = "gamma", Chance = 0.30,
                Against = Other.Fingerprint(), JudgedAt = new DateTime(2026, 9, 1, 8, 0, 0),
            },
        }));
        Services.AddSingleton(_env.NewTeaserStore());       // the last registration wins
        Services.AddSingleton(new JudgedAnswerStore(_env));
    }

    /// <summary>Opens Accept on the 40% row ("second; or first"), which counts as wrong, and types into its box.</summary>
    private static void AcceptAs(IRenderedComponent<Admin> page, string wording, bool add = true)
    {
        var row = page.FindAll(".jv-judged").Single(r => r.QuerySelector(".jv-typed")!.TextContent.Trim() == "second; or first");
        row.QuerySelector("button.jv-accept")!.Click();
        page.Find("input.jv-accept-text").Input(wording);
        if (add)
        {
            page.Find("button.jv-accept-add").Click();
        }
    }

    [Fact]
    public void Accept_is_offered_only_where_the_answer_counts_as_wrong()
    {
        // An answer already counted right needs nothing more to pass.
        var page = Render<Admin>();

        var offered = page.FindAll(".jv-judged").Select(row => row.QuerySelector("button.jv-accept") is not null);

        Assert.Equal([false, true, false], offered);   // 95%, 40% and 85% at the 70% bar
    }

    [Fact]
    public void Accept_opens_a_box_filled_in_with_what_was_typed_and_saves_nothing_yet()
    {
        var page = Render<Admin>();
        Assert.Empty(page.FindAll(".jv-accept-box"));

        Press(page, 1, "jv-accept");

        var box = page.FindAll(".jv-judged")[1].QuerySelector(".jv-accept-box")!;
        Assert.Equal("second; or first", box.QuerySelector("input.jv-accept-text")!.GetAttribute("value"));
        Assert.Equal(Place.Answer, Saved().Answer);
    }

    [Fact]
    public void Add_does_nothing_without_an_answer()
    {
        var page = Render<Admin>();

        AcceptAs(page, "   ", add: false);
        Assert.True(page.Find("button.jv-accept-add").HasAttribute("disabled"));

        // Brackets become spaces, which leaves nothing the checker could match.
        AcceptAs(page, "( )");
        Assert.Contains("needs a letter or a digit", page.Find(".jv-accept-warning").TextContent);
        Assert.Equal(Place.Answer, Saved().Answer);
    }

    [Fact]
    public void Accepting_saves_the_authors_wording_to_that_question_only_and_says_to_publish()
    {
        WithOtherQuestion();
        var page = Render<Admin>();

        AcceptAs(page, "just behind the leader");

        Assert.Equal(["second", "2nd", "just behind the leader"], Saved().AcceptedAnswers());
        Assert.True(Saved().AcceptsAnswer("Just behind the leader!"));
        Assert.Equal(Other.Answer, Services.GetRequiredService<TeaserStore>().GetById(OtherId)!.Answer);
        Assert.Contains("Publish teasers.json afterwards", page.Find(".jv-accepted-note").TextContent);
        Assert.Empty(page.FindAll(".jv-accept-box"));
    }

    [Fact]
    public void A_semicolon_and_brackets_in_the_box_dont_make_more_than_one_answer()
    {
        // As written, the ';' would have made "trailing" right on its own, and the brackets
        // would have made "just" right, and the wording without it.
        var page = Render<Admin>();

        AcceptAs(page, "the runner (just) behind the leader; trailing");

        Assert.Equal(["second", "2nd", "the runner just behind the leader, trailing"], Saved().AcceptedAnswers());
        Assert.False(Saved().AcceptsAnswer("trailing"));
        Assert.False(Saved().AcceptsAnswer("just"));
        Assert.False(Saved().AcceptsAnswer("the runner behind the leader"));
    }

    [Fact]
    public void An_answer_already_accepted_is_refused()
    {
        // Matched the way the checker matches, so a different typing of "second" counts too.
        var page = Render<Admin>();

        AcceptAs(page, "Second!");

        Assert.Contains("already accepted", page.Find(".jv-accept-warning").TextContent);
        Assert.Equal(Place.Answer, Saved().Answer);
        Assert.Contains("second; or first", Listed(page));   // the box stays open to try again
        Assert.NotEmpty(page.FindAll(".jv-accept-box"));
    }

    [Fact]
    public void An_answer_ruled_wrong_is_refused()
    {
        // Accepted answers are checked first, so this would have quietly undone the ruling.
        var page = Render<Admin>();

        AcceptAs(page, "First");

        Assert.Contains("ruled wrong", page.Find(".jv-accept-warning").TextContent);
        Assert.Equal(Place.Answer, Saved().Answer);
        Assert.Equal(Place.NotAccepted, Saved().NotAccepted);
    }

    [Fact]
    public void Cancel_closes_the_box_and_adds_nothing()
    {
        var page = Render<Admin>();

        AcceptAs(page, "just behind the leader", add: false);
        page.Find("button.jv-accept-cancel").Click();

        Assert.Empty(page.FindAll(".jv-accept-box"));
        Assert.Equal(Place.Answer, Saved().Answer);
    }

    [Fact]
    public void Accepting_clears_every_verdict_on_that_question_and_writes_no_verdicts()
    {
        // New accepted answers change the question's fingerprint, so none of Jev's verdicts on it
        // count any more: they leave the list though the new answer passes none of them. The
        // other question's verdict stays, and the verdicts file is left alone (one writer per file).
        WithOtherQuestion();
        var verdictsBefore = _env.ReadDataFile(JudgedAnswerStore.FileName);
        var page = Render<Admin>();
        Assert.Equal(["right behind the leader", "second; or first", "behind the leader", "gamma"], Listed(page));

        AcceptAs(page, "just behind the leader");

        Assert.False(Saved().AcceptsAnswer("behind the leader"));
        Assert.Equal(["gamma"], Listed(page));
        Assert.Contains("Jev's 2 other verdicts on that question no longer count", page.Find(".jv-accepted-note").TextContent);
        Assert.Equal(verdictsBefore, _env.ReadDataFile(JudgedAnswerStore.FileName));
    }

    [Fact]
    public void An_answer_accepted_while_the_question_is_open_in_the_form_survives_saving_it()
    {
        // The form holds its own copy of the question; saving it must not put back the
        // accepted answers it had before.
        var page = Render<Admin>();
        page.FindAll("button.oad-btn-plain").First(b => b.TextContent.Trim() == "Edit").Click();

        AcceptAs(page, "just behind the leader");
        page.Find("button.oad-btn-green").Click();

        Assert.True(Saved().AcceptsAnswer("just behind the leader"));
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
