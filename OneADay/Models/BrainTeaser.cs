using System.Globalization;

namespace OneADay.Models;

public enum Difficulty
{
    Easy,
    Medium,
    Hard,
}

public class BrainTeaser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The day this teaser is shown as the "Challenge of the day".</summary>
    public DateOnly Date { get; set; }

    public Difficulty Difficulty { get; set; } = Difficulty.Medium;

    /// <summary>Classification labels (e.g. "logic", "wordplay", "math").</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Filename of an optional support image, served from /teaser-images.</summary>
    public string? ImageFileName { get; set; }

    public string Question { get; set; } = string.Empty;

    /// <summary>Accepted answer. Multiple accepted answers can be separated with ';'.</summary>
    public string Answer { get; set; } = string.Empty;

    public string? Hint { get; set; }

    /// <summary>Optional worked explanation, revealed on past questions.</summary>
    public string? Solution { get; set; }

    public bool AcceptsAnswer(string submission)
    {
        if (string.IsNullOrWhiteSpace(submission))
        {
            return false;
        }
        return Answer
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(Variants)
            .Any(accepted => Matches(accepted, submission));
    }

    /// <summary>
    /// An accepted answer like "12 (a dozen)" also accepts "12", "a dozen",
    /// and the full "12 (a dozen)". Only a single parenthetical group is split
    /// this way — formula answers such as "5*(5-(1/5))" have multiple/nested
    /// parentheses and are left intact so they aren't matched by a fragment.
    /// </summary>
    private static IEnumerable<string> Variants(string accepted)
    {
        yield return accepted;

        if (accepted.Count(c => c == '(') != 1 || accepted.Count(c => c == ')') != 1)
        {
            yield break;
        }

        var open = accepted.IndexOf('(');
        var close = accepted.IndexOf(')');
        if (close > open)
        {
            yield return (accepted[..open] + accepted[(close + 1)..]).Trim();
            yield return accepted[(open + 1)..close].Trim();
        }
    }

    private static bool Matches(string accepted, string submission)
    {
        // Numeric answers compare by value, so "9", "9.0", and "1,000" vs "1000" line up.
        if (TryParseNumber(accepted, out var acceptedNumber) &&
            TryParseNumber(submission, out var submittedNumber))
        {
            return acceptedNumber == submittedNumber;
        }

        var normalizedAccepted = Normalize(accepted);
        return normalizedAccepted.Length > 0 && normalizedAccepted == Normalize(submission);
    }

    private static bool TryParseNumber(string text, out decimal value) =>
        decimal.TryParse(
            text.Trim().TrimEnd('.').Replace(",", ""),
            NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out value);

    private static string Normalize(string text) =>
        new(text.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
