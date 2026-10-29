namespace OneADay.Models;

/// <summary>
/// Each visitor address's own share of something counted per day, on top of a site-wide
/// <see cref="DailySendBudget"/>, so one address can't spend the whole day's budget.
///
/// <para>Not locked: the caller holds one lock over both, so a slot is checked and spent in
/// both at once. The day is passed in, like <see cref="DailySendBudget"/>, so tests don't wait
/// for midnight. Addresses come from <see cref="VisitorAddress.Key"/>; an unknown one (null)
/// has no share of its own and is held only by the site-wide budget.</para>
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
        return address is null || _used.GetValueOrDefault(address) < MaxPerDay;
    }

    public void Record(string? address, DateOnly today)
    {
        Roll(today);
        if (address is not null)
        {
            _used[address] = _used.GetValueOrDefault(address) + 1;
        }
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
