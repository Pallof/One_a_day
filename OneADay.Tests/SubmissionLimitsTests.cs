using System.Text.Json;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The server's own limits on what visitors can store: field lengths, and a ceiling on how many
/// reports and suggestions are kept.
///
/// <para>The stakes (security review, 2026-09-28): both lists live in memory and every save
/// rewrites the whole file. Unlimited, a script filing oversized reports over the live connection
/// could run the 512 MB server out of memory in minutes, and the file would bring it straight
/// back down on restart. The component tests for the same limits are in
/// NotificationGatingTests.</para>
/// </summary>
public class SubmissionLimitsTests
{
    private static readonly DateOnly Today = new(2026, 9, 28);

    // ---- field lengths ---------------------------------------------------------------

    [Fact]
    public void Text_within_the_limit_is_only_trimmed()
    {
        Assert.Equal("a riddle", TextLimit.Clip("  a riddle \n", 600));
        Assert.Equal(new string('x', 600), TextLimit.Clip(new string('x', 600), 600));
        Assert.Equal(string.Empty, TextLimit.Clip(null, 600));
    }

    [Fact]
    public void Text_over_the_limit_is_cut_to_it()
    {
        Assert.Equal(new string('x', 600), TextLimit.Clip(new string('x', 40_000), 600));
    }

    [Fact]
    public void A_cut_never_splits_an_emoji_in_half()
    {
        // 🧠 is two characters to .NET. Cutting between them would store half a character.
        var text = new string('x', 599) + "🧠🧠";

        var clipped = TextLimit.Clip(text, 600);

        Assert.Equal(new string('x', 599), clipped);
        Assert.False(char.IsHighSurrogate(clipped[^1]));
    }

    // ---- the report ceiling ----------------------------------------------------------

    private static void SeedOpenReports(TestEnvironment env, int count) =>
        env.WriteDataFile("issues.json", JsonSerializer.Serialize(
            Enumerable.Range(0, count).Select(i => new IssueReport { Details = $"open {i}" })));

    [Fact]
    public void Below_the_ceiling_every_report_is_kept()
    {
        // The control: one under the ceiling still saves.
        using var env = new TestEnvironment();
        SeedOpenReports(env, IssueStore.MaxOpenReports - 1);
        var store = env.NewIssueStore();

        Assert.True(store.Add(new IssueReport { Details = "the last one that fits" }));
        Assert.Equal(IssueStore.MaxOpenReports, store.GetAll().Count);
    }

    [Fact]
    public void At_the_ceiling_a_new_report_is_not_saved()
    {
        using var env = new TestEnvironment();
        SeedOpenReports(env, IssueStore.MaxOpenReports);
        var store = env.NewIssueStore();

        Assert.False(store.Add(new IssueReport { Details = "one too many" }));
        Assert.Equal(IssueStore.MaxOpenReports, store.GetAll().Count);
        Assert.DoesNotContain("one too many", env.ReadDataFile("issues.json"));
    }

    [Fact]
    public void Closing_a_report_makes_room()
    {
        // Only open reports count: closed ones are the author's own history.
        using var env = new TestEnvironment();
        SeedOpenReports(env, IssueStore.MaxOpenReports);
        var store = env.NewIssueStore();

        store.SetStatus(store.GetAll()[0].Id, IssueStatus.Solved);

        Assert.True(store.Add(new IssueReport { Details = "room again" }));
    }

    // ---- the suggestion ceiling ------------------------------------------------------

    private static void SeedSuggestions(TestEnvironment env, int count) =>
        env.WriteDataFile("suggestions.json", JsonSerializer.Serialize(
            Enumerable.Range(0, count).Select(i => new TeaserSuggestion { Question = $"waiting {i}" })));

    [Fact]
    public void Below_the_ceiling_a_suggestion_is_kept()
    {
        using var env = new TestEnvironment();
        SeedSuggestions(env, SuggestionStore.MaxStored - 1);
        var store = env.NewSuggestionStore();

        Assert.Equal(SuggestionOutcome.Added,
            store.Add(new TeaserSuggestion { Question = "fits" }, Guid.NewGuid(), "hash", Today));
    }

    [Fact]
    public void At_the_ceiling_a_suggestion_is_not_saved_and_spends_no_allowance()
    {
        // Turned away for the box being full, not for anything the visitor did: their one a day
        // must still be there once there's room.
        using var env = new TestEnvironment();
        SeedSuggestions(env, SuggestionStore.MaxStored);
        var store = env.NewSuggestionStore();
        var visitor = Guid.NewGuid();

        var outcome = store.Add(new TeaserSuggestion { Question = "one too many" }, visitor, "hash", Today);

        Assert.Equal(SuggestionOutcome.InboxFull, outcome);
        Assert.Equal(SuggestionStore.MaxStored, store.GetAll().Count);
        Assert.False(store.HasReachedDailyLimit(visitor, "hash", Today));

        store.Delete(store.GetAll()[0].Id);
        Assert.Equal(SuggestionOutcome.Added,
            store.Add(new TeaserSuggestion { Question = "room again" }, visitor, "hash", Today));
    }

    [Fact]
    public void The_daily_limit_still_answers_first()
    {
        // Someone over their allowance is told that, whatever the state of the box.
        using var env = new TestEnvironment();
        var store = env.NewSuggestionStore();
        var visitor = Guid.NewGuid();
        store.Add(new TeaserSuggestion { Question = "today's" }, visitor, "hash", Today);

        Assert.Equal(SuggestionOutcome.OverDailyLimit,
            store.Add(new TeaserSuggestion { Question = "again" }, visitor, "hash", Today));
    }
}
