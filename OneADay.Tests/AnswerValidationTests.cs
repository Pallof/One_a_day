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
