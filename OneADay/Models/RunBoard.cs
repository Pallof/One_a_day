namespace OneADay.Models;

/// <summary>One player's longest run on a Twenty Four scoreboard, and when they reached it.</summary>
public sealed record BoardEntry(TwentyFourPlayer Player, int Run, DateTime ReachedAtUtc);

/// <summary>
/// The rules of a Twenty Four scoreboard (PRD 07): the ten longest runs, one row per player
/// holding their best, longest first. Of equal runs, whoever got there first ranks higher.
/// </summary>
public static class RunBoard
{
    public const int Size = 10;

    /// <summary>
    /// The board with this run on it, or null when the run changes nothing: it's no longer than
    /// the player's own row, or too short to make the ten.
    /// </summary>
    /// <remarks>Returns a new list and never changes the one given, so a board read elsewhere stays as it was.</remarks>
    public static List<BoardEntry>? With(IReadOnlyList<BoardEntry> board, BoardEntry run)
    {
        if (board.Any(e => e.Player.Id == run.Player.Id && e.Run >= run.Run))
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
