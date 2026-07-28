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
