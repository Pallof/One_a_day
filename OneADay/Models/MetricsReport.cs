using System.Globalization;
using OneADay.Services;

namespace OneADay.Models;

public enum MetricsRange
{
    LastWeek,
    LastMonth,
    AllTime,
}

/// <summary>
/// A percentage that always carries its count. Below <see cref="TooFewBelow"/> it's marked too
/// few to judge: at that size one browser moves it by five points (PRD 16, requirement 17).
/// </summary>
public sealed record Share(int Part, int Whole)
{
    public const int TooFewBelow = 20;

    public double? Percent => Whole == 0 ? null : 100.0 * Part / Whole;
    public bool TooFew => Whole < TooFewBelow;
}

/// <summary>One puzzle's place in the rotation over the range.</summary>
public sealed record FairnessRow(
    Guid TeaserId,
    string Question,
    int DaysShown,
    int RecycledDays,
    Share OfDays,
    Share OfViewers,
    DateOnly? LastShown,
    int? DaysSince);

/// <summary>One puzzle's saw → tried → solved over the range.</summary>
public sealed record FunnelRow(
    Guid TeaserId,
    string Question,
    int Saw,
    int Tried,
    int Solved,
    Share SolveRate,
    IReadOnlyList<int> SolvedIn,
    double? AverageAttempts);

/// <summary>One day's visitors.</summary>
/// <param name="NextDayReturn">Of this day's visitors, the share back the next day. Null for today.</param>
/// <param name="WithinWeek">Of this day's visitors, the share who had visited in the previous 7 days.</param>
public sealed record HabitRow(DateOnly Day, VisitorCounts Visitors, Share? NextDayReturn, Share WithinWeek);

/// <summary>
/// Admin's Metrics section, worked out from the downloaded totals and the rotation history.
/// Pure: nothing here reads a file or the clock, so every figure is tested directly.
/// </summary>
public sealed class MetricsReport
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public DateTime? SavedAtUtc { get; init; }

    public required IReadOnlyList<FairnessRow> Fairness { get; init; }
    public required Share RecycledDays { get; init; }

    public required IReadOnlyList<FunnelRow> Funnel { get; init; }

    public required IReadOnlyList<HabitRow> Habit { get; init; }
    public required int VisitorDays { get; init; }
    public required Share NextDayReturn { get; init; }
    public required Share WithinWeek { get; init; }

    /// <summary>The list's size at the latest day it was recorded, if ever.</summary>
    public (int Confirmed, int Pending, DateOnly Day)? Subscribers { get; init; }
    public required int Confirmations { get; init; }
    public required int Unsubscribes { get; init; }

    public required int TwentyFourPlayers { get; init; }
    public required int HandsSolved { get; init; }
    public required int HandsPassed { get; init; }

    public bool IsEmpty => SavedAtUtc is null && Habit.Count == 0;

    public static MetricsReport Build(
        MetricsData data,
        IReadOnlyList<DailyRun> history,
        IReadOnlyList<BrainTeaser> teasers,
        DateOnly today,
        MetricsRange range)
    {
        var days = data.Days
            .Select(d => (Parsed: DateOnly.TryParseExact(d.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out var day), Day: day, Metrics: d.Value))
            .Where(d => d.Parsed && d.Day <= today)   // a hand-edited key mustn't break admin
            .Select(d => (d.Day, d.Metrics))
            .ToList();

        var from = range switch
        {
            MetricsRange.LastWeek => today.AddDays(-6),
            MetricsRange.LastMonth => today.AddDays(-29),
            _ => days.Select(d => d.Day)
                     .Concat(history.Select(r => r.Date))
                     .DefaultIfEmpty(today)
                     .Min(),
        };

        bool InRange(DateOnly day) => day >= from && day <= today;

        var inRange = days.Where(d => InRange(d.Day)).OrderBy(d => d.Day).ToList();
        var byDay = days.ToDictionary(d => d.Day, d => d.Metrics);
        var runs = history.Where(r => InRange(r.Date)).ToList();
        var known = teasers.ToDictionary(t => t.Id);

        // ---- rotation fairness ------------------------------------------------------
        var sawBy = inRange
            .SelectMany(d => d.Metrics.Puzzles)
            .GroupBy(p => p.Key)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Value.Saw));
        var totalSaw = sawBy.Values.Sum();

        var lastShown = history
            .Where(r => r.Date <= today)
            .GroupBy(r => r.TeaserId)
            .ToDictionary(g => g.Key, g => g.Max(r => r.Date));

        var fairness = teasers
            .Where(t => t.Date <= today)   // a teaser not yet due can't have been drawn
            .Select(t =>
            {
                var shown = runs.Where(r => r.TeaserId == t.Id).ToList();
                DateOnly? last = lastShown.TryGetValue(t.Id, out var l) ? l : null;
                return new FairnessRow(
                    t.Id,
                    t.Question,
                    DaysShown: shown.Count,
                    RecycledDays: shown.Count(r => r.Recycled),
                    OfDays: new Share(shown.Count, runs.Count),
                    OfViewers: new Share(sawBy.GetValueOrDefault(t.Id), totalSaw),
                    LastShown: last,
                    DaysSince: last is { } shownOn ? today.DayNumber - shownOn.DayNumber : null);
            })
            .OrderByDescending(r => r.DaysSince ?? int.MaxValue)   // longest unseen first
            .ToList();

        // ---- the funnel per puzzle --------------------------------------------------
        var funnel = inRange
            .SelectMany(d => d.Metrics.Puzzles.Select(p => (d.Day, Id: p.Key, Counts: p.Value)))
            .GroupBy(p => p.Id)
            .Select(g =>
            {
                var solvedIn = new int[MetricsStore.AttemptBuckets];
                foreach (var (_, _, counts) in g)
                {
                    for (var i = 0; i < solvedIn.Length && i < counts.SolvedIn.Length; i++)
                    {
                        solvedIn[i] += counts.SolvedIn[i];
                    }
                }
                var solved = g.Sum(p => p.Counts.Solved);
                var tried = g.Sum(p => p.Counts.Tried);
                var attempts = g.Sum(p => p.Counts.SolveAttempts);
                return (
                    LastDay: g.Max(p => p.Day),
                    Row: new FunnelRow(
                        g.Key,
                        known.TryGetValue(g.Key, out var t) ? t.Question : "(deleted)",
                        Saw: g.Sum(p => p.Counts.Saw),
                        Tried: tried,
                        Solved: solved,
                        SolveRate: new Share(solved, tried),
                        SolvedIn: solvedIn,
                        AverageAttempts: solved == 0 ? null : (double)attempts / solved));
            })
            .OrderByDescending(x => x.LastDay)
            .Select(x => x.Row)
            .ToList();

        // ---- the habit --------------------------------------------------------------
        Share? NextDay(DateOnly day, int visitors)
        {
            if (day >= today)
            {
                return null;   // tomorrow hasn't happened
            }
            var back = byDay.TryGetValue(day.AddDays(1), out var next) ? next.Visitors.BackFromYesterday : 0;
            return new Share(back, visitors);
        }

        var habit = inRange
            .Select(d => new HabitRow(
                d.Day,
                d.Metrics.Visitors,
                NextDay(d.Day, d.Metrics.Visitors.Total),
                new Share(d.Metrics.Visitors.BackFromYesterday + d.Metrics.Visitors.BackWithinWeek,
                          d.Metrics.Visitors.Total)))
            .OrderByDescending(r => r.Day)
            .ToList();

        var settled = habit.Where(r => r.NextDayReturn is not null).ToList();

        // ---- subscribers ------------------------------------------------------------
        var latestList = days
            .Where(d => d.Metrics.Subscribers.Confirmed is not null)
            .OrderByDescending(d => d.Day)
            .Select(d => ((int Confirmed, int Pending, DateOnly Day)?)
                (d.Metrics.Subscribers.Confirmed!.Value, d.Metrics.Subscribers.Pending ?? 0, d.Day))
            .FirstOrDefault();

        return new MetricsReport
        {
            From = from,
            To = today,
            SavedAtUtc = data.SavedAtUtc,
            Fairness = fairness,
            RecycledDays = new Share(runs.Count(r => r.Recycled), runs.Count),
            Funnel = funnel,
            Habit = habit,
            VisitorDays = habit.Sum(r => r.Visitors.Total),
            NextDayReturn = new Share(settled.Sum(r => r.NextDayReturn!.Part), settled.Sum(r => r.NextDayReturn!.Whole)),
            WithinWeek = new Share(habit.Sum(r => r.WithinWeek.Part), habit.Sum(r => r.WithinWeek.Whole)),
            Subscribers = latestList,
            Confirmations = inRange.Sum(d => d.Metrics.Subscribers.Confirmations),
            Unsubscribes = inRange.Sum(d => d.Metrics.Subscribers.Unsubscribes),
            TwentyFourPlayers = inRange.Sum(d => d.Metrics.TwentyFour.Players),
            HandsSolved = inRange.Sum(d => d.Metrics.TwentyFour.HandsSolved),
            HandsPassed = inRange.Sum(d => d.Metrics.TwentyFour.HandsPassed),
        };
    }
}
