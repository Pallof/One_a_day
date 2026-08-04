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
            return ValuesClose(acceptedNumber, submittedNumber);
        }

        // Formula answers are evaluated, so any arithmetically equivalent way of
        // writing the same expression counts: "5*(5-1/5)" and "(5-1/5)*5" are
        // the same answer.
        if (TryEvaluateExpression(accepted, out var acceptedResult) &&
            TryEvaluateExpression(submission, out var submittedResult) &&
            ValuesClose(acceptedResult, submittedResult))
        {
            var acceptedIsFormula = !TryParseNumber(accepted, out _);
            if (!acceptedIsFormula)
            {
                // Plain-number answer: any arithmetic reaching it counts as showing work.
                return true;
            }

            // Formula puzzles ("make 24 from 5,5,5,1") are only solved by an
            // expression built from exactly the numbers the question supplies —
            // so a bare "24", or an unrelated "12+12", is not a solution.
            var submissionIsFormula = !TryParseNumber(submission, out _);
            if (submissionIsFormula &&
                ExpressionOperands(accepted).SequenceEqual(ExpressionOperands(submission)))
            {
                return true;
            }
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

    // ---- Arithmetic expression evaluation -------------------------------------
    // Lets formula answers be compared by what they compute rather than how they
    // were typed. Grammar:
    //   expr   := term (('+' | '-') term)*
    //   term   := factor (('*' | '/') factor | '(' expr ')')*     ← implicit ×
    //   factor := ('+' | '-')? (number | '(' expr ')')

    private const int MaxExpressionDepth = 32;

    /// <summary>
    /// Evaluates a basic arithmetic expression (+ - * / and parentheses, with
    /// × ÷ accepted). Returns false for anything that isn't a well-formed
    /// expression, including division by zero.
    /// </summary>
    private static bool TryEvaluateExpression(string text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var s = text.Replace(",", "").Replace('×', '*').Replace('÷', '/').Replace('−', '-');
        var i = 0;
        try
        {
            if (!TryParseExpr(s, ref i, 0, out value))
            {
                return false;
            }
        }
        catch (Exception e) when (e is OverflowException or DivideByZeroException)
        {
            return false;
        }

        SkipWhitespace(s, ref i);
        return i == s.Length;   // the whole string must be consumed
    }

    /// <summary>
    /// The numeric literals an expression is built from, sorted so two orderings
    /// of the same numbers compare equal. Used to enforce that a formula answer
    /// uses exactly the numbers the question provided.
    /// </summary>
    private static List<decimal> ExpressionOperands(string text)
    {
        var s = text.Replace(",", "");
        var operands = new List<decimal>();
        var i = 0;
        while (i < s.Length)
        {
            if (!char.IsDigit(s[i]) && !(s[i] == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
            {
                i++;
                continue;
            }
            var start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.'))
            {
                i++;
            }
            if (decimal.TryParse(s[start..i], NumberStyles.Number, CultureInfo.InvariantCulture, out var n))
            {
                operands.Add(n);
            }
        }
        operands.Sort();
        return operands;
    }

    /// <summary>
    /// Numeric answers are compared at the thousandths place. Puzzle arithmetic
    /// rarely divides evenly — 8/3 and 1/3 run forever — so both sides are
    /// rounded to 3 decimals before comparing. That makes "1/3" match a stored
    /// "0.333", and stops division residue (8/(3-8/3) lands a hair off 24) from
    /// failing an answer that is plainly correct.
    /// </summary>
    private const int ComparisonDecimals = 3;

    private static decimal RoundForComparison(decimal value) =>
        Math.Round(value, ComparisonDecimals, MidpointRounding.AwayFromZero);

    private static bool ValuesClose(decimal a, decimal b) =>
        RoundForComparison(a) == RoundForComparison(b);

    private static void SkipWhitespace(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i]))
        {
            i++;
        }
    }

    private static bool TryParseExpr(string s, ref int i, int depth, out decimal value)
    {
        value = 0;
        if (depth > MaxExpressionDepth || !TryParseTerm(s, ref i, depth, out value))
        {
            return false;
        }
        while (true)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length || (s[i] != '+' && s[i] != '-'))
            {
                return true;
            }
            var op = s[i++];
            if (!TryParseTerm(s, ref i, depth, out var rhs))
            {
                return false;
            }
            value = op == '+' ? value + rhs : value - rhs;
        }
    }

    private static bool TryParseTerm(string s, ref int i, int depth, out decimal value)
    {
        value = 0;
        if (!TryParseFactor(s, ref i, depth, out value))
        {
            return false;
        }
        while (true)
        {
            SkipWhitespace(s, ref i);
            if (i < s.Length && (s[i] == '*' || s[i] == '/'))
            {
                var op = s[i++];
                if (!TryParseFactor(s, ref i, depth, out var rhs))
                {
                    return false;
                }
                if (op == '/')
                {
                    if (rhs == 0)
                    {
                        return false;
                    }
                    value /= rhs;
                }
                else
                {
                    value *= rhs;
                }
            }
            else if (i < s.Length && s[i] == '(')
            {
                // Implicit multiplication, e.g. "5(5-1/5)".
                if (!TryParseFactor(s, ref i, depth, out var rhs))
                {
                    return false;
                }
                value *= rhs;
            }
            else
            {
                return true;
            }
        }
    }

    private static bool TryParseFactor(string s, ref int i, int depth, out decimal value)
    {
        value = 0;
        if (depth > MaxExpressionDepth)
        {
            return false;
        }
        SkipWhitespace(s, ref i);
        if (i >= s.Length)
        {
            return false;
        }

        if (s[i] == '+' || s[i] == '-')
        {
            var negate = s[i] == '-';
            i++;
            if (!TryParseFactor(s, ref i, depth + 1, out value))
            {
                return false;
            }
            if (negate)
            {
                value = -value;
            }
            return true;
        }

        if (s[i] == '(')
        {
            i++;
            if (!TryParseExpr(s, ref i, depth + 1, out value))
            {
                return false;
            }
            SkipWhitespace(s, ref i);
            if (i >= s.Length || s[i] != ')')
            {
                return false;
            }
            i++;
            return true;
        }

        var start = i;
        while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.'))
        {
            i++;
        }
        return i > start &&
               decimal.TryParse(s[start..i], NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static string Normalize(string text) =>
        new(text.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
