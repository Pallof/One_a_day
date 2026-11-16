namespace OneADay.Models;

/// <summary>
/// One player's longest run on a Twenty Four scoreboard, or their best Blitz score, and when they
/// reached it.
/// </summary>
public sealed record BoardEntry(TwentyFourPlayer Player, int Run, DateTime ReachedAtUtc);

/// <summary>
/// The rules of a Twenty Four scoreboard (PRD 07): the ten longest runs, one row per player
/// holding their best, longest first. Of equal runs, whoever got there first ranks higher.
/// </summary>
public static class RunBoard
{
    public const int Size = 10;

    /// <summary>
    /// The board with this run on it, or null when the run changes nothing: it's below 1, no
    /// longer than the player's own row, or too short to make the ten.
    /// </summary>
    /// <remarks>
    /// <para>Returns a new list and never changes the one given, so a board read elsewhere stays as it was.</para>
    /// <para>The Blitz board keeps a game's score in <see cref="BoardEntry.Run"/> by the same rules. A
    /// Blitz score can be 0 or less, and none of those goes on, even onto an empty board.</para>
    /// </remarks>
    public static List<BoardEntry>? With(IReadOnlyList<BoardEntry> board, BoardEntry run)
    {
        if (run.Run < 1 || board.Any(e => e.Player.Id == run.Player.Id && e.Run >= run.Run))
        {
            return null;
        }

        var next = board
            .Where(e => e.Player.Id != run.Player.Id)
            .Append(run)
            .OrderByDescending(e => e.Run)
            .ThenBy(e => e.ReachedAtUtc)
            .Take(Size)
            .ToList();
        return next.Contains(run) ? next : null;
    }
}
