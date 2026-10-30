using OneADay.Models;

namespace OneADay.Tests;

/// <summary>Spotting answers faster than anyone types (PRD 07): three in a row under 2 seconds.</summary>
public class AnswerPaceTests
{
    private static readonly TimeSpan Fast = TimeSpan.FromSeconds(1.2);
    private static readonly TimeSpan Human = TimeSpan.FromSeconds(6);

    [Fact]
    public void Three_fast_answers_in_a_row_are_caught()
    {
        var pace = new AnswerPace();

        Assert.False(pace.Answered(Fast));
        Assert.False(pace.Answered(Fast));
        Assert.True(pace.Answered(Fast));
        Assert.True(pace.Caught);
    }

    [Fact]
    public void One_slower_answer_starts_the_count_again()
    {
        // A fast player on two easy hands, then a harder one, then two more easy ones.
        var pace = new AnswerPace();

        foreach (var took in new[] { Fast, Fast, Human, Fast, Fast })
        {
            pace.Answered(took);
        }

        Assert.False(pace.Caught);
    }

    [Fact]
    public void Two_seconds_exactly_is_a_persons_pace()
    {
        var pace = new AnswerPace();
        var justUnder = TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(1);

        pace.Answered(TimeSpan.FromSeconds(2));
        pace.Answered(TimeSpan.FromSeconds(2));
        pace.Answered(TimeSpan.FromSeconds(2));
        Assert.False(pace.Caught);

        pace.Answered(justUnder);
        pace.Answered(justUnder);
        Assert.True(pace.Answered(justUnder));
    }

    [Fact]
    public void Once_caught_the_visit_stays_caught()
    {
        // A script that slows down after being caught doesn't get back on the board.
        var pace = new AnswerPace();
        pace.Answered(Fast);
        pace.Answered(Fast);
        pace.Answered(Fast);

        Assert.True(pace.Answered(Human));
        Assert.True(pace.Caught);
    }
}
