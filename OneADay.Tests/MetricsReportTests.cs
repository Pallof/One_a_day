using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>The figures admin's Site metrics section shows, worked out from the totals (PRD 16).</summary>
public class MetricsReportTests
{
    private static readonly DateOnly Today = new(2026, 10, 6);

    private readonly MetricsData _data = new();
    private readonly List<DailyRun> _history = [];
    private readonly List<BrainTeaser> _bank = [];

    private BrainTeaser Teaser(string date, string question)
    {
        var teaser = TeaserFactory.On(date, question);
        _bank.Add(teaser);
        return teaser;
    }

    private DayMetrics On(DateOnly day)
    {
        var key = MetricsData.Key(day);
        if (!_data.Days.TryGetValue(key, out var metrics))
        {
            metrics = new DayMetrics();
            _data.Days[key] = metrics;
        }
        return metrics;
    }

    private void Ran(BrainTeaser teaser, DateOnly day, bool recycled = false, int saw = 0)
    {
        _history.Add(new DailyRun { Date = day, TeaserId = teaser.Id, Recycled = recycled });
        if (saw > 0)
        {
            On(day).Puzzles[teaser.Id] = new PuzzleCounts { Saw = saw };
        }
    }

    private MetricsReport Build(MetricsRange range = MetricsRange.AllTime) =>
        MetricsReport.Build(_data, _history, _bank, Today, range);

    [Fact]
    public void Rotation_shares_follow_the_history_and_add_up_to_everything()
    {
        var a = Teaser("2026-09-01", "A");
        var b = Teaser("2026-09-02", "B");
        var c = Teaser("2026-09-03", "C");
        Ran(a, Today.AddDays(-3), saw: 10);
        Ran(b, Today.AddDays(-2), recycled: true, saw: 30);
        Ran(a, Today.AddDays(-1), recycled: true, saw: 20);
        Ran(c, Today, recycled: true, saw: 40);

        var rows = Build().Fairness.ToDictionary(r => r.TeaserId);

        Assert.Equal(new Share(2, 4), rows[a.Id].OfDays);
        Assert.Equal(new Share(30, 100), rows[a.Id].OfViewers);
        Assert.Equal(1, rows[a.Id].RecycledDays);
        Assert.Equal(new Share(1, 4), rows[b.Id].OfDays);
        Assert.Equal(4, rows.Values.Sum(r => r.OfDays.Part));
        Assert.Equal(100, rows.Values.Sum(r => r.OfViewers.Part));
        Assert.Equal(new Share(3, 4), Build().RecycledDays);
    }

    [Fact]
    public void The_longest_unseen_puzzle_comes_first_and_one_never_shown_tops_the_list()
    {
        var recent = Teaser("2026-09-01", "recent");
        var older = Teaser("2026-09-02", "older");
        var never = Teaser("2026-09-03", "never shown");
        Teaser("2026-12-25", "not due yet");   // can't have been drawn, so not listed
        Ran(older, Today.AddDays(-9));
        Ran(recent, Today.AddDays(-1));

        var rows = Build().Fairness;

        Assert.Equal(new[] { never.Id, older.Id, recent.Id }, rows.Select(r => r.TeaserId));
        Assert.Null(rows[0].LastShown);
        Assert.Equal(9, rows[1].DaysSince);
    }

    [Fact]
    public void The_next_day_return_rate_divides_by_the_day_before()
    {
        On(Today.AddDays(-2)).Visitors.New = 10;
        On(Today.AddDays(-1)).Visitors.BackFromYesterday = 4;
        On(Today.AddDays(-1)).Visitors.New = 6;
        On(Today).Visitors.BackFromYesterday = 5;

        var report = Build();
        var byDay = report.Habit.ToDictionary(r => r.Day);

        Assert.Equal(new Share(4, 10), byDay[Today.AddDays(-2)].NextDayReturn);
        Assert.Equal(new Share(5, 10), byDay[Today.AddDays(-1)].NextDayReturn);
        Assert.Null(byDay[Today].NextDayReturn);   // tomorrow hasn't happened
        Assert.Equal(new Share(9, 20), report.NextDayReturn);
    }

    [Fact]
    public void Back_within_a_week_counts_yesterday_and_the_days_before_it_but_not_longer()
    {
        var visitors = On(Today).Visitors;
        visitors.New = 5;
        visitors.BackFromYesterday = 3;
        visitors.BackWithinWeek = 2;
        visitors.BackAfterLonger = 10;

        Assert.Equal(new Share(5, 20), Build().WithinWeek);
    }

    [Fact]
    public void The_range_leaves_out_older_days()
    {
        On(Today.AddDays(-7)).Visitors.New = 100;   // eight days ago counting today
        On(Today.AddDays(-6)).Visitors.New = 3;

        Assert.Equal(3, Build(MetricsRange.LastWeek).VisitorDays);
        Assert.Equal(103, Build(MetricsRange.LastMonth).VisitorDays);
    }

    [Fact]
    public void A_recycled_puzzle_adds_up_across_its_runs()
    {
        var puzzle = Teaser("2026-09-01", "twice");
        On(Today.AddDays(-20)).Puzzles[puzzle.Id] = new PuzzleCounts
        {
            Saw = 10, Tried = 8, Solved = 4, SolvedIn = [2, 1, 0, 0, 1], SolveAttempts = 2 + 2 + 9,
        };
        On(Today).Puzzles[puzzle.Id] = new PuzzleCounts
        {
            Saw = 5, Tried = 4, Solved = 2, SolvedIn = [0, 2, 0, 0, 0], SolveAttempts = 4,
        };

        var row = Assert.Single(Build().Funnel);

        Assert.Equal((15, 12, 6), (row.Saw, row.Tried, row.Solved));
        Assert.Equal(new Share(6, 12), row.SolveRate);
        Assert.Equal(new[] { 2, 3, 0, 0, 1 }, row.SolvedIn);
        Assert.Equal(17.0 / 6, row.AverageAttempts);
    }

    [Theory]
    [InlineData(7, 12, true)]
    [InlineData(19, 19, true)]
    [InlineData(10, 20, false)]
    public void Fewer_than_twenty_browsers_is_too_few_to_judge(int part, int whole, bool tooFew)
    {
        Assert.Equal(tooFew, new Share(part, whole).TooFew);
    }

    [Fact]
    public void The_list_size_is_the_latest_recorded_and_the_changes_add_up()
    {
        On(Today.AddDays(-3)).Subscribers = new SubscriberCounts { Confirmed = 8, Pending = 2, Confirmations = 1 };
        On(Today.AddDays(-1)).Subscribers = new SubscriberCounts { Confirmed = 9, Pending = 0, Confirmations = 2, Unsubscribes = 1 };
        On(Today).Visitors.New = 1;   // today has no list total yet

        var report = Build();

        Assert.Equal((9, 0, Today.AddDays(-1)), report.Subscribers);
        Assert.Equal((3, 1), (report.Confirmations, report.Unsubscribes));
    }
}
