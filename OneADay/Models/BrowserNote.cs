namespace OneADay.Models;

/// <summary>When a browser last visited, relative to today. See PRD 16.</summary>
public enum VisitKind
{
    New,
    BackFromYesterday,

    /// <summary>Last visited 2–7 days ago.</summary>
    BackWithinWeek,

    BackAfterLonger,
}

/// <summary>
/// What one browser remembers about itself, kept encrypted in its own storage. The server
/// reads it to decide whether something is new — a first visit today, a first answer, a
/// first solve — and adds one to a daily total if so. The server keeps no copy (PRD 16:
/// "the browser remembers; the server only counts").
/// </summary>
/// <remarks>
/// Every transition is a pure function of the note and the day, so the counting rules are
/// tested here without a browser.
/// </remarks>
public sealed record BrowserNote
{
    /// <summary>The last Pacific day this browser counted as a visitor.</summary>
    public DateOnly? LastVisit { get; init; }

    /// <summary>The day and puzzle the fields below describe. Another day or puzzle starts them afresh.</summary>
    public DateOnly? PuzzleDay { get; init; }
    public Guid PuzzleId { get; init; }

    public bool SawPuzzle { get; init; }

    /// <summary>Answers made so far, across reloads. Stops counting once solved.</summary>
    public int Attempts { get; init; }

    public bool Solved { get; init; }

    /// <summary>The last day this browser submitted a Twenty Four answer.</summary>
    public DateOnly? PlayedTwentyFour { get; init; }

    /// <summary>
    /// Counts this browser as today's visitor, unless it already has been. A note dated after
    /// today means the clock went back; it has been counted already, so nothing changes.
    /// </summary>
    public (BrowserNote Note, VisitKind? Counted) Visit(DateOnly today)
    {
        if (LastVisit is { } last && last >= today)
        {
            return (this, null);
        }

        var kind = (today.DayNumber - LastVisit?.DayNumber) switch
        {
            null => VisitKind.New,
            1 => VisitKind.BackFromYesterday,
            <= 7 => VisitKind.BackWithinWeek,
            _ => VisitKind.BackAfterLonger,
        };
        return (this with { LastVisit = today }, kind);
    }

    /// <summary>The browser opened the challenge. True the first time today, for this puzzle.</summary>
    public (BrowserNote Note, bool Counted) SeePuzzle(DateOnly today, Guid puzzle)
    {
        var note = For(today, puzzle);
        return note.SawPuzzle ? (note, false) : (note with { SawPuzzle = true }, true);
    }

    /// <summary>
    /// The browser answered the challenge. Once it has solved, nothing more counts: coming back
    /// to solve again is allowed (PRD 06), but it isn't a second solve.
    /// </summary>
    public (BrowserNote Note, AnswerCounts Counted) Answer(DateOnly today, Guid puzzle, bool correct)
    {
        var note = For(today, puzzle);
        if (note.Solved)
        {
            return (note, AnswerCounts.Nothing);
        }

        var attempts = note.Attempts + 1;
        var counted = new AnswerCounts(
            Saw: !note.SawPuzzle,
            Tried: note.Attempts == 0,
            SolvedIn: correct ? attempts : null);

        return (note with { SawPuzzle = true, Attempts = attempts, Solved = correct }, counted);
    }

    /// <summary>The browser submitted a Twenty Four answer. True the first time today.</summary>
    public (BrowserNote Note, bool Counted) PlayTwentyFour(DateOnly today) =>
        PlayedTwentyFour == today
            ? (this, false)
            : (this with { PlayedTwentyFour = today }, true);

    private BrowserNote For(DateOnly today, Guid puzzle) =>
        PuzzleDay == today && PuzzleId == puzzle
            ? this
            : this with { PuzzleDay = today, PuzzleId = puzzle, SawPuzzle = false, Attempts = 0, Solved = false };
}

/// <summary>What one answer adds to the day's totals.</summary>
/// <param name="SolvedIn">The attempt count, when this answer was the first correct one.</param>
public sealed record AnswerCounts(bool Saw, bool Tried, int? SolvedIn)
{
    public static readonly AnswerCounts Nothing = new(false, false, null);
}
