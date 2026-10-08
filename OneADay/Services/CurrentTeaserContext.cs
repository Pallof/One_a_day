using OneADay.Models;

namespace OneADay.Services;

/// <summary>
/// Tracks the puzzle the visitor is currently looking at (per circuit) — a teaser, or a
/// Twenty Four hand — so an issue report can name the exact question or hand.
/// </summary>
public class CurrentTeaserContext
{
    public BrainTeaser? Current { get; private set; }

    /// <summary>The Twenty Four hand on screen, while the game is.</summary>
    public IReadOnlyList<int>? Hand => _hand;

    private int[]? _hand;

    public void Set(BrainTeaser? teaser) => Current = teaser;

    public void Clear(Guid teaserId)
    {
        if (Current?.Id == teaserId)
        {
            Current = null;
        }
    }

    public void SetHand(int[] hand) => _hand = hand;

    /// <summary>
    /// Forgets this hand only — the same rule as <see cref="Clear"/>: the page arriving may
    /// already have said what it shows before the one leaving clears up.
    /// </summary>
    public void ClearHand(int[] hand)
    {
        if (ReferenceEquals(_hand, hand))
        {
            _hand = null;
        }
    }
}
