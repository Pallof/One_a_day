using System.Text.Json;

namespace OneADay.Services;

/// <summary>Read-only snapshot of one teaser's answer statistics.</summary>
public record StatsSnapshot(int TotalSubmissions, int SuccessfulSubmissions);

/// <summary>
/// File-backed statistics per teaser: how many answers were submitted, and how many were
/// correct. Stored in App_Data/stats.json alongside the teasers.
/// </summary>
/// <remarks>
/// <para><b>Counts only — never who answered.</b> An earlier version also kept the anonymous id
/// of every visitor who tried each teaser, to show how many different people had taken it on
/// ("466 minds have taken on this challenge"). Those ids were never pruned, and every answer
/// rewrote the whole file, so recording one answer got slower for as long as the site ran —
/// roughly 9 MB of ids a year at 500 visitors a day. Replaced on 2026-09-22 by two numbers per
/// teaser (author's decision): the file now grows only when a teaser is written, never with
/// visitors.</para>
///
/// <para>The cost is that a count of <i>people</i> is no longer possible. Someone who answers
/// five times is counted five times — and re-solving is allowed on purpose, so a re-solve is
/// another successful attempt.</para>
///
/// <para>Files written by the old version still load: their <c>Attempters</c> lists are ignored
/// on read and gone after the next write.</para>
/// </remarks>
public class StatsStore
{
    private class TeaserStats
    {
        public int TotalSubmissions { get; set; }
        public int SuccessfulSubmissions { get; set; }
    }

    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly Dictionary<Guid, TeaserStats> _stats;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public StatsStore(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "stats.json");
        _stats = File.Exists(_filePath)
            ? JsonSerializer.Deserialize<Dictionary<Guid, TeaserStats>>(File.ReadAllText(_filePath), JsonOptions) ?? []
            : [];
    }

    public void RecordSubmission(Guid teaserId, bool success)
    {
        lock (_lock)
        {
            if (!_stats.TryGetValue(teaserId, out var stats))
            {
                stats = new TeaserStats();
                _stats[teaserId] = stats;
            }
            stats.TotalSubmissions++;
            if (success)
            {
                stats.SuccessfulSubmissions++;
            }
            Persist();
        }
    }

    public StatsSnapshot Get(Guid teaserId)
    {
        lock (_lock)
        {
            return _stats.TryGetValue(teaserId, out var stats)
                ? new StatsSnapshot(stats.TotalSubmissions, stats.SuccessfulSubmissions)
                : new StatsSnapshot(0, 0);
        }
    }

    /// <summary>Drop stats for a deleted teaser so the file doesn't collect orphans.</summary>
    public void Remove(Guid teaserId)
    {
        lock (_lock)
        {
            if (_stats.Remove(teaserId))
            {
                Persist();
            }
        }
    }

    private void Persist()
    {
        AtomicFile.WriteAllText(_filePath, JsonSerializer.Serialize(_stats, JsonOptions));
    }
}
