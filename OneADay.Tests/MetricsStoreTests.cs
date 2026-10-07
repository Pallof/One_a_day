using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// metrics.json: totals that survive a restart, and nothing in it that says who (PRD 16).
/// </summary>
public class MetricsStoreTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 10, 6);
    private readonly TestEnvironment _env = new();

    public void Dispose() => _env.Dispose();

    [Fact]
    public void Counts_survive_a_restart()
    {
        var puzzle = Guid.NewGuid();
        using (var before = _env.NewMetricsStore())
        {
            before.RecordVisit(Day, VisitKind.New);
            before.RecordAnswer(Day, puzzle, new AnswerCounts(Saw: true, Tried: true, SolvedIn: 2));
        }   // shutting down saves what's left

        var after = _env.NewMetricsStore().Read().Days[MetricsData.Key(Day)];

        Assert.Equal(1, after.Visitors.New);
        Assert.Equal((1, 1, 1), (after.Puzzles[puzzle].Saw, after.Puzzles[puzzle].Tried, after.Puzzles[puzzle].Solved));
        Assert.Equal(new[] { 0, 1, 0, 0, 0 }, after.Puzzles[puzzle].SolvedIn);
    }

    // ---- the author's machine: the file is a download, and only the live site writes it ------

    /// <summary>The live site's file, as the download script would leave it.</summary>
    private (string Text, DateTime SavedAtUtc) DownloadFromLive(int confirmed)
    {
        _env.EnvironmentName = "Production";
        var live = _env.NewMetricsStore();
        live.RecordVisit(Day, VisitKind.New);
        live.SetSubscriberTotals(AppTime.Today, confirmed, pending: 4);
        live.Flush();
        _env.EnvironmentName = "Development";
        return (File.ReadAllText(MetricsFile), live.Read().SavedAtUtc!.Value);
    }

    [Fact]
    public void On_the_authors_machine_nothing_is_counted_or_saved()
    {
        _env.EnvironmentName = "Development";
        var store = _env.NewMetricsStore();

        store.RecordVisit(Day, VisitKind.New);
        store.RecordAnswer(Day, Guid.NewGuid(), new AnswerCounts(true, true, 1));
        store.SetSubscriberTotals(Day, 3, 1);
        store.Flush();
        store.Dispose();

        Assert.False(File.Exists(MetricsFile));
        Assert.Empty(store.Read().Days);
    }

    [Fact]
    public void On_the_authors_machine_admin_shows_the_latest_download_without_a_restart()
    {
        _env.EnvironmentName = "Development";
        var mac = _env.NewMetricsStore();   // started before anything was downloaded
        Assert.Empty(mac.Read().Days);

        var (_, savedOnLive) = DownloadFromLive(confirmed: 120);

        var shown = mac.Read();
        Assert.Equal(1, shown.Days[MetricsData.Key(Day)].Visitors.New);
        Assert.Equal(savedOnLive, shown.SavedAtUtc);
    }

    [Fact]
    public void The_authors_machine_never_saves_over_a_download()
    {
        // The review's case: the Mac started after a download, and its once-a-minute save
        // stamped its own time and its own (empty) subscriber list over the live numbers.
        var (downloaded, _) = DownloadFromLive(confirmed: 120);
        var mac = _env.NewMetricsStore();
        var macSubscribers = new SubscriberStore(_env, mac);

        new MetricsFlusher(mac, macSubscribers, NullLogger<MetricsFlusher>.Instance).SaveNow();
        mac.RecordVisit(Day, VisitKind.New);
        mac.Dispose();

        Assert.Equal(downloaded, File.ReadAllText(MetricsFile));
        Assert.Equal(120, Today(mac).Subscribers.Confirmed);
    }

    [Fact]
    public void Counting_touches_memory_and_only_a_flush_writes_the_file()
    {
        var store = _env.NewMetricsStore();
        store.RecordVisit(Day, VisitKind.New);

        Assert.False(File.Exists(MetricsFile));

        store.Flush();
        Assert.True(File.Exists(MetricsFile));
    }

    [Fact]
    public void Unchanged_list_totals_dont_rewrite_the_file()
    {
        var store = _env.NewMetricsStore();
        store.SetSubscriberTotals(Day, confirmed: 3, pending: 1);
        store.Flush();
        var firstSave = store.Read().SavedAtUtc;
        File.Delete(MetricsFile);

        store.SetSubscriberTotals(Day, confirmed: 3, pending: 1);   // the next minute's check
        store.Flush();

        Assert.False(File.Exists(MetricsFile));
        Assert.Equal(firstSave, store.Read().SavedAtUtc);

        store.SetSubscriberTotals(Day, confirmed: 4, pending: 1);
        store.Flush();
        Assert.True(File.Exists(MetricsFile));
    }

    [Fact]
    public void A_confirmation_counts_once_and_a_second_click_doesnt()
    {
        var metrics = _env.NewMetricsStore();
        var subscribers = new SubscriberStore(_env, metrics);
        var token = subscribers.Request("reader@example.com", DateTime.UtcNow).Token;

        Assert.True(subscribers.Confirm(token, DateTime.UtcNow));
        Assert.True(subscribers.Confirm(token, DateTime.UtcNow));   // the link clicked twice
        Assert.False(subscribers.Confirm("not-a-token", DateTime.UtcNow));

        Assert.Equal(1, Today(metrics).Subscribers.Confirmations);
    }

    [Fact]
    public void An_unsubscribe_counts_and_an_unknown_token_doesnt()
    {
        var metrics = _env.NewMetricsStore();
        var subscribers = new SubscriberStore(_env, metrics);
        var token = subscribers.Request("reader@example.com", DateTime.UtcNow).Token;

        Assert.False(subscribers.Unsubscribe("not-a-token"));
        Assert.True(subscribers.Unsubscribe(token));
        Assert.False(subscribers.Unsubscribe(token));   // already gone

        Assert.Equal(1, Today(metrics).Subscribers.Unsubscribes);
    }

    [Fact]
    public void The_list_reaches_the_file_as_totals_and_never_as_addresses()
    {
        var metrics = _env.NewMetricsStore();
        var subscribers = new SubscriberStore(_env, metrics);
        var confirmed = subscribers.Request("confirmed.reader@example.com", DateTime.UtcNow).Token;
        subscribers.Confirm(confirmed, DateTime.UtcNow);
        subscribers.Request("pending.reader@example.com", DateTime.UtcNow);

        new MetricsFlusher(metrics, subscribers, NullLogger<MetricsFlusher>.Instance).SaveNow();

        var day = Today(_env.NewMetricsStore());   // read back from the file
        Assert.Equal((1, 1), (day.Subscribers.Confirmed, day.Subscribers.Pending));
        Assert.DoesNotContain("@", File.ReadAllText(MetricsFile));
    }

    [Fact]
    public void The_file_holds_counts_and_nothing_that_says_who()
    {
        // Every way of counting, then a scan of what was written: every property must be one of
        // the known counters, and every value a number, a date or a puzzle id. A field that
        // smuggled in an address, an IP or an answer would fail here.
        var puzzle = Guid.NewGuid();
        var store = _env.NewMetricsStore();
        store.RecordVisit(Day, VisitKind.New);
        store.RecordVisit(Day, VisitKind.BackWithinWeek);
        store.RecordPuzzleSeen(Day, puzzle);
        store.RecordAnswer(Day, puzzle, new AnswerCounts(true, true, 4));
        store.RecordTwentyFourPlayer(Day);
        store.RecordTwentyFourHand(Day, solved: true);
        store.RecordTwentyFourHand(Day, solved: false);
        store.RecordConfirmation(Day);
        store.RecordUnsubscribe(Day);
        store.SetSubscriberTotals(Day, 5, 2);
        store.Flush();

        var known = new HashSet<string>
        {
            "SavedAtUtc", "Days", "Visitors", "New", "BackFromYesterday", "BackWithinWeek",
            "BackAfterLonger", "Puzzles", "Saw", "Tried", "Solved", "SolvedIn", "SolveAttempts",
            "TwentyFour", "Players", "HandsSolved", "HandsPassed", "Subscribers", "Confirmed",
            "Pending", "Confirmations", "Unsubscribes",
        };
        var dayKey = new Regex(@"^\d{4}-\d{2}-\d{2}$");

        void Check(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        Assert.True(
                            known.Contains(property.Name) || dayKey.IsMatch(property.Name)
                                || property.Name == puzzle.ToString(),
                            $"Unexpected field in metrics.json: {property.Name}");
                        Check(property.Value);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        Check(item);
                    }
                    break;
                case JsonValueKind.String:
                    Assert.True(DateTime.TryParse(element.GetString(), out _),
                        $"Unexpected text in metrics.json: {element.GetString()}");
                    break;
            }
        }

        using var json = JsonDocument.Parse(File.ReadAllText(MetricsFile));
        Check(json.RootElement);
    }

    private string MetricsFile => Path.Combine(_env.ContentRootPath, "App_Data", "metrics.json");

    private static DayMetrics Today(MetricsStore store) =>
        store.Read().Days[MetricsData.Key(AppTime.Today)];
}
