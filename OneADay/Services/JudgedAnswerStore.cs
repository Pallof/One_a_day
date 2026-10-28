using System.Text.Json;
using OneADay.Models;

namespace OneADay.Services;

/// <summary>One answer Jev judged (PRD 02).</summary>
public sealed class JudgedAnswer
{
    public Guid TeaserId { get; set; }

    /// <summary>As the first person to send it typed it.</summary>
    public string Submission { get; set; } = string.Empty;

    /// <summary>
    /// Jev's chance, from 0 to 1, that it gives the same answer as the accepted ones. Kept instead
    /// of a yes or no, so right or wrong is decided against today's bar (<see cref="JevOptions.Passes"/>).
    /// </summary>
    public double Chance { get; set; }

    public DateTime JudgedAt { get; set; }
}

/// <summary>
/// Every answer Jev has judged (App_Data/judged-answers.json), so the same answer to the same
/// question gets the same verdict without asking again, and so the author can review them in
/// admin.
/// </summary>
/// <remarks>
/// Written by the live site only. The author copies it down with deploy/pull-live-data.sh and
/// rules on it in admin; those rulings go into teasers.json, never back into this file, so
/// every file still has one writer (PRD 10).
/// </remarks>
public sealed class JudgedAnswerStore
{
    public const string FileName = "judged-answers.json";

    /// <summary>
    /// The most verdicts kept. Past it Jev isn't asked any more, since a verdict that can't be
    /// remembered would be paid for again on every try. Real use takes years to get here; the
    /// daily cap keeps a flood from getting here in less than a month.
    /// </summary>
    public const int MaxAnswers = 10_000;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly List<JudgedAnswer> _answers;
    private readonly Dictionary<(Guid, string), JudgedAnswer> _byAnswer = [];

    public JudgedAnswerStore(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, FileName);
        if (!File.Exists(_filePath))
        {
            // Written now, empty, so deploy/pull-live-data.sh finds it on a site where Jev hasn't
            // judged anything yet: one missing file there stops the whole download.
            AtomicFile.WriteAllText(_filePath, "[]");
        }
        _answers = JsonSerializer.Deserialize<List<JudgedAnswer>>(File.ReadAllText(_filePath), JsonOptions) ?? [];
        foreach (var answer in _answers)
        {
            _byAnswer.TryAdd(Key(answer.TeaserId, answer.Submission), answer);
        }
    }

    /// <summary>The earlier verdict on this answer to this question, ignoring case, spacing and punctuation.</summary>
    public JudgedAnswer? Find(Guid teaserId, string submission)
    {
        lock (_lock)
        {
            return _byAnswer.GetValueOrDefault(Key(teaserId, submission));
        }
    }

    public bool IsFull
    {
        get { lock (_lock) { return _answers.Count >= MaxAnswers; } }
    }

    /// <summary>Remembers a verdict. One already held for the same answer is kept, and so is a full store.</summary>
    public void Add(JudgedAnswer answer)
    {
        lock (_lock)
        {
            if (_answers.Count >= MaxAnswers || !_byAnswer.TryAdd(Key(answer.TeaserId, answer.Submission), answer))
            {
                return;
            }
            _answers.Add(answer);
            AtomicFile.WriteAllText(_filePath, JsonSerializer.Serialize(_answers, JsonOptions));
        }
    }

    /// <summary>Newest first.</summary>
    public IReadOnlyList<JudgedAnswer> GetAll()
    {
        lock (_lock)
        {
            return _answers.OrderByDescending(a => a.JudgedAt).ToList();
        }
    }

    private static (Guid, string) Key(Guid teaserId, string submission) =>
        (teaserId, BrainTeaser.AnswerKey(submission));
}
