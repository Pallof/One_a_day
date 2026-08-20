using OneADay.Models;

namespace OneADay.Tests;

/// <summary>
/// The Twenty Four game: dealing solvable hands, and judging what a player types —
/// legal characters, balanced parentheses, using exactly the numbers dealt, and
/// hitting 24 including via fractions.
/// </summary>
public class TwentyFourGameTests
{
    private static readonly int[] Hand3588 = [3, 5, 8, 8];
    private static readonly int[] Hand3388 = [3, 3, 8, 8];

    // ---- accepted answers -----------------------------------------------------

    [Theory]
    [InlineData("(3 + 5) * (8 - 5)", new[] { 3, 5, 8, 5 })]
    [InlineData("8 * 3 * 1 * 1", new[] { 8, 3, 1, 1 })]
    [InlineData("(1 + 2) * (3 + 5)", new[] { 1, 2, 3, 5 })]
    // order and spacing are the player's business
    [InlineData("(5 - 1/5) * 5", new[] { 5, 1, 5, 5 })]
    [InlineData("6*4*1*1", new[] { 1, 4, 6, 1 })]
    public void Accepts_a_correct_expression(string expression, int[] hand) =>
        Assert.True(TwentyFourGame.Check(expression, hand).IsCorrect,
            TwentyFourGame.Check(expression, hand).Message);

    [Fact]
    public void Accepts_the_classic_fraction_hand()
    {
        // 8 / (3 - 8/3) = 24 only if division is carried to enough precision.
        var check = TwentyFourGame.Check("8 / (3 - 8/3)", Hand3388);
        Assert.True(check.IsCorrect, check.Message);
    }

    [Theory]
    [InlineData("8/(3-8/3)")]
    [InlineData("8 / ( 3 - ( 8 / 3 ) )")]
    public void Fraction_answers_survive_spacing_and_extra_parentheses(string expression) =>
        Assert.True(TwentyFourGame.Check(expression, Hand3388).IsCorrect);

    // ---- rejected: illegal characters -----------------------------------------

    [Theory]
    [InlineData("3 ^ 5 + 8 + 8")]     // exponent
    [InlineData("sqrt(9)+5+8+8")]     // functions
    [InlineData("3 % 5 + 8 + 8")]     // modulo
    [InlineData("3! + 5 + 8 + 8")]    // factorial
    public void Rejects_illegal_operators(string expression)
    {
        var check = TwentyFourGame.Check(expression, Hand3588);
        Assert.Equal(TwentyFourResult.IllegalCharacter, check.Result);
    }

    [Fact]
    public void Rejects_decimal_points_with_a_helpful_message()
    {
        var check = TwentyFourGame.Check("3.5 * 8 - 8 + 5", Hand3588);
        Assert.Equal(TwentyFourResult.IllegalCharacter, check.Result);
        Assert.Contains("Decimal points", check.Message);
    }

    // ---- rejected: parentheses -------------------------------------------------

    [Theory]
    [InlineData("(3 + 5 * (8 - 8)")]      // one never closed
    [InlineData("3 + 5) * (8 - 8")]       // closed before opened
    [InlineData("((3 + 5) * 8 - 8")]
    [InlineData(")3 + 5 + 8 + 8(")]       // right count, wrong order
    public void Rejects_unbalanced_parentheses(string expression)
    {
        var check = TwentyFourGame.Check(expression, Hand3588);
        Assert.Equal(TwentyFourResult.UnbalancedParentheses, check.Result);
    }

    // ---- rejected: malformed ---------------------------------------------------

    [Theory]
    [InlineData("3 + 5 * ")]
    [InlineData("3 5 8 8")]
    [InlineData("3 * / 5 + 8 + 8")]
    [InlineData("()")]
    public void Rejects_malformed_expressions(string expression)
    {
        var result = TwentyFourGame.Check(expression, Hand3588).Result;
        Assert.True(result is TwentyFourResult.Malformed or TwentyFourResult.WrongNumbers,
            $"expected malformed/wrong-numbers but got {result}");
    }

    [Fact]
    public void Rejects_an_empty_submission()
    {
        Assert.Equal(TwentyFourResult.Empty, TwentyFourGame.Check("", Hand3588).Result);
        Assert.Equal(TwentyFourResult.Empty, TwentyFourGame.Check("   ", Hand3588).Result);
        Assert.Equal(TwentyFourResult.Empty, TwentyFourGame.Check(null, Hand3588).Result);
    }

    // ---- rejected: wrong numbers -----------------------------------------------

    [Theory]
    [InlineData("24")]                    // just writing the answer
    [InlineData("12 + 12")]               // numbers never dealt
    [InlineData("3 * 8")]                 // only two of the four
    [InlineData("3 + 5 + 8 + 8 + 8")]     // an extra card
    [InlineData("3 + 5 + 8 + 9")]         // one number swapped
    [InlineData("38 + 5 - 8 - 8")]        // digits glued into a new number
    public void Rejects_expressions_that_do_not_use_the_dealt_numbers(string expression)
    {
        var check = TwentyFourGame.Check(expression, Hand3588);
        Assert.Equal(TwentyFourResult.WrongNumbers, check.Result);
        Assert.Contains("exactly once", check.Message);
    }

    [Fact]
    public void Reusing_a_number_more_often_than_dealt_is_rejected()
    {
        // 8 appears twice in the hand; using it three times is not allowed.
        var check = TwentyFourGame.Check("8 + 8 + 8 - 3 + 5 - 2", Hand3588);
        Assert.Equal(TwentyFourResult.WrongNumbers, check.Result);
    }

    // ---- rejected: right numbers, wrong total ----------------------------------

    [Fact]
    public void Wrong_total_says_what_it_actually_came_to()
    {
        var check = TwentyFourGame.Check("3 * 5 + 8 + 8", Hand3588);   // 31
        Assert.Equal(TwentyFourResult.WrongValue, check.Result);
        Assert.Contains("31", check.Message);
        Assert.Contains("not 24", check.Message);
    }

    [Fact]
    public void Near_misses_from_fractions_are_still_wrong()
    {
        // 8 / (3 - 8/3) is 24; nudging it must not sneak through the rounding.
        var check = TwentyFourGame.Check("8 / (3 - 3/8)", Hand3388);
        Assert.Equal(TwentyFourResult.WrongValue, check.Result);
    }

    // ---- dealing ---------------------------------------------------------------

    [Fact]
    public void Deals_four_cards_between_one_and_ten()
    {
        var random = new Random(12345);
        for (var i = 0; i < 200; i++)
        {
            var hand = TwentyFourGame.Deal(random);
            Assert.Equal(TwentyFourGame.HandSize, hand.Length);
            Assert.All(hand, n => Assert.InRange(n, TwentyFourGame.LowestCard, TwentyFourGame.HighestCard));
        }
    }

    [Fact]
    public void Dealing_does_not_screen_out_impossible_hands()
    {
        // Not knowing whether a hand can be cracked is part of the game, so the
        // deal must NOT quietly filter for solvability.
        var random = new Random(2024);
        var sawImpossible = Enumerable.Range(0, 4000)
            .Select(_ => TwentyFourGame.Deal(random))
            .Any(hand => !TwentyFourGame.HasSolution(hand));
        Assert.True(sawImpossible, "impossible hands should still be dealt");
    }

    [Fact]
    public void Dealing_never_runs_the_solver()
    {
        // A cheap proxy for "no search happens on deal": dealing many hands must be
        // effectively instant, which it cannot be if each one is solved first.
        var random = new Random(99);
        var started = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 20_000; i++)
        {
            TwentyFourGame.Deal(random);
        }
        started.Stop();
        Assert.True(started.ElapsedMilliseconds < 500,
            $"20k deals took {started.ElapsedMilliseconds}ms — is the solver running on deal?");
    }

    [Fact]
    public void Duplicates_are_possible_in_a_hand()
    {
        var random = new Random(7);
        var sawDuplicate = Enumerable.Range(0, 300)
            .Select(_ => TwentyFourGame.Deal(random))
            .Any(hand => hand.Distinct().Count() < hand.Length);
        Assert.True(sawDuplicate, "a deck of cards produces duplicates; dealing should too");
    }

    // ---- the solver ------------------------------------------------------------

    [Theory]
    [InlineData(new[] { 3, 3, 8, 8 })]     // only solvable with fractions
    [InlineData(new[] { 1, 3, 4, 6 })]     // another fraction-only classic
    [InlineData(new[] { 8, 3, 1, 1 })]
    [InlineData(new[] { 6, 6, 6, 6 })]
    public void Finds_a_solution_for_solvable_hands(int[] hand)
    {
        var solution = TwentyFourGame.FindSolution(hand);
        Assert.NotNull(solution);
        // the solution it offers must itself pass the game's own checker
        Assert.True(TwentyFourGame.Check(solution, hand).IsCorrect,
            $"solver produced '{solution}', which the checker rejects");
    }

    [Theory]
    [InlineData(new[] { 1, 1, 1, 1 })]
    [InlineData(new[] { 1, 1, 1, 2 })]
    [InlineData(new[] { 10, 10, 10, 10 })]
    public void Recognises_impossible_hands(int[] hand)
    {
        Assert.Null(TwentyFourGame.FindSolution(hand));
        Assert.False(TwentyFourGame.HasSolution(hand));
    }
}
