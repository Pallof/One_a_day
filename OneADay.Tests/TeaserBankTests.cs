using OneADay.Models;

namespace OneADay.Tests;

/// <summary>
/// Answer-matching tests for the teasers imported from BrainTeaserQuestions.txt.
/// Each case pairs a stored Answer string with a submission and the expected result,
/// verifying that the intended solution(s) are accepted and unrelated inputs rejected.
/// </summary>
public class TeaserBankTests
{
    private static BrainTeaser WithAnswer(string answer) => new() { Answer = answer };

    // Stored Answer strings, kept in sync with App_Data/teasers.json.
    private const string Q1 = "5*(5-(1/5))";
    private const string Q2 = "5000";
    private const string Q3 = "45";
    private const string Q4 = "901";
    private const string Q5 = "2; 2:1; twice";
    private const string Q6 = "8 / (3 - (8/3))";
    private const string Q7 = "80; 80 degrees";
    private const string Q8 = "second; 2nd";
    private const string Q9 = "312211";
    private const string Q10 = "48; 48 miles per hour; 48 mph";
    private const string Q11 = "impossible; it's impossible; infinite";
    // (The former Q12 was a duplicate of Q10 and has been removed from the teaser bank.)
    private const string Q13 = "0; they are next to each other; next to each other";

    // Second batch
    private const string Q14Frog = "16; 16 days; sixteen";
    private const string Q15Desert = "4; 4 people; four";
    private const string Q16Algae = "59; 59 minutes; 59 mins; fifty nine";
    private const string Q17Digits = "24; twenty four; twenty-four";
    private const string Q18Birthday = "23; 23 people; twenty three; twenty-three";
    private const string Q19Handshake = "36; 36 handshakes; thirty six; thirty-six";
    private const string Q20Bracket = "99; ninety nine; ninety-nine";
    private const string Q21Average = "0; 1; zero; one";

    [Theory]
    // 1 — make 24 from an equation (formula answer, whitespace-tolerant)
    [InlineData(Q1, "5*(5-(1/5))")]
    [InlineData(Q1, "5*(5 - 1/5)")]
    // 2 — 2055^2 - 2045^2
    [InlineData(Q2, "5000")]
    [InlineData(Q2, "5,000")]
    // 3 — sum of digits 0..10
    [InlineData(Q3, "45")]
    [InlineData(Q3, "forty-five")]
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
    [InlineData(Q11, "infinite")]
    // 13 — two poles / rope
    [InlineData(Q13, "0")]
    [InlineData(Q13, "they are next to each other")]
    [InlineData(Q13, "Next to each other.")]
    public void Accepts_intended_answer(string storedAnswer, string submission) =>
        Assert.True(WithAnswer(storedAnswer).AcceptsAnswer(submission));

    [Theory]
    // Frog in the well — 16 days (verified by simulation)
    [InlineData(Q14Frog, "16")]
    [InlineData(Q14Frog, "16 days")]
    [InlineData(Q14Frog, "sixteen")]
    // Desert crossing — 4 people (verified by exhaustive search)
    [InlineData(Q15Desert, "4")]
    [InlineData(Q15Desert, "4 people")]
    [InlineData(Q15Desert, "four")]
    // Algae doubling — 59 minutes
    [InlineData(Q16Algae, "59")]
    [InlineData(Q16Algae, "59 minutes")]
    [InlineData(Q16Algae, "59 mins")]
    [InlineData(Q16Algae, "fifty nine")]
    [InlineData(Q16Algae, "fifty-nine")]
    // Digit puzzle — 24
    [InlineData(Q17Digits, "24")]
    [InlineData(Q17Digits, "twenty four")]
    [InlineData(Q17Digits, "twenty-four")]
    // Birthday problem — 23
    [InlineData(Q18Birthday, "23")]
    [InlineData(Q18Birthday, "23 people")]
    [InlineData(Q18Birthday, "twenty three")]
    [InlineData(Q18Birthday, "Twenty-Three")]
    // Handshakes — 36
    [InlineData(Q19Handshake, "36")]
    [InlineData(Q19Handshake, "36 handshakes")]
    [InlineData(Q19Handshake, "thirty six")]
    [InlineData(Q19Handshake, "thirty-six")]
    // Single-elimination bracket — 99
    [InlineData(Q20Bracket, "99")]
    [InlineData(Q20Bracket, "ninety nine")]
    [InlineData(Q20Bracket, "ninety-nine")]
    // Class average — either focal number is accepted
    [InlineData(Q21Average, "0")]
    [InlineData(Q21Average, "1")]
    [InlineData(Q21Average, "zero")]
    [InlineData(Q21Average, "one")]
    public void Accepts_intended_answer_second_batch(string storedAnswer, string submission) =>
        Assert.True(WithAnswer(storedAnswer).AcceptsAnswer(submission));

    [Theory]
    // Plausible near-misses for the second batch must still be rejected
    [InlineData(Q14Frog, "15")]        // forgetting the frog escapes before slipping back
    [InlineData(Q14Frog, "30")]
    [InlineData(Q15Desert, "3")]       // the file's original answer — 3 cannot cross
    [InlineData(Q15Desert, "2")]
    [InlineData(Q16Algae, "30")]       // "half the time" trap
    [InlineData(Q16Algae, "60")]
    [InlineData(Q17Digits, "42")]      // the reversed number, not the answer
    [InlineData(Q18Birthday, "183")]   // the "half of 365" trap
    [InlineData(Q18Birthday, "22")]
    [InlineData(Q19Handshake, "72")]   // double counting
    [InlineData(Q19Handshake, "81")]
    [InlineData(Q20Bracket, "100")]    // off by one
    [InlineData(Q20Bracket, "50")]
    [InlineData(Q21Average, "2")]
    [InlineData(Q21Average, "30")]
    public void Rejects_near_misses_second_batch(string storedAnswer, string submission) =>
        Assert.False(WithAnswer(storedAnswer).AcceptsAnswer(submission));

    [Theory]
    // Unrelated / wrong inputs are rejected for each teaser.
    [InlineData(Q1, "24")]
    [InlineData(Q1, "5")]      // a formula fragment must NOT count as correct
    [InlineData(Q2, "6000")]
    [InlineData(Q3, "46")]
    [InlineData(Q4, "5050")]
    [InlineData(Q5, "3")]
    [InlineData(Q6, "24")]
    [InlineData(Q6, "8")]      // a formula fragment must NOT count as correct
    [InlineData(Q7, "90")]
    [InlineData(Q8, "first")]
    [InlineData(Q9, "111221")]
    [InlineData(Q10, "50")]
    [InlineData(Q11, "60")]
    [InlineData(Q13, "10")]
    [InlineData(Q1, "")]       // blank is never accepted
    public void Rejects_wrong_answer(string storedAnswer, string submission) =>
        Assert.False(WithAnswer(storedAnswer).AcceptsAnswer(submission));
}
