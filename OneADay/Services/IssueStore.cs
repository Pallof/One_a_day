using System.Text.Json;
using System.Text.Json.Serialization;

namespace OneADay.Services;

public static class IssueCategories
{
    public const string Wording = "Question is poorly worded or written incorrectly";
    public const string Evaluation = "Submission not accepted or being evaluated correctly, or solution is incorrect";
    public const string Other = "Other";

    public static readonly string[] All = [Wording, Evaluation, Other];
}

/// <summary>Triage state of a reported issue.</summary>
public enum IssueStatus
{
    /// <summary>Freshly reported, not yet looked at.</summary>
    New,
    InProgress,
    Solved,
    WontSolve,
    Duplicate,
}

public static class IssueStatusInfo
{
    public static readonly IssueStatus[] All =
        [IssueStatus.New, IssueStatus.InProgress, IssueStatus.Solved, IssueStatus.WontSolve, IssueStatus.Duplicate];

    public static string Label(IssueStatus status) => status switch
    {
        IssueStatus.New => "New",
        IssueStatus.InProgress => "In progress",
        IssueStatus.Solved => "Solved",
        IssueStatus.WontSolve => "Won't solve",
        IssueStatus.Duplicate => "Duplicate",
        _ => status.ToString(),
    };

    /// <summary>New and In progress still need attention; the rest are closed out.</summary>
    public static bool IsOpen(IssueStatus status) =>
        status is IssueStatus.New or IssueStatus.InProgress;

    /// <summary>CSS class suffix used for the status badge colour.</summary>
    public static string CssName(IssueStatus status) => status switch
    {
        IssueStatus.New => "new",
        IssueStatus.InProgress => "progress",
        IssueStatus.Solved => "solved",
        IssueStatus.WontSolve => "wontsolve",
        IssueStatus.Duplicate => "duplicate",
        _ => "new",
    };
}

public class IssueReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime SubmittedAt { get; set; }
    public string Category { get; set; } = IssueCategories.Other;
    public string Details { get; set; } = string.Empty;

    /// <summary>Page the report was filed from, e.g. "/questions/2026-07-28".</summary>
    public string? PageUrl { get; set; }

    /// <summary>The teaser being viewed, when the report came from a challenge page.</summary>
    public Guid? TeaserId { get; set; }
    public string? TeaserQuestion { get; set; }

    /// <summary>Triage tag: New, InProgress, Solved, WontSolve, or Duplicate.</summary>
    public IssueStatus Status { get; set; } = IssueStatus.New;

    /// <summary>Set when the status last changed, so you can see staleness.</summary>
    public DateTime? StatusUpdatedAt { get; set; }

    /// <summary>Legacy boolean from before Status existed; true is read as Solved.</summary>
    [JsonPropertyName("Resolved")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LegacyResolved { get; set; }
}

/// <summary>
/// File-backed store for visitor issue reports (App_Data/issues.json),
/// reviewed and triaged in the admin page.
/// </summary>
public class IssueStore
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly List<IssueReport> _issues;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Store Status as "InProgress"/"Solved"/… so issues.json stays readable.
        Converters = { new JsonStringEnumConverter() },
    };

    public IssueStore(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "issues.json");
        _issues = Load();
    }

    /// <summary>Open reports first (New, then In progress), newest first within each group.</summary>
    public IReadOnlyList<IssueReport> GetAll()
    {
        lock (_lock)
        {
            return _issues
                .OrderBy(i => (int)i.Status)
                .ThenByDescending(i => i.SubmittedAt)
                .ToList();
        }
    }

    public int OpenCount
    {
        get { lock (_lock) { return _issues.Count(i => IssueStatusInfo.IsOpen(i.Status)); } }
    }

    public void Add(IssueReport report)
    {
        lock (_lock)
        {
            _issues.Add(report);
            Persist();
        }
    }

    public void SetStatus(Guid id, IssueStatus status)
    {
        lock (_lock)
        {
            var issue = _issues.FirstOrDefault(i => i.Id == id);
            if (issue is not null && issue.Status != status)
            {
                issue.Status = status;
                issue.StatusUpdatedAt = AppTime.NowPacific;
                Persist();
            }
        }
    }

    public void Delete(Guid id)
    {
        lock (_lock)
        {
            _issues.RemoveAll(i => i.Id == id);
            Persist();
        }
    }

    private List<IssueReport> Load()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }
        var issues = JsonSerializer.Deserialize<List<IssueReport>>(File.ReadAllText(_filePath), JsonOptions) ?? [];
        // Migrate any pre-Status reports that only had the Resolved boolean.
        foreach (var issue in issues.Where(i => i.LegacyResolved is not null))
        {
            issue.Status = issue.LegacyResolved == true ? IssueStatus.Solved : IssueStatus.New;
            issue.LegacyResolved = null;
        }
        return issues;
    }

    private void Persist() =>
        File.WriteAllText(_filePath, JsonSerializer.Serialize(_issues, JsonOptions));
}
