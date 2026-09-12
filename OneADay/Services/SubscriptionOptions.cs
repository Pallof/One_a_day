namespace OneADay.Services;

/// <summary>
/// The daily-challenge email: when it goes out, and how much the account can carry.
/// </summary>
/// <remarks>
/// <para>All three limits share one Gmail account, which allows roughly <b>500 messages
/// a day</b> and throttles well before that from a server IP. The budget, per day:</para>
/// <code>
///   author notifications   25   (EmailOptions.MaxPerDay)
///   confirmations          50   (MaxConfirmationsPerDay)
///   daily digest          400   (MaxDigestsPerDay)
///   ────────────────────────
///                         475   against ~500
/// </code>
/// <para>So <b>around 400 confirmed subscribers is the hard ceiling</b> on this setup.
/// Past it, the fix is moving the digest to a transactional provider — see PRD 15 — not
/// raising these numbers, which only brings the account suspension closer.</para>
/// </remarks>
public sealed class SubscriptionOptions
{
    public const string Section = "Subscriptions";

    /// <summary>
    /// Hour of the Pacific day the digest goes out. Deliberately not midnight — which is
    /// when the challenge technically changes — because nobody wants a 12am email.
    /// </summary>
    public int SendHourPacific { get; set; } = 7;

    public int MaxConfirmationsPerDay { get; set; } = 50;

    public int MaxDigestsPerDay { get; set; } = 400;
}
