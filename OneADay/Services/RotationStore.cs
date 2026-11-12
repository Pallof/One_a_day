using System.Text.Json;
using OneADay.Models;

namespace OneADay.Services;

/// <summary>What ran on a given day, and why.</summary>
public sealed class DailyRun
{
    public DateOnly Date { get; set; }
    public Guid TeaserId { get; set; }

    /// <summary>True when this was a recycled draw rather than a new or pinned teaser.</summary>
    public bool Recycled { get; set; }
}

public sealed class RotationState
{
    /// <summary>Teasers still in the box for the current cycle.</summary>
    public List<Guid> Box { get; set; } = [];

    /// <summary>Every day that has been resolved, so a day's teaser never changes once set.</summary>
    public List<DailyRun> History { get; set; } = [];

    /// <summary>
    /// How many times each teaser has run. Feeds the draw weighting so a teaser that
    /// keeps sitting out a cycle becomes steadily more likely to come up.
    /// </summary>
    public Dictionary<Guid, int> ShowCounts { get; set; } = [];

    /// <summary>
    /// The day the site first had each teaser. A new teaser runs no sooner than the day after,
    /// so one uploaded today waits for tomorrow even if nobody has seen today's yet (author's
    /// decision, 2026-11-04). Null in a file written before this was kept: the first time it's
    /// filled, every teaser already in the bank counts as having arrived long ago.
    /// </summary>
    public Dictionary<Guid, DateOnly>? Arrived { get; set; }
}

/// <summary>
/// File-backed state for the recycling box (App_Data/rotation.json).
///
/// The history matters as much as the box: once a day has been resolved it must keep
/// the same teaser forever, or the challenge would change under a solver mid-attempt
/// and "yesterday's solution" would drift.
/// </summary>
public class RotationStore
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly RotationState _state;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public RotationStore(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "rotation.json");
        _state = File.Exists(_filePath)
            ? JsonSerializer.Deserialize<RotationState>(File.ReadAllText(_filePath), JsonOptions) ?? new()
            : new();
    }

    public DailyRun? RunFor(DateOnly date)
    {
        lock (_lock)
        {
            return _state.History.FirstOrDefault(r => r.Date == date);
        }
    }

    /// <summary>The most recent resolved day strictly before <paramref name="date"/>.</summary>
    public DailyRun? MostRecentBefore(DateOnly date)
    {
        lock (_lock)
        {
            return _state.History
                .Where(r => r.Date < date)
                .OrderByDescending(r => r.Date)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Records a new or pinned teaser running on <paramref name="date"/> (no draw involved), and
    /// returns what runs that day. If another request settled the day first, that run is kept
    /// and returned instead: a day's teaser never changes.
    /// </summary>
    public DailyRun RecordScheduled(DateOnly date, Guid teaserId)
    {
        lock (_lock)
        {
            var existing = _state.History.FirstOrDefault(r => r.Date == date);
            if (existing is not null)
            {
                return existing;
            }
            var run = new DailyRun { Date = date, TeaserId = teaserId, Recycled = false };
            _state.History.Add(run);
            CountShow(teaserId);
            Persist();
            return run;
        }
    }

    /// <summary>Every teaser that has run on at least one day.</summary>
    public IReadOnlySet<Guid> ShownIds()
    {
        lock (_lock)
        {
            return _state.History.Select(r => r.TeaserId).ToHashSet();
        }
    }

    /// <summary>
    /// Notes the teasers the site hasn't had before as arriving <paramref name="today"/>, and
    /// returns the day each one arrived. Writes only when something is new.
    /// </summary>
    public IReadOnlyDictionary<Guid, DateOnly> NoteArrivals(IEnumerable<Guid> teaserIds, DateOnly today)
    {
        lock (_lock)
        {
            var ids = teaserIds.ToList();
            var changed = false;
            if (_state.Arrived is null)
            {
                if (ids.Count == 0)
                {
                    return new Dictionary<Guid, DateOnly>();
                }
                // The first time, for a bank that was here before arrivals were kept.
                _state.Arrived = ids.Distinct().ToDictionary(id => id, _ => DateOnly.MinValue);
                changed = true;
            }
            foreach (var id in ids)
            {
                changed |= _state.Arrived.TryAdd(id, today);
            }
            if (changed)
            {
                Persist();
            }
            return new Dictionary<Guid, DateOnly>(_state.Arrived);
        }
    }

    /// <summary>
    /// Whether a teaser has run for the first time since the last recycled draw: the run of new
    /// teasers the box gets reshuffled after. A pinned teaser running again doesn't count.
    /// </summary>
    public bool NewRunsSinceLastDraw()
    {
        lock (_lock)
        {
            var lastDraw = _state.History
                .Where(r => r.Recycled)
                .Select(r => r.Date)
                .DefaultIfEmpty(DateOnly.MinValue)
                .Max();
            return _state.History
                .GroupBy(r => r.TeaserId)
                .Select(g => g.MinBy(r => r.Date)!)
                .Any(first => !first.Recycled && first.Date > lastDraw);
        }
    }

    /// <summary>
    /// Resolves a day by drawing from the box, or returns the draw already made for it.
    /// <paramref name="freshBox"/> empties the box first, so the draw refills it from the
    /// whole bank.
    /// </summary>
    public DailyRun? DrawFor(DateOnly date, IReadOnlyList<Guid> bank, Random random, bool freshBox = false)
    {
        lock (_lock)
        {
            // Never redraw a day that has already been settled.
            var existing = _state.History.FirstOrDefault(r => r.Date == date);
            if (existing is not null)
            {
                return existing;
            }

            var recent = _state.History
                .OrderByDescending(r => r.Date)
                .Select(r => r.TeaserId)
                .ToList();

            IEnumerable<Guid> box = freshBox ? [] : _state.Box;
            var draw = TeaserRotation.Draw(box, bank, recent, random, _state.ShowCounts);
            if (draw is null)
            {
                return null;
            }

            _state.Box = draw.Box;
            var run = new DailyRun { Date = date, TeaserId = draw.Drawn, Recycled = true };
            _state.History.Add(run);
            CountShow(draw.Drawn);
            Persist();
            return run;
        }
    }

    /// <summary>Box contents, recent history, and per-teaser appearance counts, for admin.</summary>
    public (int Remaining, IReadOnlyList<DailyRun> Recent, IReadOnlyDictionary<Guid, int> ShowCounts)
        Snapshot(int take = 14)
    {
        lock (_lock)
        {
            return (_state.Box.Count,
                _state.History.OrderByDescending(r => r.Date).Take(take).ToList(),
                new Dictionary<Guid, int>(_state.ShowCounts));
        }
    }

    private void CountShow(Guid teaserId) =>
        _state.ShowCounts[teaserId] = _state.ShowCounts.GetValueOrDefault(teaserId) + 1;

    /// <summary>Empties the box so the next draw starts a fresh cycle over the whole bank.</summary>
    public void ResetBox()
    {
        lock (_lock)
        {
            _state.Box.Clear();
            Persist();
        }
    }

    private void Persist() =>
        AtomicFile.WriteAllText(_filePath, JsonSerializer.Serialize(_state, JsonOptions));
}
