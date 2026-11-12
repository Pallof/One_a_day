using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// Which question runs on a day (PRD 08): a settled day keeps its question, new questions go
/// first in date order from the day after they arrive, a pinned one takes exactly its day, and
/// the box is reshuffled once the new ones have run (author's decisions, 2026-11-04).
/// </summary>
public sealed class DailyScheduleTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 4);

    private readonly TestEnvironment _env = new();
    private DateOnly _clock = Today;
    private TeaserStore _store = null!;
    private RotationStore _rotation = null!;
    private DailySchedule _schedule = null!;

    public void Dispose() => _env.Dispose();

    private static BrainTeaser Q(string question, DateOnly date, bool pinned = false) => new()
    {
        Date = date,
        Question = question,
        Answer = "a",
        Pinned = pinned,
    };

    /// <summary>Starts the site, or restarts it as an upload does, on what's in App_Data.</summary>
    private void Start()
    {
        _store = _env.NewTeaserStore();
        _rotation = _env.NewRotationStore();
        _schedule = new DailySchedule(_store, _rotation, () => _clock);
    }

    /// <summary>
    /// A bank of <paramref name="count"/> questions that have each run once, on the days before
    /// today. It was all here before arrivals were kept, as on the first run after this change,
    /// so it counts as long arrived.
    /// </summary>
    private void BankThatHasRun(int count)
    {
        _env.SeedTeasers(Enumerable.Range(1, count)
            .Select(i => Q($"old {i}", new DateOnly(2026, 7, 1).AddDays(i)))
            .ToArray());
        Start();
        for (var i = count; i >= 1; i--)
        {
            _schedule.ForDay(Today.AddDays(-i));
        }
    }

    /// <summary><paramref name="day"/> comes round, and someone opens the site.</summary>
    private BrainTeaser Day(DateOnly day)
    {
        _clock = day;
        return _schedule.ForDay(day)!;
    }

    private BrainTeaser Named(string question) => _store.GetAll().Single(t => t.Question == question);

    // ---- a settled day ---------------------------------------------------------

    [Fact]
    public void A_settled_day_keeps_its_question_when_one_dated_or_pinned_to_that_day_arrives()
    {
        // On 2026-11-04 new questions were first dated today, which would have swapped the
        // live question mid-day, after the 7am email had gone out with the old one.
        BankThatHasRun(5);
        var running = _schedule.ForDay(Today)!;

        _store.Save(Q("dated today", Today));
        var edited = Named("old 2");
        edited.Pinned = true;
        edited.Date = Today;
        _store.Save(edited);
        Start();   // the upload restarts the site

        Assert.Equal(running.Id, _schedule.ForDay(Today)!.Id);
    }

    [Fact]
    public void Two_requests_settling_one_day_get_the_same_question()
    {
        // Two page loads can both work out the day before either records it. The second must
        // get the first one's question back, not its own.
        var rotation = _env.NewRotationStore();
        var first = Guid.NewGuid();

        rotation.RecordScheduled(Today, first);
        var second = rotation.RecordScheduled(Today, Guid.NewGuid());

        Assert.Equal(first, second.TeaserId);
        Assert.Equal(first, rotation.RunFor(Today)!.TeaserId);
    }

    // ---- new questions -----------------------------------------------------------

    [Fact]
    public void A_new_question_waits_for_the_day_after_it_arrives_even_if_today_is_unsettled()
    {
        BankThatHasRun(5);
        _store.Save(Q("new", Today));   // nobody has opened the site yet today

        var today = _schedule.ForDay(Today)!;
        var tomorrow = Day(Today.AddDays(1));

        Assert.StartsWith("old", today.Question);
        Assert.Equal("new", tomorrow.Question);
    }

    [Fact]
    public void New_questions_run_before_any_repeat_earliest_date_first_closing_up_gaps()
    {
        BankThatHasRun(5);
        _store.Save(Q("dated a week ahead", Today.AddDays(7)));
        _store.Save(Q("dated tomorrow", Today.AddDays(1)));
        _store.Save(Q("dated last month", Today.AddDays(-30)));
        _schedule.ForDay(Today);   // today settles, and the site has the three

        Assert.Equal(
            new[] { "dated last month", "dated tomorrow", "dated a week ahead" },
            Enumerable.Range(1, 3).Select(i => Day(Today.AddDays(i)).Question));
        Day(Today.AddDays(4));
        Assert.True(_rotation.RunFor(Today.AddDays(4))!.Recycled);   // then the box again
    }

    [Fact]
    public void Questions_in_the_bank_before_arrivals_were_kept_can_run_at_once()
    {
        // The questions dated 5 to 14 November 2026 were uploaded before this change. On its
        // first run they count as long arrived, so 5 November still gets its new question.
        var old = Q("ran in October", new DateOnly(2026, 7, 1));
        var waiting = Q("dated today", Today);
        _env.SeedTeasers(old, waiting);
        _env.WriteDataFile("rotation.json", $$"""
            {
              "Box": [],
              "History": [ { "Date": "2026-10-01", "TeaserId": "{{old.Id}}", "Recycled": true } ],
              "ShowCounts": { "{{old.Id}}": 1 }
            }
            """);
        Start();

        Assert.Equal("dated today", _schedule.ForDay(Today)!.Question);
    }

    [Fact]
    public void The_day_a_question_arrived_is_kept_across_a_restart()
    {
        // Every upload restarts the site. A question that arrived yesterday must still count as
        // yesterday's after the next restart, not start waiting all over again.
        BankThatHasRun(5);
        _schedule.ForDay(Today);
        _store.Save(Q("new", Today.AddDays(5)));
        _schedule.ForDay(Today);   // the site has it today

        _clock = Today.AddDays(1);
        Start();   // the next upload restarts the site tomorrow

        Assert.Equal("new", _schedule.ForDay(Today.AddDays(1))!.Question);
    }

    // ---- pinned questions --------------------------------------------------------

    [Fact]
    public void A_pinned_question_takes_its_day_and_the_queue_picks_up_after()
    {
        BankThatHasRun(5);
        _store.Save(Q("new A", Today.AddDays(-1)));
        _store.Save(Q("new B", Today.AddDays(1)));
        _store.Save(Q("pinned", Today.AddDays(2), pinned: true));
        _schedule.ForDay(Today);

        Assert.Equal(
            new[] { "new A", "pinned", "new B" },
            Enumerable.Range(1, 3).Select(i => Day(Today.AddDays(i)).Question));
    }

    [Fact]
    public void A_pinned_question_never_runs_before_its_day()
    {
        BankThatHasRun(5);
        _store.Save(Q("pinned", Today.AddDays(3), pinned: true));
        _schedule.ForDay(Today);

        var days = Enumerable.Range(1, 3).Select(i => Day(Today.AddDays(i)).Question).ToList();

        Assert.All(days.Take(2), q => Assert.StartsWith("old", q));
        Assert.Equal("pinned", days[2]);
    }

    [Fact]
    public void A_question_that_has_run_stays_out_of_the_box_until_the_day_its_pinned_to()
    {
        BankThatHasRun(5);
        var pinned = Named("old 1");
        pinned.Pinned = true;
        pinned.Date = Today.AddDays(30);
        _store.Save(pinned);

        var drawn = Enumerable.Range(0, 30).Select(i => Day(Today.AddDays(i)).Id).ToList();

        Assert.DoesNotContain(pinned.Id, drawn);
        Assert.Equal(pinned.Id, Day(Today.AddDays(30)).Id);
    }

    [Fact]
    public void A_pinned_question_that_arrives_on_its_day_runs_the_next_day()
    {
        // Today is already settled on the live site by the time an upload lands, so its pin is
        // missed. It runs next, ahead of the queue, being the earliest date waiting.
        BankThatHasRun(5);
        _store.Save(Q("new, dated tomorrow", Today.AddDays(1)));
        _store.Save(Q("pinned to today", Today, pinned: true));

        Assert.StartsWith("old", _schedule.ForDay(Today)!.Question);
        Assert.Equal("pinned to today", Day(Today.AddDays(1)).Question);
        Assert.Equal("new, dated tomorrow", Day(Today.AddDays(2)).Question);
    }

    // ---- the reshuffle -------------------------------------------------------------

    /// <summary>
    /// Ten old questions, three days into a cycle of the box, then two new questions arrive
    /// and run on the next two days.
    /// </summary>
    private void TwoNewQuestionsRunMidCycle()
    {
        BankThatHasRun(10);
        for (var i = 0; i <= 2; i++)
        {
            Day(Today.AddDays(i));
        }
        Assert.Equal(7, _rotation.Snapshot().Remaining);   // 10, less three draws

        _store.Save(Q("new 1", Today.AddDays(20)));
        _store.Save(Q("new 2", Today.AddDays(21)));
        _schedule.ForDay(Today.AddDays(2));   // the site has them on day 2
        Assert.Equal("new 1", Day(Today.AddDays(3)).Question);
        Assert.Equal("new 2", Day(Today.AddDays(4)).Question);
    }

    [Fact]
    public void Once_the_new_questions_have_run_the_box_is_reshuffled_with_every_question()
    {
        TwoNewQuestionsRunMidCycle();

        Day(Today.AddDays(5));

        // All 12, the two that just ran included, went into a fresh box and one came out.
        // Carrying on with the old box would leave 6.
        Assert.Equal(11, _rotation.Snapshot().Remaining);
    }

    [Fact]
    public void The_box_is_reshuffled_once_not_on_every_day_after()
    {
        TwoNewQuestionsRunMidCycle();

        Day(Today.AddDays(5));
        Day(Today.AddDays(6));

        Assert.Equal(10, _rotation.Snapshot().Remaining);
    }

    [Fact]
    public void A_pinned_question_that_has_run_before_doesnt_reshuffle_the_box()
    {
        // Only questions running for the first time do. A pin coming round again leaves the
        // cycle as it was.
        BankThatHasRun(10);
        Day(Today);
        Day(Today.AddDays(1));
        var pinned = Named("old 3");
        pinned.Pinned = true;
        pinned.Date = Today.AddDays(2);
        _store.Save(pinned);

        Assert.Equal(pinned.Id, Day(Today.AddDays(2)).Id);
        Day(Today.AddDays(3));

        Assert.Equal(7, _rotation.Snapshot().Remaining);   // 10, less three draws
    }

    // ---- yesterday's solution ------------------------------------------------------

    [Fact]
    public void Yesterday_is_what_ran_yesterday()
    {
        BankThatHasRun(5);
        var ranYesterday = _rotation.RunFor(Today.AddDays(-1))!.TeaserId;

        Assert.Equal(ranYesterday, _schedule.PreviousBefore(Today)!.Id);
    }

    [Fact]
    public void A_yesterday_nobody_opened_the_site_on_has_no_solution_and_spends_no_new_question()
    {
        // A whole day with no visit, an outage say, never had a challenge. Working one out
        // afterwards took the next new question: its solution published before it ever ran.
        _env.SeedTeasers(Enumerable.Range(1, 5)
            .Select(i => Q($"old {i}", new DateOnly(2026, 7, 1).AddDays(i)))
            .ToArray());
        Start();
        for (var i = 6; i >= 2; i--)
        {
            Day(Today.AddDays(-i));
        }
        _store.Save(Q("new 1", Today.AddDays(1)));
        _store.Save(Q("new 2", Today.AddDays(2)));
        _schedule.ForDay(Today.AddDays(-2));   // the site has them two days ago
        _clock = Today;                        // and then nothing, all of yesterday

        Assert.Null(_schedule.PreviousBefore(Today));
        Assert.Null(_rotation.RunFor(Today.AddDays(-1)));
        Assert.Equal("new 1", _schedule.ForDay(Today)!.Question);
        Assert.Equal("new 2", Day(Today.AddDays(1)).Question);
    }

    // ---- what admin shows ----------------------------------------------------------

    [Fact]
    public void Upcoming_shows_each_waiting_question_on_the_day_it_will_run_and_settles_nothing()
    {
        BankThatHasRun(5);
        _schedule.ForDay(Today);
        _store.Save(Q("new A", Today.AddDays(-1)));
        _store.Save(Q("new B", Today.AddDays(1)));
        _store.Save(Q("pinned", Today.AddDays(2), pinned: true));
        _store.Save(Q("pinned well ahead", Today.AddDays(10), pinned: true));
        var settledDays = _rotation.Snapshot(int.MaxValue).Recent.Count;

        var upcoming = _schedule.Upcoming(Today.AddDays(1));

        Assert.Equal(
            new[]
            {
                (Today.AddDays(1), "new A"),
                (Today.AddDays(2), "pinned"),
                (Today.AddDays(3), "new B"),
                (Today.AddDays(10), "pinned well ahead"),
            },
            upcoming.Select(u => (u.Day, u.Teaser.Question)));
        Assert.Equal(settledDays, _rotation.Snapshot(int.MaxValue).Recent.Count);

        // And it is what the site then does.
        foreach (var (day, teaser) in upcoming)
        {
            Assert.Equal(teaser.Id, Day(day).Id);
        }
    }
}
