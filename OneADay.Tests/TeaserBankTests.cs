using OneADay.Models;

namespace OneADay.Tests;

/// <summary>
/// Answer-matching tests for the 13 teasers imported from BrainTeaserQuestions.txt.
/// Each case pairs a stored Answer string with a submission and the expected result,
/// verifying that the intended solution(s) are accepted and unrelated inputs rejected.
/// </summary>
public class TeaserBankTests
{
    private static BrainTeaser WithAnswer(string answer) => new() { Answer = answer };

    // Stored Answer strings, kept in sync with App_Data/teasers.json.
    private const string Q1 = "5*(5-(1/5))";
    private const string Q2 = "5000";
    private const string Q3 = "46";
    private const string Q4 = "901";
    private const string Q5 = "2; 2:1; twice";
    private const string Q6 = "8 / (3 - (8/3))";
    private const string Q7 = "80; 80 degrees";
    private const string Q8 = "second; 2nd";
    private const string Q9 = "312211";
    private const string Q10 = "48; 48 miles per hour; 48 mph";
    private const string Q11 = "impossible; it's impossible; infinitely fast";
    private const string Q12 = "48; 48 mph";
    private const string Q13 = "0; they are next to each other; next to each other";

    [Theory]
    // 1 — make 24 from an equation (formula answer, whitespace-tolerant)
    [InlineData(Q1, "5*(5-(1/5))")]
    [InlineData(Q1, "5*(5 - 1/5)")]
    // 2 — 2055^2 - 2045^2
    [InlineData(Q2, "5000")]
    [InlineData(Q2, "5,000")]
    // 3 — sum of digits 0..10
    [InlineData(Q3, "46")]
    // 4 — digit-value summation 1..100
    [InlineData(Q4, "901")]
    // 5 — train/tunnel ratio
    [InlineData(Q5, "2")]
    [InlineData(Q5, "2:1")]
    [InlineData(Q5, "Twice")]
    // 6 — make 24 from 3,3,8,8 (nested-parenthesis formula)
    [InlineData(Q6, "8 / (3 - (8/3))")]
    [InlineData(Q6, "8/(3-(8/3))")]
    // 7 — clock angle at 1:20
    [InlineData(Q7, "80")]
    [InlineData(Q7, "80 degrees")]
    [InlineData(Q7, "80°")]
    // 8 — race position
    [InlineData(Q8, "second")]
    [InlineData(Q8, "Second")]
    [InlineData(Q8, "2nd")]
    // 9 — look-and-say sequence
    [InlineData(Q9, "312211")]
    // 10 — average speed 40/60
    [InlineData(Q10, "48")]
    [InlineData(Q10, "48 mph")]
    [InlineData(Q10, "48 miles per hour")]
    // 11 — racetrack impossibility
    [InlineData(Q11, "impossible")]
    [InlineData(Q11, "Impossible!")]
    [InlineData(Q11, "infinitely fast")]
    // 12 — highway 40/60 average speed
    [InlineData(Q12, "48")]
    [InlineData(Q12, "48mph")]
    // 13 — two poles / rope
    [InlineData(Q13, "0")]
    [InlineData(Q13, "they are next to each other")]
    [InlineData(Q13, "Next to each other.")]
    public void Accepts_intended_answer(string storedAnswer, string submission) =>
        Assert.True(WithAnswer(storedAnswer).AcceptsAnswer(submission));

    [Theory]
    // Unrelated / wrong inputs are rejected for each teaser.
    [InlineData(Q1, "24")]
    [InlineData(Q1, "5")]      // a formula fragment must NOT count as correct
    [InlineData(Q2, "6000")]
    [InlineData(Q3, "45")]
    [InlineData(Q4, "5050")]
    [InlineData(Q5, "3")]
    [InlineData(Q6, "24")]
    [InlineData(Q6, "8")]      // a formula fragment must NOT count as correct
    [InlineData(Q7, "90")]
    [InlineData(Q8, "first")]
    [InlineData(Q9, "111221")]
    [InlineData(Q10, "50")]
    [InlineData(Q11, "60")]
    [InlineData(Q12, "60")]
    [InlineData(Q13, "10")]
    [InlineData(Q1, "")]       // blank is never accepted
    public void Rejects_wrong_answer(string storedAnswer, string submission) =>
        Assert.False(WithAnswer(storedAnswer).AcceptsAnswer(submission));
}
