using System.Text.Json;

namespace OneADay.Services;

public class TeaserSuggestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime SubmittedAt { get; set; }
    public string Difficulty { get; set; } = "Medium";
    public string Question { get; set; } = string.Empty;
    public string SolutionAndHint { get; set; } = string.Empty;
}

/// <summary>
/// File-backed store for brain teasers suggested by visitors on the
/// Contact us page. Lives in App_Data/suggestions.json; reviewed in admin.
///
/// Rate limiting: 1 suggestion per device per day, and at most
/// <see cref="MaxPerIpPerDay"/> per IP per day as a bot backstop. The limit
/// log is kept separately from the inbox so deleting a suggestion doesn't
/// reset anyone's limit. IPs are stored only as hashes.
/// </summary>
public class SuggestionStore
{
    public const int MaxPerIpPerDay = 3;

    private class SubmissionLogEntry
    {
        public Guid VisitorId { get; set; }
        public string? IpHash { get; set; }
        public DateOnly Date { get; set; }
    }

    private class SuggestionData
    {
        public List<TeaserSuggestion> Suggestions { get; set; } = [];
        public List<SubmissionLogEntry> Log { get; set; } = [];
    }

    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly SuggestionData _data;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SuggestionStore(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "suggestions.json");
        _data = Load();
    }

    public IReadOnlyList<TeaserSuggestion> GetAll()
    {
        lock (_lock)
        {
            return _data.Suggestions.OrderByDescending(s => s.SubmittedAt).ToList();
        }
    }

    public bool HasReachedDailyLimit(Guid visitorId, string? ipHash, DateOnly today)
    {
        lock (_lock)
        {
            return ReachedLimit(visitorId, ipHash, today);
        }
    }

    /// <summary>Adds the suggestion unless the visitor already hit today's limit.</summary>
    public bool TryAdd(TeaserSuggestion suggestion, Guid visitorId, string? ipHash, DateOnly today)
    {
        lock (_lock)
        {
            if (ReachedLimit(visitorId, ipHash, today))
            {
                return false;
            }
            _data.Suggestions.Add(suggestion);
            _data.Log.Add(new SubmissionLogEntry { VisitorId = visitorId, IpHash = ipHash, Date = today });
            _data.Log.RemoveAll(e => e.Date < today.AddDays(-7));
            Persist();
            return true;
        }
    }

    public void Delete(Guid id)
    {
        lock (_lock)
        {
            _data.Suggestions.RemoveAll(s => s.Id == id);
            Persist();
        }
    }

    private bool ReachedLimit(Guid visitorId, string? ipHash, DateOnly today)
    {
        var todays = _data.Log.Where(e => e.Date == today).ToList();
        if (todays.Any(e => e.VisitorId == visitorId))
        {
            return true;
        }
        return ipHash is not null && todays.Count(e => e.IpHash == ipHash) >= MaxPerIpPerDay;
    }

    private SuggestionData Load()
    {
        if (!File.Exists(_filePath))
        {
            return new SuggestionData();
        }
        var json = File.ReadAllText(_filePath);
        // Migrate the original format, which was a bare array of suggestions.
        if (json.TrimStart().StartsWith('['))
        {
            return new SuggestionData
            {
                Suggestions = JsonSerializer.Deserialize<List<TeaserSuggestion>>(json, JsonOptions) ?? [],
            };
        }
        return JsonSerializer.Deserialize<SuggestionData>(json, JsonOptions) ?? new SuggestionData();
    }

    private void Persist()
    {
        File.WriteAllText(_filePath, JsonSerializer.Serialize(_data, JsonOptions));
    }
}
