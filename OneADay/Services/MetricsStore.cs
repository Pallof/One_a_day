using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using OneADay.Models;

namespace OneADay.Services;

/// <summary>One day's totals. Counts only — nothing here says who.</summary>
public sealed class DayMetrics
{
    public VisitorCounts Visitors { get; set; } = new();
    public Dictionary<Guid, PuzzleCounts> Puzzles { get; set; } = [];
    public TwentyFourCounts TwentyFour { get; set; } = new();
    public SubscriberCounts Subscribers { get; set; } = new();
}

public sealed class VisitorCounts
{
    public int New { get; set; }
    public int BackFromYesterday { get; set; }
    public int BackWithinWeek { get; set; }
    public int BackAfterLonger { get; set; }

    [JsonIgnore]
    public int Total => New + BackFromYesterday + BackWithinWeek + BackAfterLonger;
}

/// <summary>One puzzle's funnel on one day.</summary>
public sealed class PuzzleCounts
{
    public int Saw { get; set; }
    public int Tried { get; set; }
    public int Solved { get; set; }

    /// <summary>Solves by attempt count: 1, 2, 3, 4, and 5 or more.</summary>
    public int[] SolvedIn { get; set; } = new int[MetricsStore.AttemptBuckets];

    /// <summary>Attempts summed over every solve, so the average is exact despite the 5+ bucket.</summary>
    public int SolveAttempts { get; set; }
}

public sealed class TwentyFourCounts
{
    /// <summary>Browsers that submitted at least one answer that day.</summary>
    public int Players { get; set; }
    public int HandsSolved { get; set; }
    public int HandsPassed { get; set; }
}

public sealed class SubscriberCounts
{
    /// <summary>The list's size at the last save that day; null on a day nobody saved one.</summary>
    public int? Confirmed { get; set; }
    public int? Pending { get; set; }

    public int Confirmations { get; set; }
    public int Unsubscribes { get; set; }
}

/// <summary>Everything in metrics.json.</summary>
public sealed class MetricsData
{
    /// <summary>When the live site last saved — how fresh a downloaded copy is.</summary>
    public DateTime? SavedAtUtc { get; set; }

    /// <summary>Keyed by Pacific day, "yyyy-MM-dd".</summary>
    public SortedDictionary<string, DayMetrics> Days { get; set; } = new(StringComparer.Ordinal);

    public static string Key(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
/// Daily site totals for admin's Metrics section (PRD 16), in App_Data/metrics.json.
/// </summary>
/// <remarks>
/// <para><b>Totals only.</b> Each browser keeps its own note of what it has done
/// (<see cref="BrowserNote"/>) and this store only adds one when the note says something is
/// new. No visitor id, address or answer text is ever stored, so the file grows with days,
/// not visitors — the problem that ended the old people-count in stats.json (PRD 06).</para>
///
/// <para><b>Saved in batches.</b> Counting happens on visitors' clicks, so it only touches
/// memory; <see cref="MetricsFlusher"/> saves about once a minute, and disposing saves
/// whatever is left, so a restart loses at most the last minute.</para>
///
/// <para><b>Only the live site writes the file.</b> On the author's machine — wherever admin
/// exists (<see cref="AdminAccess.IsAvailable"/>) — the file is a download of the live
/// site's, so nothing is counted and nothing saved there, and every read comes fresh from
/// disk. Before this, the Mac's own copy of the site stamped its save time and its test
/// subscriber totals over the downloaded numbers on every start (code review, 2026-10-07).</para>
/// </remarks>
public sealed class MetricsStore : IDisposable
{
    public const int AttemptBuckets = 5;

    private readonly string _filePath;
    private readonly bool _authorsMachine;
    private readonly object _lock = new();
    private readonly MetricsData _data;
    private bool _dirty;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public MetricsStore(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "metrics.json");
        _authorsMachine = AdminAccess.IsAvailable(env);
        _data = Load();
    }

    private MetricsData Load()
    {
        var data = File.Exists(_filePath)
            ? JsonSerializer.Deserialize<MetricsData>(File.ReadAllText(_filePath), JsonOptions) ?? new()
            : new MetricsData();
        data.Days = new SortedDictionary<string, DayMetrics>(data.Days, StringComparer.Ordinal);
        return data;
    }

    public void RecordVisit(DateOnly day, VisitKind kind) => Change(day, d =>
    {
        switch (kind)
        {
            case VisitKind.New: d.Visitors.New++; break;
            case VisitKind.BackFromYesterday: d.Visitors.BackFromYesterday++; break;
            case VisitKind.BackWithinWeek: d.Visitors.BackWithinWeek++; break;
            default: d.Visitors.BackAfterLonger++; break;
        }
    });

    public void RecordPuzzleSeen(DateOnly day, Guid puzzle) => Change(day, d => PuzzleIn(d, puzzle).Saw++);

    public void RecordAnswer(DateOnly day, Guid puzzle, AnswerCounts counted)
    {
        if (counted == AnswerCounts.Nothing)
        {
            return;
        }
        Change(day, d =>
        {
            var p = PuzzleIn(d, puzzle);
            if (counted.Saw) { p.Saw++; }
            if (counted.Tried) { p.Tried++; }
            if (counted.SolvedIn is { } attempts)
            {
                p.Solved++;
                p.SolvedIn[Math.Min(attempts, AttemptBuckets) - 1]++;
                p.SolveAttempts += attempts;
            }
        });
    }

    public void RecordTwentyFourPlayer(DateOnly day) => Change(day, d => d.TwentyFour.Players++);

    public void RecordTwentyFourHand(DateOnly day, bool solved) => Change(day, d =>
    {
        if (solved) { d.TwentyFour.HandsSolved++; } else { d.TwentyFour.HandsPassed++; }
    });

    public void RecordConfirmation(DateOnly day) => Change(day, d => d.Subscribers.Confirmations++);

    public void RecordUnsubscribe(DateOnly day) => Change(day, d => d.Subscribers.Unsubscribes++);

    /// <summary>
    /// The list's size, as totals. Addresses never come near this file. Taken every minute, so
    /// it only counts as a change when a number moved — otherwise a quiet site would rewrite
    /// the file every minute for nothing.
    /// </summary>
    public void SetSubscriberTotals(DateOnly day, int confirmed, int pending)
    {
        lock (_lock)
        {
            if (_data.Days.TryGetValue(MetricsData.Key(day), out var existing)
                && existing.Subscribers.Confirmed == confirmed
                && existing.Subscribers.Pending == pending)
            {
                return;
            }
            Change(day, d =>
            {
                d.Subscribers.Confirmed = confirmed;
                d.Subscribers.Pending = pending;
            });
        }
    }

    /// <summary>
    /// A copy of everything, for admin. Safe to read while visitors are being counted. On the
    /// author's machine it's the file as it is now — the latest download, however long the
    /// site has been running.
    /// </summary>
    public MetricsData Read()
    {
        lock (_lock)
        {
            return _authorsMachine
                ? Load()
                : JsonSerializer.Deserialize<MetricsData>(JsonSerializer.Serialize(_data, JsonOptions), JsonOptions)!;
        }
    }

    /// <summary>Saves if anything changed since the last save.</summary>
    public void Flush()
    {
        lock (_lock)
        {
            if (!_dirty)
            {
                return;
            }
            _data.SavedAtUtc = DateTime.UtcNow;
            AtomicFile.WriteAllText(_filePath, JsonSerializer.Serialize(_data, JsonOptions));
            _dirty = false;
        }
    }

    public void Dispose() => Flush();

    private void Change(DateOnly day, Action<DayMetrics> change)
    {
        if (_authorsMachine)
        {
            return;   // only the live site counts; see the remarks
        }
        lock (_lock)
        {
            var key = MetricsData.Key(day);
            if (!_data.Days.TryGetValue(key, out var metrics))
            {
                metrics = new DayMetrics();
                _data.Days[key] = metrics;
            }
            change(metrics);
            _dirty = true;
        }
    }

    private static PuzzleCounts PuzzleIn(DayMetrics day, Guid puzzle)
    {
        if (!day.Puzzles.TryGetValue(puzzle, out var counts))
        {
            counts = new PuzzleCounts();
            day.Puzzles[puzzle] = counts;
        }
        return counts;
    }
}
