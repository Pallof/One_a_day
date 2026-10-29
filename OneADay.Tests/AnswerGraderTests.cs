using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The order an answer is judged in (PRD 02): the checker, then the author's rulings, then an
/// earlier verdict of Jev's, and only then Jev itself, where the question allows a second opinion.
/// </summary>
public sealed class AnswerGraderTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    private readonly FakeJudge _jev = new();

    private static readonly BrainTeaser Place = TeaserFactory.On("2026-08-12", "You pass the runner in second place. Where are you now?",
        answer: "second; 2nd; 2");
    private static readonly BrainTeaser Speed = TeaserFactory.On("2026-08-13", "What's the average speed?", answer: "48; 48 mph");
    private static readonly BrainTeaser Formula = TeaserFactory.On("2026-08-14", "Make 24 from 5, 5, 5 and 1.", answer: "5*(5-(1/5))");
    private static readonly BrainTeaser Train = TeaserFactory.On("2026-08-15", "How many times faster is the train?",
        answer: "2; 2:1; twice; 2 to 1");
    private static readonly BrainTeaser Hours = TeaserFactory.On("2026-08-16", "How long does it take?", answer: "2.5 hours; two and a half hours");
    private static readonly BrainTeaser Keyboard = TeaserFactory.On("2026-08-17", "What has keys but opens no locks?", answer: "a keyboard; keyboard");

    private AnswerGrader Grader(JevOptions? options = null) =>
        new(_jev, new JudgedAnswerStore(_env), Options.Create(options ?? GradingTestServices.On()), NullLogger<AnswerGrader>.Instance);

    [Fact]
    public async Task What_the_checker_accepts_never_goes_to_Jev()
    {
        Assert.True(await Grader().IsCorrectAsync(Place, "2nd"));
        Assert.Empty(_jev.Asked);
    }

    [Fact]
    public async Task Jev_can_pass_what_the_checker_turned_down_and_the_verdict_is_kept()
    {
        _jev.Chance = _ => 0.95;

        Assert.True(await Grader().IsCorrectAsync(Place, "right behind the leader"));
        Assert.Equal(Place.AcceptedAnswers(), _jev.LastAccepted);

        // Kept on disk, and found again however it's typed: a new grader, as after a restart.
        Assert.True(await Grader().IsCorrectAsync(Place, "Right behind the leader!"));
        Assert.Single(_jev.Asked);
    }

    [Fact]
    public async Task Below_the_bar_is_wrong_and_the_verdict_is_kept_too()
    {
        _jev.Chance = _ => 0.89;
        var grader = Grader(GradingTestServices.On(threshold: 0.9));

        Assert.False(await grader.IsCorrectAsync(Place, "near the front"));
        Assert.False(await grader.IsCorrectAsync(Place, "near the front"));
        Assert.Single(_jev.Asked);
    }

    [Fact]
    public async Task A_kept_verdict_is_judged_against_todays_bar()
    {
        // The chance is kept, not a yes or no, so moving the bar regrades it without asking again.
        _jev.Chance = _ => 0.8;
        Assert.True(await Grader(GradingTestServices.On(threshold: 0.7)).IsCorrectAsync(Place, "right behind the leader"));

        Assert.False(await Grader(GradingTestServices.On(threshold: 0.9)).IsCorrectAsync(Place, "right behind the leader"));
        Assert.True(await Grader(GradingTestServices.On(threshold: 0.75)).IsCorrectAsync(Place, "right behind the leader"));
        Assert.Single(_jev.Asked);
    }

    [Fact]
    public async Task Exactly_the_bar_counts_as_right()
    {
        _jev.Chance = _ => 0.7;

        Assert.True(await Grader(GradingTestServices.On(threshold: 0.7)).IsCorrectAsync(Place, "right behind the leader"));
    }

    [Fact]
    public void The_shipped_bar_sits_between_the_trial_answers_wrong_and_right()
    {
        // 2026-10-28, 47 answers sent twice: no wrong one scored above 0.49 and no right one
        // below 0.73. A bar outside that gap marks a tried answer the wrong way.
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindRepoRoot(), "OneADay", "appsettings.json")));
        var shipped = doc.RootElement.GetProperty(JevOptions.Section).GetProperty("Threshold").GetDouble();

        Assert.InRange(shipped, 0.5, 0.73);
        Assert.Equal(shipped, new JevOptions().Threshold);
    }

    [Fact]
    public async Task No_verdict_is_wrong_and_asked_again_next_time()
    {
        // Jev down or slow. Kept, a "wrong" from an outage would stick to a right answer for good.
        var grader = Grader();

        Assert.False(await grader.IsCorrectAsync(Place, "right behind the leader"));
        Assert.False(await grader.IsCorrectAsync(Place, "right behind the leader"));
        Assert.Equal(2, _jev.Asked.Count);
    }

    [Fact]
    public async Task A_formula_question_never_goes_to_Jev()
    {
        _jev.Chance = _ => 0.99;

        Assert.False(await Grader().IsCorrectAsync(Formula, "five times five minus one"));
        Assert.Empty(_jev.Asked);
    }

    [Theory]
    // A number must be the right number to reach Jev at all...
    [InlineData("about 50", false)]
    [InlineData("fifty, roughly", false)]
    // ...and where every accepted answer is a number, the submission must give one
    [InlineData("the harmonic mean", false)]
    [InlineData("48 on average", true)]
    [InlineData("forty-eight, since the slow half takes longer", true)]
    public async Task On_a_number_question_only_the_right_number_reaches_Jev(string typed, bool asked)
    {
        _jev.Chance = _ => 0.99;

        Assert.Equal(asked, await Grader().IsCorrectAsync(Speed, typed));
        Assert.Equal(asked, _jev.Asked.Count == 1);
    }

    [Theory]
    // Where the accepted answers include a number, wording without one can be asked about,
    // but a wrong number still can't
    [InlineData("right behind the leader", true)]
    [InlineData("1st place", false)]
    public async Task Where_the_answers_include_a_number_a_wrong_number_never_reaches_Jev(string typed, bool asked)
    {
        _jev.Chance = _ => 0.99;

        await Grader().IsCorrectAsync(Place, typed);

        Assert.Equal(asked, _jev.Asked.Count == 1);
    }

    [Fact]
    public async Task Where_every_answer_is_words_a_number_can_still_be_asked_about()
    {
        // No accepted number to hold it to. Jev itself says no: "1st place" scored 0.04 to 0.06
        // in the trial of 2026-10-28.
        var wordsOnly = TeaserFactory.On("2026-08-12", Place.Question, answer: "second; second place");

        await Grader().IsCorrectAsync(wordsOnly, "1st place");

        Assert.Single(_jev.Asked);
    }

    [Fact]
    public async Task A_verdict_on_one_number_is_never_reused_for_another()
    {
        // PR 74 review: "2-1" and "21" once shared a key, so Jev's yes to one passed the other.
        _jev.Chance = typed => typed is "2-1" or "2.5 hrs" ? 0.95 : 0.05;
        var grader = Grader();

        Assert.True(await grader.IsCorrectAsync(Train, "2-1"));
        Assert.True(await grader.IsCorrectAsync(Hours, "2.5 hrs"));

        Assert.False(await grader.IsCorrectAsync(Train, "21"));
        Assert.False(await grader.IsCorrectAsync(Hours, "25 hrs"));
        Assert.Equal(["2-1", "2.5 hrs"], _jev.Asked);
    }

    [Fact]
    public async Task A_kept_verdict_is_reused_only_where_Jev_could_be_asked()
    {
        // As if kept under a key that let "21" share "2-1"'s verdict, the way they once did.
        _env.WriteDataFile(JudgedAnswerStore.FileName, JsonSerializer.Serialize(new[]
        {
            new JudgedAnswer { TeaserId = Train.Id, Submission = "21", Chance = 0.95, Against = Train.Fingerprint() },
        }));

        Assert.False(await Grader().IsCorrectAsync(Train, "21"));
        Assert.Empty(_jev.Asked);
    }

    [Theory]
    [InlineData("⌨️")]
    [InlineData("?")]
    [InlineData("...")]
    public async Task An_answer_with_no_letters_or_digits_is_wrong_and_never_sent(string typed)
    {
        // Each would have had the same empty key, so one yes would have passed them all.
        _jev.Chance = _ => 0.99;

        Assert.False(await Grader().IsCorrectAsync(Keyboard, typed));
        Assert.Empty(_jev.Asked);
    }

    [Fact]
    public async Task Switched_off_Jevs_earlier_verdicts_go_too()
    {
        // Removing the key is the emergency switch, so it must stop the kept yeses as well.
        _jev.Chance = _ => 0.95;
        Assert.True(await Grader().IsCorrectAsync(Place, "right behind the leader"));

        Assert.False(await Grader(new JevOptions { Enabled = false, ApiKey = "test-key-not-real" }).IsCorrectAsync(Place, "right behind the leader"));
        Assert.False(await Grader(new JevOptions { Enabled = true, ApiKey = "" }).IsCorrectAsync(Place, "right behind the leader"));
    }

    [Fact]
    public async Task Editing_the_question_or_its_answers_means_Jev_is_asked_again()
    {
        var before = TeaserFactory.On("2026-08-12", Place.Question, answer: "third; 3rd");   // a mistake
        _jev.Chance = _ => 0.95;
        Assert.True(await Grader().IsCorrectAsync(before, "in front of the runner I passed"));

        var fixedUp = TeaserFactory.On("2026-08-12", Place.Question, answer: Place.Answer);
        fixedUp.Id = before.Id;
        _jev.Chance = _ => 0.05;

        Assert.False(await Grader().IsCorrectAsync(fixedUp, "in front of the runner I passed"));
        Assert.False(await Grader().IsCorrectAsync(fixedUp, "in front of the runner I passed"));   // the new verdict is kept
        Assert.Equal(2, _jev.Asked.Count);
    }

    [Fact]
    public async Task Editing_the_solution_or_picture_keeps_the_verdicts()
    {
        _jev.Chance = _ => 0.95;
        var teaser = TeaserFactory.On("2026-08-12", Place.Question, answer: Place.Answer);
        Assert.True(await Grader().IsCorrectAsync(teaser, "right behind the leader"));

        teaser.Solution = "You take their place, not the leader's.";
        teaser.ImageFileName = "race.png";
        teaser.NotAccepted = "first";

        Assert.True(await Grader().IsCorrectAsync(teaser, "right behind the leader"));
        Assert.Single(_jev.Asked);
    }

    [Fact]
    public async Task Each_address_has_its_own_share_of_the_day()
    {
        _jev.Chance = _ => 0.95;
        var grader = Grader(GradingTestServices.On(maxPerDay: 10, maxPerAddressPerDay: 2));

        Assert.True(await grader.IsCorrectAsync(Place, "right behind the leader", "203.0.113.7"));
        Assert.True(await grader.IsCorrectAsync(Place, "just behind first", "203.0.113.7"));
        Assert.False(await grader.IsCorrectAsync(Place, "behind the front runner", "203.0.113.7"));

        // Someone else's share is untouched, and the same answer from them is now kept
        Assert.True(await grader.IsCorrectAsync(Place, "behind the front runner", "198.51.100.4"));
        Assert.True(await grader.IsCorrectAsync(Place, "behind the front runner", "203.0.113.7"));
        Assert.Equal(3, _jev.Asked.Count);
    }

    [Fact]
    public async Task A_kept_verdict_spends_none_of_the_share()
    {
        _jev.Chance = _ => 0.95;
        var grader = Grader(GradingTestServices.On(maxPerAddressPerDay: 2));

        await grader.IsCorrectAsync(Place, "right behind the leader", "203.0.113.7");
        await grader.IsCorrectAsync(Place, "right behind the leader", "203.0.113.7");
        await grader.IsCorrectAsync(Place, "Right behind the leader!", "203.0.113.7");

        Assert.True(await grader.IsCorrectAsync(Place, "just behind first", "203.0.113.7"));
        Assert.Equal(2, _jev.Asked.Count);
    }

    [Fact]
    public async Task The_authors_ruling_beats_an_earlier_yes_from_Jev()
    {
        _jev.Chance = _ => 0.95;
        var teaser = TeaserFactory.On("2026-08-12", Place.Question, answer: Place.Answer);
        Assert.True(await Grader().IsCorrectAsync(teaser, "right behind the leader"));

        teaser.NotAccepted = "right behind the leader";

        Assert.False(await Grader().IsCorrectAsync(teaser, "Right behind the leader"));
        Assert.Single(_jev.Asked);
    }

    [Fact]
    public async Task Past_the_daily_cap_the_checker_alone_decides()
    {
        _jev.Chance = _ => 0.95;
        var grader = Grader(GradingTestServices.On(maxPerDay: 2));

        Assert.True(await grader.IsCorrectAsync(Place, "right behind the leader"));
        Assert.True(await grader.IsCorrectAsync(Place, "just behind first"));
        Assert.False(await grader.IsCorrectAsync(Place, "behind the front runner"));
        Assert.Equal(2, _jev.Asked.Count);
    }

    [Theory]
    [InlineData(true, "")]                  // on, but no key
    [InlineData(false, "test-key-not-real")] // a key, but switched off
    public async Task Without_both_a_key_and_the_switch_Jev_is_never_asked(bool enabled, string key)
    {
        _jev.Chance = _ => 0.99;

        Assert.False(await Grader(new JevOptions { Enabled = enabled, ApiKey = key }).IsCorrectAsync(Place, "right behind the leader"));
        Assert.Empty(_jev.Asked);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, ".gitignore")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Couldn't find the repo root.");
    }

    public void Dispose() => _env.Dispose();
}
