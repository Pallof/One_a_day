using System.Text.RegularExpressions;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// Per-teaser answer statistics: how many answers came in and how many were right — and nothing
/// about who sent them.
///
/// <para>The store used to keep every visitor's anonymous id as well, to count people rather than
/// answers. The ids were never pruned and every answer rewrote the whole file, so recording an
/// answer got slower for as long as the site ran. These tests pin the replacement: two numbers
/// per teaser, a file that grows only with the number of teasers, and old files that still
/// load.</para>
/// </summary>
public sealed class StatsStoreTests : IDisposable
{
    private static readonly Regex AnyGuid =
        new("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

    private readonly TestEnvironment _env = new();

    [Fact]
    public void A_wrong_answer_counts_as_an_attempt_but_not_a_success()
    {
        var store = _env.NewStatsStore();
        var teaser = Guid.NewGuid();

        store.RecordSubmission(teaser, success: false);

        Assert.Equal(new StatsSnapshot(1, 0), store.Get(teaser));
    }

    [Fact]
    public void A_right_answer_counts_as_both()
    {
        var store = _env.NewStatsStore();
        var teaser = Guid.NewGuid();

        store.RecordSubmission(teaser, success: false);
        store.RecordSubmission(teaser, success: false);
        store.RecordSubmission(teaser, success: true);

        Assert.Equal(new StatsSnapshot(3, 1), store.Get(teaser));
    }

    [Fact]
    public void Each_teaser_keeps_its_own_counts()
    {
        var store = _env.NewStatsStore();
        var one = Guid.NewGuid();
        var two = Guid.NewGuid();

        store.RecordSubmission(one, success: true);
        store.RecordSubmission(two, success: false);
        store.RecordSubmission(two, success: false);

        Assert.Equal(new StatsSnapshot(1, 1), store.Get(one));
        Assert.Equal(new StatsSnapshot(2, 0), store.Get(two));
    }

    [Fact]
    public void A_teaser_nobody_has_answered_reads_as_zero()
    {
        Assert.Equal(new StatsSnapshot(0, 0), _env.NewStatsStore().Get(Guid.NewGuid()));
    }

    [Fact]
    public void Answering_again_counts_again()
    {
        // Re-solving the daily challenge is intended — the celebration is the point — so a repeat
        // is recorded like any other answer. With no ids there is no telling a repeat from a new
        // visitor, and that is the accepted trade.
        var store = _env.NewStatsStore();
        var teaser = Guid.NewGuid();

        store.RecordSubmission(teaser, success: true);
        store.RecordSubmission(teaser, success: true);

        Assert.Equal(new StatsSnapshot(2, 2), store.Get(teaser));
    }

    [Fact]
    public void Counts_survive_a_restart()
    {
        var teaser = Guid.NewGuid();
        var before = _env.NewStatsStore();
        before.RecordSubmission(teaser, success: false);
        before.RecordSubmission(teaser, success: true);

        var after = _env.NewStatsStore();   // a fresh store, reading the file back

        Assert.Equal(new StatsSnapshot(2, 1), after.Get(teaser));
    }

    [Fact]
    public void Nothing_about_who_answered_is_written()
    {
        // The point of the change: the file holds two numbers per teaser and no visitor ids, so it
        // grows with the number of teasers and never with visitors.
        var store = _env.NewStatsStore();
        store.RecordSubmission(Guid.NewGuid(), success: true);
        store.RecordSubmission(Guid.NewGuid(), success: false);

        var json = _env.ReadDataFile("stats.json");

        // Two teasers answered, so exactly two ids: their own keys. Any visitor id would add more.
        Assert.Equal(2, AnyGuid.Matches(json).Count);
        Assert.DoesNotContain("Attempters", json);
    }

    [Fact]
    public void A_file_from_the_old_version_still_loads_and_sheds_its_ids()
    {
        // Before 2026-09-22 each teaser also carried an Attempters list. Such a file must load with
        // its counts intact, and the ids must be gone after the next write.
        var teaser = Guid.NewGuid();
        _env.WriteDataFile("stats.json", $$"""
            {
              "{{teaser}}": {
                "TotalSubmissions": 7,
                "SuccessfulSubmissions": 3,
                "Attempters": [
                  "{{Guid.NewGuid()}}",
                  "{{Guid.NewGuid()}}",
                  "{{Guid.NewGuid()}}"
                ]
              }
            }
            """);

        var store = _env.NewStatsStore();
        Assert.Equal(new StatsSnapshot(7, 3), store.Get(teaser));

        store.RecordSubmission(teaser, success: true);

        Assert.Equal(new StatsSnapshot(8, 4), store.Get(teaser));
        var json = _env.ReadDataFile("stats.json");
        Assert.DoesNotContain("Attempters", json);
        Assert.Single(AnyGuid.Matches(json));   // only the teaser's own key survives
    }

    [Fact]
    public void Removing_a_teaser_drops_its_counts()
    {
        var store = _env.NewStatsStore();
        var teaser = Guid.NewGuid();
        store.RecordSubmission(teaser, success: true);

        store.Remove(teaser);

        Assert.Equal(new StatsSnapshot(0, 0), store.Get(teaser));
        Assert.Equal(new StatsSnapshot(0, 0), _env.NewStatsStore().Get(teaser));   // and on disk
    }

    public void Dispose() => _env.Dispose();
}
