using System.Globalization;

namespace OneADay.Models;

public class BrainTeaser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The day this teaser is shown as the "Challenge of the day".</summary>
    public DateOnly Date { get; set; }

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
    /// and the full "12 (a dozen)".
    /// </summary>
    private static IEnumerable<string> Variants(string accepted)
    {
        yield return accepted;

        var open = accepted.IndexOf('(');
        var close = accepted.LastIndexOf(')');
        if (open >= 0 && close > open)
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
