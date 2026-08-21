namespace OneADay.Models;

/// <summary>
/// The "box" that recycles past teasers once the queue of new ones runs dry.
///
/// Draws are made without replacement — like pulling slips from a box — so a teaser
/// cannot come round again until the box is refilled. The box refills while a few
/// slips still remain rather than on empty, which keeps the last few draws of a cycle
/// from becoming forced and predictable.
/// </summary>
public static class TeaserRotation
{
    /// <summary>Refill once the box is down to this share of the full bank.</summary>
    public const double RefillAtFraction = 0.20;

    /// <summary>
    /// How many of the most recently shown teasers are held back from a draw.
    /// Refilling early means a teaser shown days ago is immediately eligible again;
    /// this stops one reappearing right on the heels of its last outing.
    /// </summary>
    public const double CooldownFraction = 0.20;

    public static int RefillThreshold(int bankSize) =>
        Math.Max(1, (int)Math.Ceiling(bankSize * RefillAtFraction));

    public static int CooldownSize(int bankSize) =>
        Math.Max(0, (int)Math.Floor(bankSize * CooldownFraction));

    /// <summary>Roulette-wheel selection: chance is proportional to <see cref="WeightFor"/>.</summary>
    private static Guid PickWeighted(
        List<Guid> candidates, IReadOnlyDictionary<Guid, int>? showCounts, Random random)
    {
        if (showCounts is null || candidates.Count == 1)
        {
            return candidates[random.Next(candidates.Count)];
        }

        var weights = candidates
            .Select(id => WeightFor(showCounts.TryGetValue(id, out var n) ? n : 0))
            .ToList();

        var roll = random.NextDouble() * weights.Sum();
        for (var i = 0; i < candidates.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0)
            {
                return candidates[i];
            }
        }
        return candidates[^1];   // floating-point slack on the final slice
    }

    public sealed record DrawResult(Guid Drawn, List<Guid> Box, bool Refilled);

    /// <summary>
    /// How strongly a teaser's past appearances suppress its chance of being drawn.
    /// Weight is 1/(1 + shows), so a never-shown teaser is twice as likely as one
    /// shown once, and five times as likely as one shown four times.
    /// </summary>
    /// <remarks>
    /// This matters because the box refills while slips remain: roughly a fifth of the
    /// bank sits out any given cycle, and without weighting the same teasers could keep
    /// drawing the short straw and go months unseen. Weighting makes a skipped teaser
    /// steadily more likely until it comes up — self-correcting, with no bookkeeping
    /// beyond a counter.
    ///
    /// A "reroll on a high count" would achieve something similar (it is rejection
    /// sampling), but it can loop unboundedly and its real odds are hard to reason
    /// about. Drawing directly from the weights is one pass and exactly specified.
    /// </remarks>
    public static double WeightFor(int timesShown) => 1.0 / (1 + Math.Max(0, timesShown));

    /// <summary>
    /// Takes one teaser out of the box, refilling first if it has run low.
    /// </summary>
    /// <param name="box">What is still in the box from the current cycle.</param>
    /// <param name="bank">Every teaser eligible for recycling, newest additions included.</param>
    /// <param name="recentlyShown">Most-recent-first; the head of this list is held back.</param>
    /// <returns>The drawn teaser and the box that remains, or null if the bank is empty.</returns>
    public static DrawResult? Draw(
        IEnumerable<Guid> box,
        IReadOnlyList<Guid> bank,
        IReadOnlyList<Guid> recentlyShown,
        Random random,
        IReadOnlyDictionary<Guid, int>? showCounts = null)
    {
        if (bank.Count == 0)
        {
            return null;
        }

        // Teasers deleted since the box was filled must not be drawable.
        var live = new HashSet<Guid>(bank);
        var working = box.Where(live.Contains).Distinct().ToList();

        // Refill *before* drawing so the box is never empty at the point of use.
        // Refilling replaces rather than appends — the few leftover slips are part
        // of the fresh set, not duplicates of it.
        var refilled = false;
        if (working.Count <= RefillThreshold(bank.Count))
        {
            working = bank.ToList();
            refilled = true;
        }

        // Hold back the most recent outings when we can still afford to.
        var cooldown = recentlyShown.Take(CooldownSize(bank.Count)).ToHashSet();
        var candidates = working.Where(id => !cooldown.Contains(id)).ToList();
        if (candidates.Count == 0)
        {
            // A small bank can make every remaining slip "recent"; showing something
            // beats showing nothing.
            candidates = working;
        }

        var drawn = PickWeighted(candidates, showCounts, random);
        working.Remove(drawn);
        return new DrawResult(drawn, working, refilled);
    }
}
