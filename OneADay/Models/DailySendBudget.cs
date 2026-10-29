namespace OneADay.Models;

/// <summary>
/// How many of something counted per day may still go out today: notification emails, or
/// questions to Jev (<c>AnswerGrader</c>).
///
/// <para>Pulled out of the sender so it can be tested without an SMTP server and
/// without waiting for midnight: the day is passed in rather than read from a clock,
/// the same way <c>TeaserRotation</c> takes its randomness as a parameter.</para>
///
/// <para><b>The caller decides what counts.</b> The email senders spend a slot only once a
/// send actually succeeds. Counting attempts would mean an SMTP outage burns the whole
/// day's budget on mail that never arrived — and when service came back the cap would
/// already be exhausted, so the first working notifications of the day would be the
/// ones dropped. <c>AnswerGrader</c> spends one for every question sent to Jev, answered or
/// not, since a call that times out may still be billed.</para>
/// </summary>
public sealed class DailySendBudget(int maxPerDay)
{
    private int _sent;
    private DateOnly _day;

    public int MaxPerDay { get; } = maxPerDay;

    /// <summary>Sends recorded for <paramref name="today"/>; zero for any other day.</summary>
    public int SentOn(DateOnly today) => _day == today ? _sent : 0;

    /// <summary>
    /// Whether another send is allowed today. Rolls the counter when the day changes,
    /// so a long-running process doesn't carry yesterday's total forward.
    /// </summary>
    public bool HasRoom(DateOnly today)
    {
        Roll(today);
        return _sent < MaxPerDay;
    }

    /// <summary>Spends a slot, at whatever point the caller counts it (see the class summary).</summary>
    public void RecordSent(DateOnly today)
    {
        Roll(today);
        _sent++;
    }

    /// <summary>
    /// Forward only. A clock stepped back — an NTP correction around midnight — would
    /// otherwise start a fresh day, and stepping forward again would start the later day
    /// afresh too: a second allowance for a day already spent. A step back just keeps
    /// counting against the latest day.
    /// </summary>
    private void Roll(DateOnly today)
    {
        if (today > _day)
        {
            _day = today;
            _sent = 0;
        }
    }
}
