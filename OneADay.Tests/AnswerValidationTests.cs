using OneADay.Models;

namespace OneADay.Tests;

public class AnswerValidationTests
{
    private static BrainTeaser TeaserWithAnswer(string answer) => new() { Answer = answer };

    [Theory]
    // Plain strings: case, spacing, and punctuation are forgiven
    [InlineData("a keyboard", "a keyboard")]
    [InlineData("a keyboard", "A Keyboard!")]
    [InlineData("a keyboard", "  a   keyboard  ")]
    [InlineData("an echo; echo", "Echo")]
    [InlineData("an echo; echo", "an echo.")]
    // Integers: numeric equivalence, not string equality
    [InlineData("9", "9")]
    [InlineData("9", " 9 ")]
    [InlineData("9", "9.0")]
    [InlineData("9; nine", "nine")]
    [InlineData("1000", "1,000")]
    [InlineData("1,000,000", "1000000")]
    [InlineData("-5", "-5.0")]
    [InlineData("3.14", "3.14")]
    // Mixed answers with parentheses: whole, outside, or inside part all count
    [InlineData("12 (a dozen)", "12 (a dozen)")]
    [InlineData("12 (a dozen)", "12")]
    [InlineData("12 (a dozen)", "a dozen")]
    [InlineData("12 (a dozen)", "12 a dozen")]
    [InlineData("42 (the answer to everything)", "the answer to everything")]
    [InlineData("7 (seven); a week", "A WEEK")]
    public void Accepts(string storedAnswer, string submission) =>
        Assert.True(TeaserWithAnswer(storedAnswer).AcceptsAnswer(submission));

    [Theory]
    // Spelled-out numbers count as their numeric value
    [InlineData("80", "eighty")]
    [InlineData("80", "Eighty")]
    [InlineData("9", "nine")]
    [InlineData("0", "zero")]
    [InlineData("12", "twelve")]
    [InlineData("48", "forty-eight")]
    [InlineData("48", "forty eight")]
    [InlineData("40", "fourty")]          // common misspelling
    [InlineData("5000", "five thousand")]
    [InlineData("901", "nine hundred and one")]
    [InlineData("901", "nine hundred one")]
    [InlineData("100", "one hundred")]
    [InlineData("46", "forty six")]
    // ...and the reverse direction, when the stored answer is spelled out
    [InlineData("nine", "9")]
    [InlineData("forty-eight", "48")]
    // Number + unit works in words too
    [InlineData("80 degrees", "eighty degrees")]
    [InlineData("48 mph", "forty-eight mph")]
    [InlineData("eighty degrees", "80 degrees")]
    public void Accepts_number_words(string storedAnswer, string submission) =>
        Assert.True(TeaserWithAnswer(storedAnswer).AcceptsAnswer(submission));

    private const string Formula24 = "5*(5-(1/5))";
    private const string Formula3388 = "8 / (3 - (8/3))";

    [Theory]
    // The author's exact form
    [InlineData(Formula24, "5*(5-(1/5))")]
    // Redundant parentheses dropped
    [InlineData(Formula24, "5*(5-1/5)")]
    // Reordered — multiplication commutes
    [InlineData(Formula24, "(5-1/5)*5")]
    [InlineData(Formula24, "(5 - 1/5) * 5")]
    // Whitespace and alternate operator glyphs
    [InlineData(Formula24, "5 × (5 − 1/5)")]
    // Implicit multiplication
    [InlineData(Formula24, "5(5-1/5)")]
    // The nested 3,3,8,8 puzzle — decimal division leaves residue, must still match
    [InlineData(Formula3388, "8/(3-(8/3))")]
    [InlineData(Formula3388, "8/(3-8/3)")]
    [InlineData(Formula3388, "8 / (3 - 8 / 3)")]
    // A plain-number answer accepts arithmetic that reaches it (showing work)
    [InlineData("5000", "500*10")]
    [InlineData("5000", "255*255-245*245")]
    [InlineData("48", "96/2")]
    public void Accepts_equivalent_formulas(string storedAnswer, string submission) =>
        Assert.True(TeaserWithAnswer(storedAnswer).AcceptsAnswer(submission));

    [Theory]
    // Non-terminating division is compared at the thousandths place.
    // NB: store value answers as a NUMBER ("0.333"), not a formula — a stored
    // formula additionally requires the solver to show a formula (see
    // Rejects_wrong_or_malformed_formulas).
    [InlineData("0.333", "1/3")]
    [InlineData("0.333", "0.3333333333")]
    [InlineData("0.667", "2/3")]
    [InlineData("0.667", "0.6666")]
    // Rounding is away-from-zero at the midpoint
    [InlineData("0.334", "0.3335")]
    [InlineData("-0.333", "-1/3")]
    // The nested puzzle still lands on 24 despite division residue
    [InlineData("24", "8/(3-8/3)")]
    [InlineData("8/(3-(8/3))", "8/(3-8/3)")]
    // Whole numbers are unaffected
    [InlineData("45", "45.0001")]
    public void Compares_decimals_at_thousandths(string storedAnswer, string submission) =>
        Assert.True(TeaserWithAnswer(storedAnswer).AcceptsAnswer(submission));

    [Theory]
    // Differences at or above the thousandths place are still wrong
    [InlineData("0.333", "0.334")]
    [InlineData("0.333", "0.34")]
    [InlineData("0.333", "1/2")]
    [InlineData("0.667", "0.666")]
    [InlineData("45", "45.01")]
    [InlineData("9.0", "90")]
    // A stored FORMULA still demands a formula built from the same numbers,
    // even when the values agree — so store value answers as plain numbers.
    [InlineData("1/3", "0.333")]
    [InlineData("2/3", "0.6666666")]
    public void Rejects_beyond_thousandths(string storedAnswer, string submission) =>
        Assert.False(TeaserWithAnswer(storedAnswer).AcceptsAnswer(submission));

    [Theory]
    // Formula puzzles must still require a formula — a bare result is not a solution
    [InlineData(Formula24, "24")]
    [InlineData(Formula3388, "24")]
    // Hard operand check: the expression must use exactly the numbers the
    // question supplied, so hitting the target another way is not a solution
    [InlineData(Formula24, "12+12")]
    [InlineData(Formula24, "8*3")]
    [InlineData(Formula24, "25-1")]
    [InlineData(Formula3388, "12+12")]
    // ...including "simplifying" a step into a number that wasn't provided
    [InlineData(Formula24, "5 * (5 - 0.2)")]
    [InlineData(Formula24, "5*4.8")]
    // ...or reusing a provided number more times than it was given
    [InlineData(Formula24, "5*5-(5/5)")]
    // Wrong values stay wrong
    [InlineData(Formula24, "5*(5-1)")]
    [InlineData(Formula24, "23")]
    [InlineData(Formula3388, "8/(3-8)")]
    // Fragments of the formula are not the formula
    [InlineData(Formula24, "5")]
    [InlineData(Formula3388, "8")]
    // Malformed expressions are rejected rather than crashing
    [InlineData(Formula24, "5*(5-")]
    [InlineData(Formula24, "((((")]
    [InlineData(Formula24, "5//5")]
    [InlineData("1", "1/0")]
    public void Rejects_wrong_or_malformed_formulas(string storedAnswer, string submission) =>
        Assert.False(TeaserWithAnswer(storedAnswer).AcceptsAnswer(submission));

    [Theory]
    // Wrong spelled-out numbers are still wrong
    [InlineData("80", "ninety")]
    [InlineData("80", "eight")]
    [InlineData("48", "forty-nine")]
    [InlineData("5000", "five hundred")]
    [InlineData("901", "nine hundred")]
    // Non-numeric words must not be coerced into numbers
    [InlineData("80", "thousand")]
    [InlineData("80", "banana")]
    [InlineData("80 degrees", "eighty radians")]
    public void Rejects_wrong_number_words(string storedAnswer, string submission) =>
        Assert.False(TeaserWithAnswer(storedAnswer).AcceptsAnswer(submission));

    [Theory]
    [InlineData("a keyboard", "a piano")]
    [InlineData("9", "8")]
    [InlineData("9", "90")]
    [InlineData("9.0", "90")]  // "9.0" must not collapse into "90"
    [InlineData("3.14", "314")]
    [InlineData("1,000", "100")]
    [InlineData("12 (a dozen)", "a baker's dozen")]
    [InlineData("an echo", "")]
    [InlineData("an echo", "   ")]
    public void Rejects(string storedAnswer, string submission) =>
        Assert.False(TeaserWithAnswer(storedAnswer).AcceptsAnswer(submission));
}
