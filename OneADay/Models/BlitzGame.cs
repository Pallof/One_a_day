namespace OneADay.Models;

/// <summary>What a Blitz player did with the hand on the table.</summary>
public enum BlitzMove
{
    /// <summary>A right answer: +1.</summary>
    Solved,

    /// <summary>Impossible, on a hand that can't make 24: +1.</summary>
    CalledRight,

    /// <summary>Impossible, on a hand that can: -1.</summary>
    CalledWrong,

    /// <summary>A fresh hand for nothing, three a game.</summary>
    Pass,
}

/// <summary>
/// One game of 24 Blitz (PRD 07): five minutes from Go, +1 for each hand solved or rightly called
/// impossible, -1 for each wrong call, and three passes that score nothing.
/// </summary>
/// <remarks>
/// <para>Kept on the server, like the hand it's played on: the browser only ever sees the clock,
/// the score and the passes left, so nothing it sends can stretch the time, change the score or
/// pick the numbers. Anticheat, when it comes, can build on that.</para>
///
/// <para>The time is the caller's, read from the server's clock at each move, so a move that
/// arrives after the end doesn't count even if the page hasn't been told yet that time's up.</para>
/// </remarks>
public sealed class BlitzGame
{
    public static readonly TimeSpan Length = TimeSpan.FromMinutes(5);
    public const int Passes = 3;

    public BlitzGame(DateTime startedAtUtc) => EndsAtUtc = startedAtUtc + Length;

    public DateTime EndsAtUtc { get; }

    /// <summary>
    /// The points so far. It can go below zero, so a wrong call always costs something (author's
    /// decision, 2026-11-13); only a score of 1 or more goes on the board.
    /// </summary>
    public int Score { get; private set; }

    public int PassesLeft { get; private set; } = Passes;

    public bool IsOver(DateTime nowUtc) => nowUtc >= EndsAtUtc;

    /// <summary>The time left, never less than none.</summary>
    public TimeSpan Left(DateTime nowUtc) => IsOver(nowUtc) ? TimeSpan.Zero : EndsAtUtc - nowUtc;

    /// <summary>
    /// Counts a move. False, changing nothing, once time's up or for a pass when none are left.
    /// </summary>
    public bool Play(BlitzMove move, DateTime nowUtc)
    {
        if (IsOver(nowUtc) || (move == BlitzMove.Pass && PassesLeft == 0))
        {
            return false;
        }

        switch (move)
        {
            case BlitzMove.Solved:
            case BlitzMove.CalledRight:
                Score++;
                break;
            case BlitzMove.CalledWrong:
                Score--;
                break;
            case BlitzMove.Pass:
                PassesLeft--;
                break;
        }
        return true;
    }

    /// <summary>The clock as the page shows it, m:ss, counting a part-second left as a whole one.</summary>
    public static string Clock(TimeSpan left)
    {
        var seconds = (int)Math.Ceiling(left.TotalSeconds);
        return $"{seconds / 60}:{seconds % 60:00}";
    }
}
