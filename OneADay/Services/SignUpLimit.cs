using Microsoft.Extensions.Options;
using OneADay.Models;

namespace OneADay.Services;

/// <summary>
/// How many sign-ups each visitor address may make today
/// (<see cref="SubscriptionOptions.MaxSignUpsPerAddressPerDay"/>).
/// </summary>
/// <remarks>
/// <para>The global cap on confirmations (<see cref="ConfirmationSender"/>) keeps the Gmail
/// account safe whatever happens, but on its own one script can spend it and leave every real
/// sign-up that day with nothing. This spreads it out. Security review, 2026-09-28.</para>
///
/// <para>Kept in memory. A restart gives everyone a fresh allowance, which is harmless for a
/// limit that only has to outlast one script's run. The day is passed in, like
/// <see cref="DailySendBudget"/>, so tests don't wait for midnight.</para>
/// </remarks>
public sealed class SignUpLimit(IOptions<SubscriptionOptions> options)
{
    /// <summary>
    /// Addresses tracked in one day before new ones are refused. A flood from that many
    /// addresses has spent the day's confirmations long before, and the table stays small.
    /// </summary>
    public const int MaxAddressesPerDay = 10_000;

    private readonly int _maxPerDay = options.Value.MaxSignUpsPerAddressPerDay;
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _used = [];
    private DateOnly _day;

    /// <summary>Spends one of today's sign-ups for this address. False when none are left.</summary>
    /// <param name="address">From <see cref="VisitorAddress.Key"/>. An unknown address is always
    /// allowed: the global cap still stands behind it.</param>
    public bool TryTake(string? address, DateOnly today)
    {
        if (address is null)
        {
            return true;
        }

        lock (_gate)
        {
            if (_day != today)
            {
                _day = today;
                _used.Clear();
            }

            var used = _used.GetValueOrDefault(address);
            if (used >= _maxPerDay || (used == 0 && _used.Count >= MaxAddressesPerDay))
            {
                return false;
            }

            _used[address] = used + 1;
            return true;
        }
    }
}
