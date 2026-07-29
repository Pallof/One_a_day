using OneADay.Models;

namespace OneADay.Services;

/// <summary>
/// Tracks the teaser the visitor is currently looking at (per circuit), so an
/// issue report filed from a challenge page can name the exact question.
/// </summary>
public class CurrentTeaserContext
{
    public BrainTeaser? Current { get; private set; }

    public void Set(BrainTeaser? teaser) => Current = teaser;

    public void Clear(Guid teaserId)
    {
        if (Current?.Id == teaserId)
        {
            Current = null;
        }
    }
}
