using System.Text.Encodings.Web;
using System.Text.Json;
using OneADay.Models;

namespace OneADay.Services;

/// <summary>
/// The verdicts of Jev's the author has agreed with in admin (App_Data/agreed-verdicts.json),
/// so they leave the review list (PRD 02).
/// </summary>
/// <remarks>
/// Written by admin on the author's Mac and read only there: it decides what the review list
/// shows and nothing else, so it is never uploaded or pulled. Agreeing changes no grading, and
/// puts nothing a visitor typed into teasers.json: the author keeps the accepted answers in
/// their own words (author's decision, 2026-10-28).
/// </remarks>
public sealed class AgreedVerdictStore
{
    public const string FileName = "agreed-verdicts.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly List<JudgedAnswer> _agreed;
    private readonly HashSet<(Guid, string, string?)> _keys = [];

    public AgreedVerdictStore(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, FileName);
        _agreed = File.Exists(_filePath)
            ? JsonSerializer.Deserialize<List<JudgedAnswer>>(File.ReadAllText(_filePath), JsonOptions) ?? []
            : [];
        foreach (var verdict in _agreed)
        {
            _keys.Add(Key(verdict));
        }
    }

    /// <summary>
    /// Whether the author agreed with this verdict. One made again after the question was edited
    /// is a new verdict, and comes back for review.
    /// </summary>
    public bool Contains(JudgedAnswer verdict)
    {
        lock (_lock)
        {
            return _keys.Contains(Key(verdict));
        }
    }

    public void Add(JudgedAnswer verdict)
    {
        lock (_lock)
        {
            if (!_keys.Add(Key(verdict)))
            {
                return;
            }
            _agreed.Add(verdict);
            AtomicFile.WriteAllText(_filePath, JsonSerializer.Serialize(_agreed, JsonOptions));
        }
    }

    private static (Guid, string, string?) Key(JudgedAnswer verdict) =>
        (verdict.TeaserId, BrainTeaser.AnswerKey(verdict.Submission), verdict.Against);
}
