using OneADay.Models;

namespace OneADay.Tests;

/// <summary>
/// The recycling box: draws without replacement, refills while a few slips remain,
/// and holds recent outings back so nothing reappears immediately after a refill.
/// </summary>
public class TeaserRotationTests
{
    private static List<Guid> Bank(int n) => Enumerable.Range(0, n).Select(_ => Guid.NewGuid()).ToList();

    [Fact]
    public void Threshold_is_a_fifth_of_the_bank()
    {
        Assert.Equal(4, TeaserRotation.RefillThreshold(20));   // the user's 20-question example
        Assert.Equal(5, TeaserRotation.RefillThreshold(25));
        Assert.Equal(1, TeaserRotation.RefillThreshold(1));    // never zero, or the box could empty
        Assert.Equal(1, TeaserRotation.RefillThreshold(0));
    }

    [Fact]
    public void An_empty_bank_draws_nothing()
    {
        Assert.Null(TeaserRotation.Draw([], [], [], new Random(1)));
    }

    [Fact]
    public void First_draw_fills_the_box_from_the_whole_bank()
    {
        var bank = Bank(20);
        var result = TeaserRotation.Draw([], bank, [], new Random(1));

        Assert.NotNull(result);
        Assert.True(result.Refilled);
        Assert.Contains(result.Drawn, bank);
        Assert.Equal(19, result.Box.Count);            // one taken out
        Assert.DoesNotContain(result.Drawn, result.Box);
    }

    [Fact]
    public void A_cycle_never_repeats_before_the_refill()
    {
        // Draw repeatedly and confirm no teaser comes round twice within a cycle.
        var bank = Bank(20);
        var box = new List<Guid>();
        var random = new Random(7);
        var seenThisCycle = new List<Guid>();

        for (var day = 0; day < 16; day++)
        {
            var result = TeaserRotation.Draw(box, bank, [], random)!;
            if (result.Refilled)
            {
                seenThisCycle.Clear();      // a new cycle started
            }
            Assert.DoesNotContain(result.Drawn, seenThisCycle);
            seenThisCycle.Add(result.Drawn);
            box = result.Box;
        }
    }

    [Fact]
    public void Refills_while_slips_remain_rather_than_on_empty()
    {
        var bank = Bank(20);
        var box = new List<Guid>();
        var random = new Random(3);
        var refillSizes = new List<int>();

        for (var day = 0; day < 40; day++)
        {
            var before = box.Count;
            var result = TeaserRotation.Draw(box, bank, [], random)!;
            if (result.Refilled && day > 0)
            {
                refillSizes.Add(before);    // how many were left when it refilled
            }
            box = result.Box;
        }

        Assert.NotEmpty(refillSizes);
        // The box must never have been allowed to run dry before refilling.
        Assert.All(refillSizes, left => Assert.InRange(left, 1, TeaserRotation.RefillThreshold(20)));
    }

    [Fact]
    public void Recently_shown_teasers_are_held_back()
    {
        var bank = Bank(20);
        // Pretend the four most recent days used these; a fresh box would otherwise
        // make them immediately drawable again.
        var recent = bank.Take(4).ToList();

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var result = TeaserRotation.Draw([], bank, recent, new Random(attempt))!;
            Assert.DoesNotContain(result.Drawn, recent);
        }
    }

    [Fact]
    public void A_tiny_bank_still_produces_a_draw()
    {
        // With one teaser, every candidate is also "recent" — showing it again beats
        // showing nothing at all.
        var bank = Bank(1);
        var result = TeaserRotation.Draw([], bank, bank, new Random(1));
        Assert.NotNull(result);
        Assert.Equal(bank[0], result.Drawn);
    }

    [Fact]
    public void Deleted_teasers_are_never_drawn()
    {
        var bank = Bank(10);
        var stale = Guid.NewGuid();                 // in the box, but deleted from the bank
        var box = new List<Guid>(bank) { stale };

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var result = TeaserRotation.Draw(box, bank, [], new Random(attempt))!;
            Assert.NotEqual(stale, result.Drawn);
        }
    }

    [Fact]
    public void Newly_added_teasers_join_at_the_next_refill()
    {
        var bank = Bank(5);
        var box = new List<Guid>();
        var random = new Random(11);

        // burn through a cycle
        for (var i = 0; i < 4; i++)
        {
            box = TeaserRotation.Draw(box, bank, [], random)!.Box;
        }

        // the author adds a new teaser mid-cycle
        var newcomer = Guid.NewGuid();
        bank.Add(newcomer);

        var drawnLater = new List<Guid>();
        for (var i = 0; i < 30; i++)
        {
            var result = TeaserRotation.Draw(box, bank, [], random)!;
            drawnLater.Add(result.Drawn);
            box = result.Box;
        }

        Assert.Contains(newcomer, drawnLater);
    }

    [Fact]
    public void Draws_are_spread_across_the_bank_not_stuck_on_one()
    {
        var bank = Bank(10);
        var box = new List<Guid>();
        var random = new Random(2026);
        var counts = bank.ToDictionary(id => id, _ => 0);

        for (var day = 0; day < 500; day++)
        {
            var result = TeaserRotation.Draw(box, bank, [], random)!;
            counts[result.Drawn]++;
            box = result.Box;
        }

        // Every teaser should have had plenty of outings — no starvation, no hogging.
        Assert.All(counts.Values, c => Assert.InRange(c, 20, 90));
    }

    // ---- show-count weighting ---------------------------------------------------

    [Fact]
    public void Weight_falls_as_a_teaser_is_shown_more()
    {
        Assert.Equal(1.0, TeaserRotation.WeightFor(0));
        Assert.Equal(0.5, TeaserRotation.WeightFor(1));
        Assert.Equal(0.2, TeaserRotation.WeightFor(4));
        // a never-shown teaser is five times likelier than one shown four times
        Assert.Equal(5.0, TeaserRotation.WeightFor(0) / TeaserRotation.WeightFor(4), 6);
    }

    [Fact]
    public void A_neglected_teaser_is_favoured_over_well_worn_ones()
    {
        var bank = Bank(5);
        var neglected = bank[0];
        // everyone else has been shown nine times; the neglected one never
        var counts = bank.ToDictionary(id => id, id => id == neglected ? 0 : 9);

        var random = new Random(1234);
        var picks = 0;
        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var result = TeaserRotation.Draw([], bank, [], random, counts)!;
            if (result.Drawn == neglected) { picks++; }
        }

        // weights are 1.0 vs 0.1 x4, so it should win roughly 71% of the time
        Assert.InRange(picks, 600, 820);
    }

    [Fact]
    public void Weighting_closes_the_gap_over_time_rather_than_starving_anyone()
    {
        // The scenario that motivated weighting: the box refills at 20% remaining, so
        // some teasers sit out cycles. Over a long run nobody should be left behind.
        var bank = Bank(20);
        var box = new List<Guid>();
        var counts = bank.ToDictionary(id => id, _ => 0);
        var random = new Random(4242);

        for (var day = 0; day < 2000; day++)
        {
            var result = TeaserRotation.Draw(box, bank, [], random, counts)!;
            counts[result.Drawn]++;
            box = result.Box;
        }

        var fewest = counts.Values.Min();
        var most = counts.Values.Max();
        // 2000 days over 20 teasers averages 100 each; the spread should stay tight.
        Assert.InRange(fewest, 70, 100);
        Assert.True(most - fewest < 45, $"spread too wide: {fewest}..{most}");
    }

    [Fact]
    public void Unweighted_draws_still_work_when_no_counts_are_supplied()
    {
        var bank = Bank(6);
        var result = TeaserRotation.Draw([], bank, [], new Random(1));
        Assert.NotNull(result);
        Assert.Contains(result.Drawn, bank);
    }

    [Fact]
    public void Draw_frequencies_match_the_declared_weights()
    {
        // The direct question: over many draws, does each teaser come up as often as
        // WeightFor says it should? Each draw starts from a fresh box with no cooldown,
        // isolating the weighting from the without-replacement behaviour.
        const int draws = 40_000;
        var bank = Bank(4);
        var counts = new Dictionary<Guid, int>
        {
            [bank[0]] = 0,   // weight 1.000
            [bank[1]] = 1,   // weight 0.500
            [bank[2]] = 3,   // weight 0.250
            [bank[3]] = 7,   // weight 0.125
        };

        var totalWeight = bank.Sum(id => TeaserRotation.WeightFor(counts[id]));
        var observed = bank.ToDictionary(id => id, _ => 0);
        var random = new Random(20260821);

        for (var i = 0; i < draws; i++)
        {
            observed[TeaserRotation.Draw([], bank, [], random, counts)!.Drawn]++;
        }

        foreach (var id in bank)
        {
            var expectedShare = TeaserRotation.WeightFor(counts[id]) / totalWeight;
            var actualShare = (double)observed[id] / draws;
            Assert.True(Math.Abs(actualShare - expectedShare) < 0.02,
                $"shown {counts[id]}x: expected ~{expectedShare:P1} of draws, saw {actualShare:P1}");
        }
    }

    [Fact]
    public void Equal_counts_leave_the_draw_uniform()
    {
        // Weighting must only kick in when counts actually differ — a bank that has
        // been shown evenly should see no bias at all.
        const int draws = 20_000;
        var bank = Bank(4);
        var counts = bank.ToDictionary(id => id, _ => 5);
        var observed = bank.ToDictionary(id => id, _ => 0);
        var random = new Random(99);

        for (var i = 0; i < draws; i++)
        {
            observed[TeaserRotation.Draw([], bank, [], random, counts)!.Drawn]++;
        }

        Assert.All(observed.Values, c =>
            Assert.True(Math.Abs((double)c / draws - 0.25) < 0.02,
                $"expected ~25% each, saw {(double)c / draws:P1}"));
    }

    [Fact]
    public void Supplying_counts_visibly_changes_the_outcome_versus_not()
    {
        // Guards against the counts being accepted but quietly ignored: the same seed
        // and bank must produce a different spread once weights are in play.
        const int draws = 10_000;
        var bank = Bank(4);
        var lopsided = new Dictionary<Guid, int>
        {
            [bank[0]] = 0, [bank[1]] = 20, [bank[2]] = 20, [bank[3]] = 20,
        };

        double ShareOfFirst(IReadOnlyDictionary<Guid, int>? counts)
        {
            var random = new Random(555);
            var hits = 0;
            for (var i = 0; i < draws; i++)
            {
                if (TeaserRotation.Draw([], bank, [], random, counts)!.Drawn == bank[0]) { hits++; }
            }
            return (double)hits / draws;
        }

        var unweighted = ShareOfFirst(null);
        var weighted = ShareOfFirst(lopsided);

        Assert.True(Math.Abs(unweighted - 0.25) < 0.02, $"unweighted should be ~25%, was {unweighted:P1}");
        // weights 1 vs 0.0476 x3 -> the neglected one should dominate
        Assert.True(weighted > 0.80, $"weighted should exceed 80%, was {weighted:P1}");
    }
}
