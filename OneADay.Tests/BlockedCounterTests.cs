using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The blocked-submission counters on both stores.
///
/// These exist because the anti-automation checks reject silently: with no count there
/// is no way to tell "nothing is attacking us" apart from "we are quietly eating real
/// submissions". A counter that silently stopped incrementing would restore exactly the
/// blindness it was added to remove, so it is worth pinning down.
/// </summary>
public class BlockedCounterTests
{
    // ---- suggestions -----------------------------------------------------------

    [Fact]
    public void Suggestion_counter_starts_at_zero()
    {
        using var env = new TestEnvironment();
        Assert.Equal(0, env.NewSuggestionStore().BlockedCount);
    }

    [Fact]
    public void Suggestion_counter_counts_every_block()
    {
        using var env = new TestEnvironment();
        var store = env.NewSuggestionStore();

        for (var i = 1; i <= 3; i++)
        {
            store.RecordBlocked();
            Assert.Equal(i, store.BlockedCount);
        }
    }

    [Fact]
    public void Suggestion_counter_survives_a_restart()
    {
        using var env = new TestEnvironment();
        var first = env.NewSuggestionStore();
        first.RecordBlocked();
        first.RecordBlocked();

        // A fresh store over the same App_Data — as if the app had been redeployed.
        Assert.Equal(2, env.NewSuggestionStore().BlockedCount);
    }

    [Fact]
    public void Blocking_does_not_consume_anyones_daily_quota()
    {
        // The guard runs before the caps, so a bot being rejected must not spend the
        // allowance of a real person sharing that IP.
        using var env = new TestEnvironment();
        var store = env.NewSuggestionStore();
        var visitor = Guid.NewGuid();
        const string ip = "hash";
        var today = new DateOnly(2026, 8, 26);

        for (var i = 0; i < 10; i++)
        {
            store.RecordBlocked();
        }

        Assert.Equal(10, store.BlockedCount);
        Assert.False(store.HasReachedDailyLimit(visitor, ip, today));
        Assert.True(store.TryAdd(new TeaserSuggestion { Question = "q" }, visitor, ip, today));
    }

    [Fact]
    public void Blocking_does_not_add_anything_to_the_inbox()
    {
        using var env = new TestEnvironment();
        var store = env.NewSuggestionStore();

        store.RecordBlocked();

        Assert.Empty(store.GetAll());
    }

    // ---- issue reports ---------------------------------------------------------

    [Fact]
    public void Issue_counter_starts_at_zero_and_counts_every_block()
    {
        using var env = new TestEnvironment();
        var store = env.NewIssueStore();
        Assert.Equal(0, store.BlockedCount);

        store.RecordBlocked();
        store.RecordBlocked();

        Assert.Equal(2, store.BlockedCount);
        Assert.Empty(store.GetAll());       // nothing lands in the triage list
    }

    [Fact]
    public void Issue_counter_survives_a_restart()
    {
        using var env = new TestEnvironment();
        env.NewIssueStore().RecordBlocked();

        Assert.Equal(1, env.NewIssueStore().BlockedCount);
    }

    [Fact]
    public void Blocking_a_report_never_limits_a_real_one()
    {
        // Reports are deliberately uncapped — screening automation must not become a
        // back-door rate limit on people.
        using var env = new TestEnvironment();
        var store = env.NewIssueStore();

        for (var i = 0; i < 25; i++)
        {
            store.RecordBlocked();
        }
        for (var i = 0; i < 5; i++)
        {
            store.Add(new IssueReport { Details = $"report {i}" });
        }

        Assert.Equal(25, store.BlockedCount);
        Assert.Equal(5, store.GetAll().Count);
    }

    // ---- on-disk format --------------------------------------------------------

    [Fact]
    public void Issues_saved_in_the_old_bare_array_format_still_load()
    {
        // issues.json used to be a plain array of reports. Adding the counter wrapped
        // it in an object; the real file on disk is still the old shape, so loading it
        // must not silently drop anyone's reports.
        using var env = new TestEnvironment();
        env.WriteDataFile("issues.json", """
            [
              { "Id": "11111111-1111-1111-1111-111111111111",
                "Details": "the hint gives it away",
                "Category": "Other",
                "Status": "New" }
            ]
            """);

        var store = env.NewIssueStore();

        Assert.Single(store.GetAll());
        Assert.Equal("the hint gives it away", store.GetAll()[0].Details);
        Assert.Equal(0, store.BlockedCount);     // absent in the old format
    }

    [Fact]
    public void Migrating_the_old_format_preserves_reports_and_adds_the_counter()
    {
        using var env = new TestEnvironment();
        env.WriteDataFile("issues.json", """
            [ { "Id": "22222222-2222-2222-2222-222222222222", "Details": "keep me", "Status": "New" } ]
            """);

        var first = env.NewIssueStore();
        first.RecordBlocked();               // forces a write in the new shape

        var reloaded = env.NewIssueStore();
        Assert.Single(reloaded.GetAll());
        Assert.Equal("keep me", reloaded.GetAll()[0].Details);
        Assert.Equal(1, reloaded.BlockedCount);
        Assert.Contains("BlockedCount", env.ReadDataFile("issues.json"));
    }

    [Fact]
    public void Suggestions_saved_in_the_old_bare_array_format_still_load()
    {
        using var env = new TestEnvironment();
        env.WriteDataFile("suggestions.json", """
            [ { "Id": "33333333-3333-3333-3333-333333333333",
                "Question": "an older suggestion", "Difficulty": "Easy" } ]
            """);

        var store = env.NewSuggestionStore();

        Assert.Single(store.GetAll());
        Assert.Equal("an older suggestion", store.GetAll()[0].Question);
        Assert.Equal(0, store.BlockedCount);
    }
}
