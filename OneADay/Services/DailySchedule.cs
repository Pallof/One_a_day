using OneADay.Models;

namespace OneADay.Services;

/// <summary>
/// Decides which teaser runs on a given day, and remembers the answer (PRD 08).
///
/// New teasers always win: while any is waiting, each day takes the next one, earliest date
/// first, from the day after the site first had it. Only when none is waiting does the
/// recycling box get opened, and the first draw after a run of new teasers reshuffles it.
/// A day once settled never changes, so an upload can't swap the question mid-day.
/// </summary>
public class DailySchedule(TeaserStore teasers, RotationStore rotation, Func<DateOnly>? today = null)
{
    private readonly Random _random = new();
    private readonly Func<DateOnly> _today = today ?? (() => AppTime.Today);

    /// <summary>How far ahead <see cref="Upcoming"/> looks for a pinned teaser.</summary>
    public const int UpcomingDays = 400;

    /// <summary>
    /// The teaser for <paramref name="date"/>, resolving and recording it if today is
    /// the first time anyone has asked. Safe to call on every page load.
    /// </summary>
    public BrainTeaser? ForDay(DateOnly date)
    {
        var all = teasers.GetAll();
        var arrived = rotation.NoteArrivals(all.Select(t => t.Id), _today());

        // 1. Already settled? Keep it, whatever has arrived since. A day's challenge must never
        //    change under a solver, and the 7am email has already gone out with it.
        var settled = rotation.RunFor(date);
        if (settled is not null)
        {
            var known = teasers.GetById(settled.TeaserId);
            if (known is not null)
            {
                return known;
            }
            // The teaser was deleted since; fall through and draw a replacement.
        }

        // 2. A pinned teaser on its day, or else the next new one.
        var shown = rotation.ShownIds();
        var next = NextNew(date, all, shown, arrived);
        if (next is not null)
        {
            var run = rotation.RecordScheduled(date, next.Id);
            return teasers.GetById(run.TeaserId);
        }

        // 3. Otherwise recycle, starting a fresh box if new teasers have run since the last draw.
        var drawn = rotation.DrawFor(date, Bank(date, all, shown), _random,
                                     freshBox: rotation.NewRunsSinceLastDraw());
        return drawn is null ? null : teasers.GetById(drawn.TeaserId);
    }

    /// <summary>The teasers the box would draw from on <paramref name="date"/>.</summary>
    public IReadOnlyList<Guid> BankOn(DateOnly date) => Bank(date, teasers.GetAll(), rotation.ShownIds());

    /// <summary>
    /// The pinned and new teasers still to come, each with the day it would run if nothing
    /// else arrives, from <paramref name="from"/> on. Days already settled are skipped, and
    /// days in between are recycled. A pin more than <see cref="UpcomingDays"/> ahead is left off.
    /// </summary>
    public IReadOnlyList<(DateOnly Day, BrainTeaser Teaser)> Upcoming(DateOnly from)
    {
        var all = teasers.GetAll();
        var arrived = rotation.NoteArrivals(all.Select(t => t.Id), _today());
        var shown = new HashSet<Guid>(rotation.ShownIds());
        var lastPin = all.Where(t => t.Pinned).Select(t => t.Date).DefaultIfEmpty(from).Max();

        var plan = new List<(DateOnly, BrainTeaser)>();
        for (var day = from; day < from.AddDays(UpcomingDays); day = day.AddDays(1))
        {
            if (day > lastPin && all.All(t => shown.Contains(t.Id)))
            {
                break;
            }
            if (rotation.RunFor(day) is { } settled)
            {
                shown.Add(settled.TeaserId);
                continue;
            }
            if (NextNew(day, all, shown, arrived) is { } next)
            {
                plan.Add((day, next));
                shown.Add(next.Id);
            }
        }
        return plan;
    }

    /// <summary>
    /// What runs on <paramref name="date"/> ahead of the box, if anything: a teaser pinned to
    /// that day, or else the waiting new teaser with the earliest date. A gap in the dates, or
    /// a date already gone, just closes up.
    /// </summary>
    private static BrainTeaser? NextNew(
        DateOnly date,
        IReadOnlyList<BrainTeaser> all,
        IReadOnlySet<Guid> shown,
        IReadOnlyDictionary<Guid, DateOnly> arrived)
    {
        // Strictly from the day after the site first had it (author's decision, 2026-11-04).
        bool HereBefore(BrainTeaser t) => arrived.GetValueOrDefault(t.Id, date) < date;

        var pinned = all.FirstOrDefault(t => t.Pinned && t.Date == date && HereBefore(t));
        if (pinned is not null)
        {
            return pinned;
        }

        // OrderBy is stable, so two teasers on one date keep their order in the bank.
        return all
            .Where(t => !shown.Contains(t.Id) && HereBefore(t) && !HeldForItsDay(t, date))
            .OrderBy(t => t.Date)
            .FirstOrDefault();
    }

    /// <summary>
    /// The box holds teasers that have run before. A new one waits its turn in the queue
    /// instead, and a pinned one stays out until its day.
    /// </summary>
    private static List<Guid> Bank(DateOnly date, IReadOnlyList<BrainTeaser> all, IReadOnlySet<Guid> shown) =>
        all.Where(t => shown.Contains(t.Id) && !HeldForItsDay(t, date)).Select(t => t.Id).ToList();

    private static bool HeldForItsDay(BrainTeaser teaser, DateOnly date) => teaser.Pinned && teaser.Date > date;

    /// <summary>
    /// The challenge from the day before <paramref name="date"/> — the source for
    /// "yesterday's solution".
    /// </summary>
    /// <remarks>
    /// Resolved through the same path as any other day, so it follows the recycling
    /// rotation rather than the hand-written schedule. An earlier version read the
    /// schedule when history was thin, which anchored the page to whichever teasers
    /// had been typed in most recently — so it barely changed between new uploads,
    /// exactly the opposite of the daily churn the rotation exists to provide.
    ///
    /// Today is settled first so the outcome does not depend on which page a visitor
    /// happens to open. That ordering also guarantees yesterday's draw sees today's in
    /// the cooldown, so the two can never be the same teaser.
    /// </remarks>
    public BrainTeaser? PreviousBefore(DateOnly date)
    {
        var today = ForDay(date);
        var yesterday = ForDay(date.AddDays(-1));

        // Never publish the answer to the challenge people are solving right now.
        // The cooldown normally keeps the two days apart, but a bank too small for it
        // to bite (one or two teasers) can land the same one on both days.
        if (yesterday is null || (today is not null && yesterday.Id == today.Id))
        {
            return null;
        }
        return yesterday;
    }
}
