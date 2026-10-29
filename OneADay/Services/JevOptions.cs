namespace OneADay.Services;

/// <summary>
/// The second opinion on answers the checker turns down (PRD 02): Jev, TypeSafe's yes/no
/// decision model, reached through OpenRouter.
///
/// Everything here except <see cref="ApiKey"/> is safe to commit. The key is an OpenRouter key
/// and comes from user-secrets locally or the <c>Jev__ApiKey</c> environment variable on the
/// server. It must never appear in appsettings.json. Without it, or switched off, nothing is
/// ever sent and the checker alone decides: Jev's earlier verdicts aren't used either.
/// </summary>
public sealed class JevOptions
{
    public const string Section = "Jev";

    /// <summary>
    /// Master switch. The property default is <c>false</c>, which keeps tests from ever reaching
    /// out; appsettings.json sets it true, so a key is all it takes to turn the second opinion on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>An OpenRouter API key. A secret: see the class summary.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>OpenRouter's decisions endpoint. OpenRouter still calls it alpha, so it can be changed here.</summary>
    public string Endpoint { get; set; } = "https://openrouter.ai/api/alpha/decisions";

    /// <summary>A pinned version, so a newer model can't quietly move the line between right and wrong.</summary>
    public string Model { get; set; } = "typesafe/jev-1.14";

    /// <summary>
    /// Jev's chance that it's the same answer must reach this for the answer to count as right.
    /// Seventy, not ninety (author's decision, 2026-10-28): on 47 trial answers every wrong one
    /// scored 0.49 or less and every right one 0.73 or more, and ninety failed 8 of the 27 right.
    /// </summary>
    public double Threshold { get; set; } = 0.7;

    /// <summary>
    /// Most questions sent to Jev in one Pacific day. Past it the checker alone decides until
    /// midnight, so someone typing nonsense all day can't run up the bill. A thousand, up from
    /// 300, so a busy day doesn't run out by the afternoon (author's decision, 2026-10-28): at
    /// about $0.000017 a question, a full month is about 50 cents.
    /// </summary>
    public int MaxPerDay { get; set; } = 1000;

    /// <summary>
    /// Most of those one visitor address may use in a Pacific day, so one script can't spend
    /// everyone's. Thirty, since a home or office network counts as one address (author's
    /// decision, 2026-10-28); a player typing normally needs one or two.
    /// </summary>
    public int MaxPerAddressPerDay { get; set; } = 30;

    /// <summary>
    /// How long a visitor waits for Jev before the checker's "not quite" stands. Five, not two
    /// (author's decision, 2026-10-27): a wait now and then is better than a right answer
    /// marked wrong because the network was slow.
    /// </summary>
    public double TimeoutSeconds { get; set; } = 5;

    /// <summary>Switched on, with a key to call it with.</summary>
    public bool IsOn => Enabled && !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// Whether a chance from Jev counts as right at today's bar. Kept verdicts are judged by this
    /// too, so moving the bar regrades every one of them without asking Jev again.
    /// </summary>
    public bool Passes(double chance) => chance >= Threshold;
}
