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
    /// What ran on the most recent day before <paramref name="date"/> — the source for
    /// "yesterday's solution". Reads history rather than dates, because with recycling
    /// the teaser that ran yesterday is not simply the one dated yesterday.
    /// </summary>
    public BrainTeaser? PreviousBefore(DateOnly date)
    {
        var run = rotation.MostRecentBefore(date);
        if (run is not null)
        {
            var known = teasers.GetById(run.TeaserId);
            if (known is not null)
            {
                return known;
            }
        }
        // No recorded history yet (a fresh install, or days nobody visited). Fall back
        // to the schedule — but relative to whatever is running today, never to the
        // calendar, or the live challenge would be published as "yesterday's".
        var current = teasers.GetCurrent(date);
        return current is null ? null : teasers.GetPreviousBefore(current.Date);
    }
}
