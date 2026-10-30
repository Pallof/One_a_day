namespace OneADay.Models;

/// <summary>
/// Spots a Twenty Four player answering faster than anyone types, the mark of a script
/// (PRD 07). One per visit.
/// </summary>
/// <remarks>
/// <para>Timed on the server, from the deal to the answer, so a script can't claim it took
/// longer. Only answers that add to the run count: a correct answer or a right Impossible call.
/// The time the hand takes to reach the browser only adds to a person's time.</para>
///
/// <para>One fast answer is a person on an easy hand. Three in a row is not: reading four cards,
/// working it out and typing even <c>6+6+6+6</c> takes a fast player about 1.5 to 2 seconds.
/// One slower answer starts the count again; a Pass or a wrong answer doesn't, so a script
/// can't reset it by passing.</para>
///
/// <para>Once caught, the visit stays caught. What follows is the board's business, and
/// nothing on the page changes, so the player can't tell (author's decision, 2026-10-29).</para>
/// </remarks>
public sealed class AnswerPace
{
    /// <summary>An answer sooner than this after the deal is too fast to have been typed.</summary>
    public static readonly TimeSpan TooFast = TimeSpan.FromSeconds(2);

    /// <summary>How many too-fast answers in a row it takes.</summary>
    public const int InARow = 3;

    private int _fastInARow;

    public bool Caught { get; private set; }

    /// <summary>One answer that added to the run, this long after its hand was dealt. True once caught.</summary>
    public bool Answered(TimeSpan took)
    {
        _fastInARow = took < TooFast ? _fastInARow + 1 : 0;
        Caught |= _fastInARow >= InARow;
        return Caught;
    }
}
