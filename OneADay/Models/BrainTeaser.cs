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
        // Numeric answers compare by value, so "9", "9.0", "1,000" vs "1000",
        // and spelled-out words like "eighty" all line up.
        if (TryParseNumber(accepted, out var acceptedNumber) &&
            TryParseNumber(submission, out var submittedNumber))
        {
            return acceptedNumber == submittedNumber;
        }

        // Same, but for a number followed by a unit ("80 degrees" vs "eighty degrees").
        if (TrySplitNumberAndUnit(accepted, out var acceptedValue, out var acceptedUnit) &&
            TrySplitNumberAndUnit(submission, out var submittedValue, out var submittedUnit) &&
            acceptedValue == submittedValue &&
            Normalize(acceptedUnit) == Normalize(submittedUnit))
        {
            return true;
        }

        var normalizedAccepted = Normalize(accepted);
        return normalizedAccepted.Length > 0 && normalizedAccepted == Normalize(submission);
    }

    /// <summary>
    /// Splits "80 degrees" / "eighty degrees" / "48 mph" into value + unit,
    /// taking the longest leading run of words that parses as a number.
    /// </summary>
    private static bool TrySplitNumberAndUnit(string text, out decimal value, out string unit)
    {
        value = 0;
        unit = string.Empty;

        var words = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2)
        {
            return false;
        }

        for (var take = words.Length - 1; take >= 1; take--)
        {
            if (TryParseNumber(string.Join(' ', words[..take]), out var parsed))
            {
                value = parsed;
                unit = string.Join(' ', words[take..]);
                return unit.Length > 0;
            }
        }
        return false;
    }

    /// <summary>
    /// Parses a number written as digits ("80", "1,000", "-5.5") or as English
    /// words ("eighty", "forty-eight", "five thousand", "one hundred and one").
    /// </summary>
    private static bool TryParseNumber(string text, out decimal value)
    {
        if (decimal.TryParse(
                text.Trim().TrimEnd('.').Replace(",", ""),
                NumberStyles.Number | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value))
        {
            return true;
        }
        return TryParseNumberWords(text, out value);
    }

    private static readonly Dictionary<string, long> SmallNumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"] = 0, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4,
        ["five"] = 5, ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9,
        ["ten"] = 10, ["eleven"] = 11, ["twelve"] = 12, ["thirteen"] = 13,
        ["fourteen"] = 14, ["fifteen"] = 15, ["sixteen"] = 16, ["seventeen"] = 17,
        ["eighteen"] = 18, ["nineteen"] = 19, ["twenty"] = 20, ["thirty"] = 30,
        ["forty"] = 40, ["fourty"] = 40, ["fifty"] = 50, ["sixty"] = 60,
        ["seventy"] = 70, ["eighty"] = 80, ["ninety"] = 90,
    };

    private static readonly Dictionary<string, long> MultiplierWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hundred"] = 100, ["thousand"] = 1_000, ["million"] = 1_000_000, ["billion"] = 1_000_000_000,
    };

    private static bool TryParseNumberWords(string text, out decimal value)
    {
        value = 0;
        var words = text.ToLowerInvariant()
            .Replace('-', ' ')
            .Split([' ', '\t', ',', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w is not ("and" or "a"))
            .ToArray();

        if (words.Length == 0)
        {
            return false;
        }

        long total = 0, current = 0;
        var sawNumberWord = false;

        foreach (var word in words)
        {
            if (SmallNumberWords.TryGetValue(word, out var small))
            {
                current += small;
                sawNumberWord = true;
            }
            else if (MultiplierWords.TryGetValue(word, out var multiplier))
            {
                if (!sawNumberWord && multiplier >= 1000)
                {
                    return false;   // "thousand" alone isn't a number
                }
                if (current == 0)
                {
                    current = 1;    // "hundred" → 100
                }
                if (multiplier == 100)
                {
                    current *= multiplier;
                }
                else
                {
                    total += current * multiplier;
                    current = 0;
                }
                sawNumberWord = true;
            }
            else
            {
                return false;   // any non-number word ⇒ not a spelled-out number
            }
        }

        if (!sawNumberWord)
        {
            return false;
        }
        value = total + current;
        return true;
    }

    private static string Normalize(string text) =>
        new(text.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
