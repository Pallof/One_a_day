namespace OneADay.Models;

/// <summary>
/// Each visitor address's own share of something counted per day, on top of a site-wide
/// <see cref="DailySendBudget"/>, so one address can't spend the whole day's budget.
///
/// <para>Not locked: the caller holds one lock over both, so a slot is checked and spent in
/// both at once. The day is passed in, like <see cref="DailySendBudget"/>, so tests don't wait
/// for midnight. Addresses come from <see cref="VisitorAddress.Key"/>. Every unknown one (null)
/// shares a single share: a page reached over a held poll instead of a WebSocket has no
/// address by the time it's used, and a script can choose that on purpose (PR 74 review).</para>
///
/// <para>Only addresses that spent something are tracked, so the table never holds more
/// entries in a day than the site-wide budget allows.</para>
/// </summary>
public sealed class DailyAddressAllowance(int maxPerDay)
{
    private readonly Dictionary<string, int> _used = [];
    private DateOnly _day;

    public int MaxPerDay { get; } = maxPerDay;

    public bool HasRoom(string? address, DateOnly today)
    {
        Roll(today);
        return _used.GetValueOrDefault(VisitorAddress.OrUnknown(address)) < MaxPerDay;
    }

    public void Record(string? address, DateOnly today)
    {
        Roll(today);
        var key = VisitorAddress.OrUnknown(address);
        _used[key] = _used.GetValueOrDefault(key) + 1;
    }

    /// <summary>Forward only, for the reason <see cref="DailySendBudget"/> gives.</summary>
    private void Roll(DateOnly today)
    {
        if (today > _day)
        {
            _day = today;
            _used.Clear();
        }
    }
}
