using OneADay.Models;

namespace OneADay.Tests;

/// <summary>
/// The silent anti-automation checks on the suggestion form: a decoy field no person
/// can reach, and a floor on how fast a submission can plausibly be composed.
/// </summary>
public class SubmissionGuardTests
{
    private static readonly TimeSpan Unhurried = TimeSpan.FromMinutes(2);

    // ---- the honeypot ----------------------------------------------------------

    [Fact]
    public void An_untouched_decoy_field_lets_the_submission_through()
    {
        Assert.False(SubmissionGuard.LooksAutomated("", Unhurried));
        Assert.False(SubmissionGuard.LooksAutomated(null, Unhurried));
    }

    [Theory]
    [InlineData("http://spam.example")]
    [InlineData("x")]
    [InlineData("anything at all")]
    public void Anything_typed_into_the_decoy_blocks_the_submission(string filled)
    {
        Assert.True(SubmissionGuard.LooksAutomated(filled, Unhurried));
    }

    [Fact]
    public void Whitespace_alone_is_not_treated_as_filled()
    {
        // A stray space from an autofill pass shouldn't cost a real person their
        // suggestion — only actual content counts as a bot signature.
        Assert.False(SubmissionGuard.LooksAutomated("   ", Unhurried));
        Assert.False(SubmissionGuard.LooksAutomated("\t\n", Unhurried));
    }

    // ---- the timing floor ------------------------------------------------------

    [Fact]
    public void Submitting_faster_than_the_floor_is_blocked()
    {
        Assert.True(SubmissionGuard.LooksAutomated(null, TimeSpan.Zero));
        Assert.True(SubmissionGuard.LooksAutomated(null, TimeSpan.FromSeconds(1)));
        Assert.True(SubmissionGuard.LooksAutomated(null, TimeSpan.FromSeconds(4.9)));
    }

    [Fact]
    public void The_floor_is_five_seconds_and_the_boundary_is_allowed()
    {
        Assert.Equal(5, SubmissionGuard.MinComposeSeconds);
        Assert.False(SubmissionGuard.LooksAutomated(null, TimeSpan.FromSeconds(5)));
        Assert.False(SubmissionGuard.LooksAutomated(null, TimeSpan.FromSeconds(5.1)));
    }

    [Fact]
    public void A_visitor_who_takes_their_time_is_never_blocked()
    {
        foreach (var minutes in new[] { 1, 10, 60, 60 * 24 })
        {
            Assert.False(SubmissionGuard.LooksAutomated(null, TimeSpan.FromMinutes(minutes)));
        }
    }

    [Fact]
    public void An_unset_timestamp_fails_open_rather_than_eating_the_submission()
    {
        // If the form's shown-at stamp was never set, the elapsed span comes out
        // enormous. That must read as "took their time", not "blocked" — a filter
        // that silently drops real suggestions is worse than one that misses a bot.
        var elapsedFromDefault = DateTime.UtcNow - default(DateTime);
        Assert.False(SubmissionGuard.LooksAutomated(null, elapsedFromDefault));
    }

    // ---- the two together ------------------------------------------------------

    [Fact]
    public void Either_signal_alone_is_enough_to_block()
    {
        Assert.True(SubmissionGuard.LooksAutomated("bot", Unhurried));           // decoy only
        Assert.True(SubmissionGuard.LooksAutomated(null, TimeSpan.Zero));        // speed only
        Assert.True(SubmissionGuard.LooksAutomated("bot", TimeSpan.Zero));       // both
        Assert.False(SubmissionGuard.LooksAutomated(null, Unhurried));           // neither
    }
}
