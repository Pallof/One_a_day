namespace OneADay.Models;

/// <summary>
/// Small helpers for display text, so markup can stay readable.
/// </summary>
public static class Wording
{
    /// <summary>
    /// Picks singular or plural wording for a count.
    /// </summary>
    /// <remarks>
    /// This exists to keep pluralisation out of markup. Written inline, the idiom is
    /// <c>submission@(count == 1 ? "" : "s")</c> — which splits a word across a
    /// conditional and reads as neither the word nor the logic. Pass irregular plurals
    /// explicitly: <c>Plural(n, "mind has", "minds have")</c>.
    /// </remarks>
    public static string Plural(int count, string singular, string? plural = null)
    {
        if (count == 1)
        {
            return singular;
        }
        return plural ?? singular + "s";
    }
}
