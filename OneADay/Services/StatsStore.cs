using System.Text.Json;

namespace OneADay.Services;

/// <summary>Read-only snapshot of one teaser's attempt statistics.</summary>
public record StatsSnapshot(int UniqueAttempters, int TotalSubmissions, int SuccessfulSubmissions);

/// <summary>
/// File-backed statistics per teaser: who attempted (anonymous visitor ids),
/// how many submissions came in, and how many were correct.
/// Stored in App_Data/stats.json alongside the teasers.
/// </summary>
public class StatsStore
{
    private class TeaserStats
    {
        public int TotalSubmissions { get; set; }
        public int SuccessfulSubmissions { get; set; }
        public HashSet<Guid> Attempters { get; set; } = [];
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

    public void RecordSubmission(Guid teaserId, Guid visitorId, bool success)
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
            stats.Attempters.Add(visitorId);
            Persist();
        }
    }

    public StatsSnapshot Get(Guid teaserId)
    {
        lock (_lock)
        {
            return _stats.TryGetValue(teaserId, out var stats)
                ? new StatsSnapshot(stats.Attempters.Count, stats.TotalSubmissions, stats.SuccessfulSubmissions)
                : new StatsSnapshot(0, 0, 0);
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
        File.WriteAllText(_filePath, JsonSerializer.Serialize(_stats, JsonOptions));
    }
}
