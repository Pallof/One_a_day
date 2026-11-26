using OneADay.Models;

namespace OneADay.Tests;

/// <summary>
/// 24 Blitz's rules (PRD 07, issue 84): five minutes from Go, +1 a solve or right call, -1 a wrong
/// call, three passes scoring nothing, and nothing counting once time's up.
/// </summary>
public class BlitzGameTests
{
    private static readonly DateTime Go = new(2026, 11, 13, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_game_is_five_minutes_with_three_passes()
    {
        // The issue's numbers, written out rather than read back from the code's own constants.
        var game = new BlitzGame(Go);

        Assert.Equal(Go.AddSeconds(300), game.EndsAtUtc);
        Assert.Equal(3, game.PassesLeft);
        Assert.Equal(0, game.Score);
    }

    [Fact]
    public void Solves_and_right_calls_score_one_a_wrong_call_costs_one_and_the_score_can_go_below_zero()
    {
        var game = new BlitzGame(Go);

        game.Play(BlitzMove.CalledWrong, Go);
        Assert.Equal(-1, game.Score);
        game.Play(BlitzMove.CalledWrong, Go);
        Assert.Equal(-2, game.Score);
        game.Play(BlitzMove.Solved, Go);
        game.Play(BlitzMove.CalledRight, Go);
        game.Play(BlitzMove.Solved, Go);
        Assert.Equal(1, game.Score);
    }

    [Fact]
    public void Three_passes_score_nothing_and_a_fourth_isnt_taken()
    {
        var game = new BlitzGame(Go);

        Assert.True(game.Play(BlitzMove.Pass, Go));
        Assert.True(game.Play(BlitzMove.Pass, Go));
        Assert.True(game.Play(BlitzMove.Pass, Go));
        Assert.Equal((0, 0), (game.PassesLeft, game.Score));

        Assert.False(game.Play(BlitzMove.Pass, Go));
        Assert.Equal((0, 0), (game.PassesLeft, game.Score));
    }

    [Theory]
    [InlineData(BlitzMove.Solved)]
    [InlineData(BlitzMove.CalledRight)]
    [InlineData(BlitzMove.CalledWrong)]
    [InlineData(BlitzMove.Pass)]
    public void At_five_minutes_exactly_and_after_nothing_counts(BlitzMove move)
    {
        var game = new BlitzGame(Go);
        var justBefore = Go.AddMinutes(5).AddTicks(-1);

        Assert.False(game.IsOver(justBefore));
        Assert.True(game.Play(BlitzMove.Solved, justBefore));   // the last moment that counts

        foreach (var late in new[] { Go.AddMinutes(5), Go.AddMinutes(5).AddSeconds(1) })
        {
            Assert.True(game.IsOver(late));
            Assert.False(game.Play(move, late));
        }
        Assert.Equal((1, 3), (game.Score, game.PassesLeft));
    }

    [Fact]
    public void The_clock_counts_down_in_whole_seconds_and_stops_at_nothing()
    {
        var game = new BlitzGame(Go);

        Assert.Equal("5:00", BlitzGame.Clock(game.Left(Go)));
        Assert.Equal("4:59", BlitzGame.Clock(game.Left(Go.AddSeconds(1))));
        Assert.Equal("4:59", BlitzGame.Clock(game.Left(Go.AddSeconds(1.5))));   // a part-second left counts
        Assert.Equal("1:05", BlitzGame.Clock(game.Left(Go.AddSeconds(235))));
        Assert.Equal("0:01", BlitzGame.Clock(game.Left(Go.AddSeconds(299.2))));
        Assert.Equal("0:00", BlitzGame.Clock(game.Left(Go.AddMinutes(5))));
        Assert.Equal(TimeSpan.Zero, game.Left(Go.AddMinutes(7)));
    }
}
