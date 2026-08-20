using System.Globalization;

namespace OneADay.Models;

/// <summary>
/// Shared arithmetic for anything that has to judge a written expression —
/// teaser answers (<see cref="BrainTeaser"/>) and the Twenty Four game.
/// Keeping one implementation means the "compare at the thousandths place"
/// rule can't drift between them.
/// </summary>
public static class Arithmetic
{
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
    public static bool TryEvaluate(string text, out decimal value)
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
    public static List<decimal> Operands(string text)
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
    public const int ComparisonDecimals = 3;

    public static decimal Round(decimal value) =>
        Math.Round(value, ComparisonDecimals, MidpointRounding.AwayFromZero);

    public static bool ValuesClose(decimal a, decimal b) =>
        Round(a) == Round(b);

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
}
