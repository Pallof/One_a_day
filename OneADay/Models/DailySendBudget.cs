namespace OneADay.Models;

/// <summary>
/// How many notification emails may still go out today.
///
/// <para>Pulled out of the sender so it can be tested without an SMTP server and
/// without waiting for midnight: the day is passed in rather than read from a clock,
/// the same way <c>TeaserRotation</c> takes its randomness as a parameter.</para>
///
/// <para><b>It counts deliveries, not attempts.</b> A slot is only spent once a send
/// actually succeeds. Counting attempts would mean an SMTP outage burns the whole
/// day's budget on mail that never arrived — and when service came back the cap would
/// already be exhausted, so the first working notifications of the day would be the
/// ones dropped.</para>
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

    /// <summary>Spends a slot. Call this only after a send has actually succeeded.</summary>
    public void RecordSent(DateOnly today)
    {
        Roll(today);
        _sent++;
    }

    private void Roll(DateOnly today)
    {
        if (_day != today)
        {
            _day = today;
            _sent = 0;
        }
    }
}
