namespace OneADay.Models;

/// <summary>Why a Twenty Four submission was accepted or turned down.</summary>
public enum TwentyFourResult
{
    Correct,
    Empty,
    TooLong,
    IllegalCharacter,
    UnbalancedParentheses,
    Malformed,
    WrongNumbers,
    WrongValue,
}

public sealed record TwentyFourCheck(TwentyFourResult Result, string Message)
{
    public bool IsCorrect => Result == TwentyFourResult.Correct;
}

/// <summary>
/// The Twenty Four game: four numbers dealt like cards (1–10, duplicates allowed,
/// face cards counting 10), combined with + - * / and parentheses to make 24.
/// </summary>
public static class TwentyFourGame
{
    public const int Target = 24;
    public const int HandSize = 4;
    public const int LowestCard = 1;
    public const int HighestCard = 10;

    /// <summary>
    /// Longest submission accepted. The input's maxlength attribute covers honest
    /// browsers; this is the backstop for anything that bypasses it (a crafted
    /// SignalR message, say). Without it a megabyte-long flat expression like
    /// "1+1+1+…" would parse happily — the depth guard only limits *nesting*, not
    /// length — and burn CPU proportional to its size on every submission.
    /// </summary>
    public const int MaxExpressionLength = 120;

    /// <summary>Slack when deciding whether a value "is" 24 — see <see cref="Arithmetic"/>.</summary>
    private const decimal SolverTolerance = 0.0000001m;

    private static readonly char[] AllowedCharacters =
        ['+', '-', '*', '/', '(', ')', '×', '÷', '−', ' ', '\t'];

    /// <summary>
    /// Deals four cards, straight from the "deck" — no filtering.
    /// </summary>
    /// <remarks>
    /// Hands are deliberately NOT screened for solvability. Not knowing whether a
    /// hand can be cracked is part of the game, and screening would mean running the
    /// full search on every deal just to throw hands away. The solver is only asked
    /// a question when the player presses Pass.
    /// </remarks>
    public static int[] Deal(Random random)
    {
        var hand = new int[HandSize];
        for (var i = 0; i < HandSize; i++)
        {
            hand[i] = random.Next(LowestCard, HighestCard + 1);
        }
        return hand;
    }

    /// <summary>
    /// Whether a hand can reach 24 at all. Roughly 15% of random hands cannot.
    /// </summary>
    /// <remarks>
    /// Nothing the player can see calls this. Dealing does not screen hands, and
    /// Pass does not reveal answers — the solver exists so the tests can cross-check
    /// the checker (every solution it finds is fed back through <see cref="Check"/>)
    /// and so a future feature has it to hand. Keep it off the player-facing paths.
    /// </remarks>
    public static bool HasSolution(IReadOnlyList<int> hand) => FindSolution(hand) is not null;

    /// <summary>One worked expression that reaches 24, or null if the hand is impossible.</summary>
    public static string? FindSolution(IReadOnlyList<int> hand)
    {
        var items = hand.Select(n => ((decimal)n, n.ToString())).ToList();
        return Search(items, out var solution) ? Unwrap(solution) : null;
    }

    private static bool Search(List<(decimal Value, string Expr)> items, out string solution)
    {
        if (items.Count == 1)
        {
            solution = items[0].Expr;
            return Math.Abs(items[0].Value - Target) < SolverTolerance;
        }

        for (var i = 0; i < items.Count; i++)
        {
            for (var j = 0; j < items.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }
                var a = items[i];
                var b = items[j];
                var rest = items.Where((_, k) => k != i && k != j).ToList();

                foreach (var combined in Combine(a, b))
                {
                    rest.Add(combined);
                    if (Search(rest, out solution))
                    {
                        return true;
                    }
                    rest.RemoveAt(rest.Count - 1);
                }
            }
        }

        solution = string.Empty;
        return false;
    }

    private static IEnumerable<(decimal Value, string Expr)> Combine(
        (decimal Value, string Expr) a, (decimal Value, string Expr) b)
    {
        yield return (a.Value + b.Value, $"({a.Expr} + {b.Expr})");
        yield return (a.Value - b.Value, $"({a.Expr} - {b.Expr})");
        yield return (a.Value * b.Value, $"({a.Expr} * {b.Expr})");
        if (b.Value != 0)
        {
            yield return (a.Value / b.Value, $"({a.Expr} / {b.Expr})");
        }
    }

    private static string Unwrap(string expression) =>
        expression.StartsWith('(') && expression.EndsWith(')') && IsBalanced(expression[1..^1])
            ? expression[1..^1]
            : expression;

    /// <summary>
    /// Judges a player's expression against the hand they were dealt. Each failure
    /// names what actually went wrong so the player can fix it.
    /// </summary>
    public static TwentyFourCheck Check(string? expression, IReadOnlyList<int> hand)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return new(TwentyFourResult.Empty, "Type an expression first.");
        }

        if (expression.Length > MaxExpressionLength)
        {
            return new(TwentyFourResult.TooLong,
                $"That's far longer than four numbers need — keep it under {MaxExpressionLength} characters.");
        }

        var offending = expression.FirstOrDefault(c => !char.IsDigit(c) && !AllowedCharacters.Contains(c));
        if (offending != default)
        {
            var what = offending == '.'
                ? "Decimal points aren't allowed — use only the numbers you were dealt."
                : $"'{offending}' isn't allowed. Use your numbers with + - * / and parentheses only.";
            return new(TwentyFourResult.IllegalCharacter, what);
        }

        if (!IsBalanced(expression))
        {
            return new(TwentyFourResult.UnbalancedParentheses, "Those parentheses don't match up.");
        }

        if (!Arithmetic.TryEvaluate(expression, out var value))
        {
            return new(TwentyFourResult.Malformed,
                "That isn't a complete expression — check for a missing number or a stray operator.");
        }

        var used = Arithmetic.Operands(expression);
        var dealt = hand.Select(n => (decimal)n).OrderBy(n => n).ToList();
        if (!used.SequenceEqual(dealt))
        {
            return new(TwentyFourResult.WrongNumbers,
                $"Use each of your numbers exactly once. You were dealt {Join(dealt)}" +
                (used.Count == 0 ? "." : $", but used {Join(used)}."));
        }

        if (!Arithmetic.ValuesClose(value, Target))
        {
            return new(TwentyFourResult.WrongValue,
                $"That comes to {Trim(Arithmetic.Round(value))}, not {Target}.");
        }

        return new(TwentyFourResult.Correct, $"Correct — {expression.Trim()} = {Target}.");
    }

    /// <summary>Every '(' closed in order, and nothing closed that was never opened.</summary>
    private static bool IsBalanced(string expression)
    {
        var depth = 0;
        foreach (var c in expression)
        {
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')' && --depth < 0)
            {
                return false;
            }
        }
        return depth == 0;
    }

    private static string Join(IEnumerable<decimal> values) =>
        string.Join(", ", values.Select(Trim));

    private static string Trim(decimal value) =>
        value == decimal.Truncate(value)
            ? decimal.Truncate(value).ToString()
            : value.ToString("0.###");
}
