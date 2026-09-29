using OneADay.Models;

namespace OneADay.Tests;

/// <summary>
/// The daily email allowance.
///
/// <para>This logic previously lived inside <c>EmailSenderService</c> and had no tests
/// at all — the only thing asserting anything about the cap checked that a default
/// value was in range, which would have passed with the enforcement deleted entirely.
/// It is a small piece of state with two easy ways to be wrong (counting the wrong
/// thing, and never rolling over), so it is worth pinning properly.</para>
/// </summary>
public class DailySendBudgetTests
{
    private static readonly DateOnly Monday = new(2026, 9, 7);
    private static readonly DateOnly Tuesday = new(2026, 9, 8);

    [Fact]
    public void A_fresh_budget_has_room()
    {
        var budget = new DailySendBudget(3);

        Assert.True(budget.HasRoom(Monday));
        Assert.Equal(0, budget.SentOn(Monday));
    }

    [Fact]
    public void Room_runs_out_after_the_cap_is_reached()
    {
        var budget = new DailySendBudget(3);

        for (var i = 0; i < 3; i++)
        {
            Assert.True(budget.HasRoom(Monday));
            budget.RecordSent(Monday);
        }

        Assert.False(budget.HasRoom(Monday));
        Assert.Equal(3, budget.SentOn(Monday));
    }

    [Fact]
    public void Merely_checking_for_room_spends_nothing()
    {
        // The bug this guards against: an earlier version incremented inside the
        // check, so asking "may I send?" consumed a slot whether or not anything
        // was ever sent.
        var budget = new DailySendBudget(2);

        for (var i = 0; i < 50; i++)
        {
            Assert.True(budget.HasRoom(Monday));
        }

        Assert.Equal(0, budget.SentOn(Monday));
    }

    // Failed sends costing nothing is the sender's policy — it records only deliveries —
    // so it's pinned against the sender: EmailNotifierTests.A_failed_send_spends_none_of_the_days_cap.

    // ---- the day boundary ------------------------------------------------------

    [Fact]
    public void A_new_day_restores_the_full_allowance()
    {
        var budget = new DailySendBudget(2);
        budget.RecordSent(Monday);
        budget.RecordSent(Monday);
        Assert.False(budget.HasRoom(Monday));

        Assert.True(budget.HasRoom(Tuesday));
        Assert.Equal(0, budget.SentOn(Tuesday));
    }

    [Fact]
    public void Yesterdays_total_is_not_carried_forward()
    {
        var budget = new DailySendBudget(10);
        budget.RecordSent(Monday);
        budget.RecordSent(Monday);
        budget.RecordSent(Monday);

        budget.RecordSent(Tuesday);

        Assert.Equal(1, budget.SentOn(Tuesday));
        Assert.Equal(0, budget.SentOn(Monday));   // the old day is gone, not remembered
    }

    [Fact]
    public void The_clock_going_backwards_does_not_grant_extra_sends()
    {
        // Not expected in production, but an NTP correction around midnight shouldn't hand
        // out a second allowance for a day already spent. Until the test audit of
        // 2026-09-28 it did: Tuesday, back to Monday, forward to Tuesday gave Tuesday a
        // fresh allowance, while this test's old assertions could never fail.
        var budget = new DailySendBudget(1);
        budget.RecordSent(Tuesday);

        Assert.False(budget.HasRoom(Monday));    // stepped back: still Tuesday's allowance
        Assert.False(budget.HasRoom(Tuesday));   // forward again: not a fresh one
        Assert.Equal(1, budget.SentOn(Tuesday));
    }

    // ---- degenerate configuration ----------------------------------------------

    [Fact]
    public void A_cap_of_zero_blocks_everything()
    {
        // A config typo of "MaxPerDay": 0 should stop mail rather than divide by zero
        // or send unlimited. Worth knowing it fails closed.
        var budget = new DailySendBudget(0);

        Assert.False(budget.HasRoom(Monday));
    }

    [Fact]
    public void A_cap_of_one_allows_exactly_one()
    {
        var budget = new DailySendBudget(1);

        Assert.True(budget.HasRoom(Monday));
        budget.RecordSent(Monday);
        Assert.False(budget.HasRoom(Monday));
    }
}
