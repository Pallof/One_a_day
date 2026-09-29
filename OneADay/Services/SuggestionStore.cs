using System.Text.Json;

namespace OneADay.Services;

public class TeaserSuggestion
{
    /// <summary>The longest teaser, or solution and hint, the form takes. The server holds to it too.</summary>
    public const int MaxFieldLength = 600;

    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime SubmittedAt { get; set; }
    public string Difficulty { get; set; } = "Medium";
    public string Question { get; set; } = string.Empty;
    public string SolutionAndHint { get; set; } = string.Empty;
}

/// <summary>What happened to a suggestion handed to <see cref="SuggestionStore.Add"/>.</summary>
public enum SuggestionOutcome
{
    Added,

    /// <summary>This device or address already used today's allowance. Nothing saved.</summary>
    OverDailyLimit,

    /// <summary><see cref="SuggestionStore.MaxStored"/> suggestions are waiting already. Nothing saved.</summary>
    InboxFull,
}

/// <summary>
/// File-backed store for brain teasers suggested by visitors on the
/// Contact us page. Lives in App_Data/suggestions.json; reviewed in admin.
///
/// Rate limiting: 1 suggestion per device per day, and at most
/// <see cref="MaxPerIpPerDay"/> per IP per day as a bot backstop. The limit
/// log is kept separately from the inbox so deleting a suggestion doesn't
/// reset anyone's limit. IPs are stored only as hashes.
///
/// These caps govern *volume*. Whether a submission looks automated at all is
/// decided earlier by <see cref="OneADay.Models.SubmissionGuard"/>; anything it
/// rejects never reaches <see cref="Add"/> and so never consumes a quota.
/// </summary>
public class SuggestionStore : IDisposable
{
    public const int MaxPerIpPerDay = 3;

    /// <summary>
    /// The most suggestions kept at once. Past it, a new one isn't saved and its sender is told.
    /// </summary>
    /// <remarks>
    /// The per-IP cap stops one address, not a script that keeps changing address — and every
    /// save rewrites the whole file, which lives in memory. A thousand is years of real
    /// suggestions; deleting reviewed ones makes room. Security review, 2026-09-28.
    /// </remarks>
    public const int MaxStored = 1000;

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

        /// <summary>Running total of submissions dropped by <see cref="OneADay.Models.SubmissionGuard"/>.</summary>
        public int BlockedCount { get; set; }
    }

    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly SuggestionData _data;
    private readonly ILogger<SuggestionStore>? _log;

    /// <summary>A bot counted since the file was last written. See <see cref="RecordBlocked"/>.</summary>
    private bool _blockedUnsaved;

    /// <summary>Whether the full-inbox warning has been logged since the inbox last had room.</summary>
    private bool _warnedFull;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SuggestionStore(IWebHostEnvironment env, ILogger<SuggestionStore>? log = null)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "suggestions.json");
        _log = log;
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

    /// <summary>
    /// Saves the suggestion, unless the visitor already hit today's limit or the inbox is full.
    /// </summary>
    public SuggestionOutcome Add(TeaserSuggestion suggestion, Guid visitorId, string? ipHash, DateOnly today)
    {
        lock (_lock)
        {
            if (ReachedLimit(visitorId, ipHash, today))
            {
                return SuggestionOutcome.OverDailyLimit;
            }

            if (_data.Suggestions.Count >= MaxStored)
            {
                if (!_warnedFull)
                {
                    _warnedFull = true;
                    _log?.LogWarning("{Max} suggestions are waiting, so new ones aren't being saved. " +
                                     "Delete reviewed ones to make room.", MaxStored);
                }
                return SuggestionOutcome.InboxFull;
            }

            _warnedFull = false;
            _data.Suggestions.Add(suggestion);
            _data.Log.Add(new SubmissionLogEntry { VisitorId = visitorId, IpHash = ipHash, Date = today });
            _data.Log.RemoveAll(e => e.Date < today.AddDays(-7));
            Persist();
            return SuggestionOutcome.Added;
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

    /// <summary>
    /// Counts one submission dropped as automated. Worth surfacing in admin: the
    /// honeypot and timing checks reject silently, so without a count there is no way
    /// to tell "no bots" apart from "quietly eating real suggestions".
    /// </summary>
    /// <remarks>
    /// Counted in memory, not written straight away, so a script sending nothing but blocked
    /// suggestions can't make the server rewrite the whole file for each one. The count goes
    /// out with the next save, or when the app shuts down (<see cref="Dispose"/>).
    /// </remarks>
    public void RecordBlocked()
    {
        lock (_lock)
        {
            _data.BlockedCount++;
            _blockedUnsaved = true;
        }
    }

    public int BlockedCount
    {
        get { lock (_lock) { return _data.BlockedCount; } }
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

    /// <summary>
    /// Saves a bot count no other save has carried yet. The host calls this when the app shuts
    /// down normally, as it does for every redeploy.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (!_blockedUnsaved)
            {
                return;
            }
            try
            {
                Persist();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort on the way out, as in IssueStore.Dispose.
            }
        }
    }

    private void Persist()
    {
        AtomicFile.WriteAllText(_filePath, JsonSerializer.Serialize(_data, JsonOptions));
        _blockedUnsaved = false;
    }
}
