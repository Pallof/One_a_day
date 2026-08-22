using OneADay.Models;

namespace OneADay.Services;

/// <summary>
/// Decides which teaser runs on a given day, and remembers the answer.
///
/// New teasers always win: a teaser scheduled for a date runs on that date. Only when
/// the queue has nothing for today does the recycling box get opened, so writing new
/// content is never undercut by the rotation.
/// </summary>
public class DailySchedule(TeaserStore teasers, RotationStore rotation)
{
    private readonly Random _random = new();

    /// <summary>
    /// The teaser for <paramref name="date"/>, resolving and recording it if today is
    /// the first time anyone has asked. Safe to call on every page load.
    /// </summary>
    public BrainTeaser? ForDay(DateOnly date)
    {
        // 1. Newly written content for this exact day always takes precedence.
        var scheduled = teasers.GetForDate(date);
        if (scheduled is not null)
        {
            rotation.RecordScheduled(date, scheduled.Id);
            return scheduled;
        }

        // 2. Already settled? Keep it — a day's challenge must never change.
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

        // 3. Otherwise recycle. Only released teasers are eligible — drawing a
        //    future-dated one early would spoil the queue.
        var bank = teasers.ReleasedOn(date).Select(t => t.Id).ToList();
        var run = rotation.DrawFor(date, bank, _random);
        return run is null ? null : teasers.GetById(run.TeaserId);
    }

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
