using System.Text.Encodings.Web;
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

    /// <summary>
    /// What Jev was shown, as <see cref="BrainTeaser.Fingerprint"/>. The verdict stands only
    /// while the question and its accepted answers are unchanged.
    /// </summary>
    public string? Against { get; set; }

    public DateTime JudgedAt { get; set; }

    /// <summary>Whether this verdict was made against the question as it is now.</summary>
    public bool StillStandsFor(BrainTeaser teaser) => teaser.Id == TeaserId && Against == teaser.Fingerprint();
}

/// <summary>
/// Every answer Jev has judged (App_Data/judged-answers.json), so the same answer to the same
/// question gets the same verdict without asking again, and so the author can review them in
/// admin.
/// </summary>
/// <remarks>
/// Written by whichever site grades answers: the live one. The author copies it down with
/// deploy/pull-live-data.sh and rules on it in admin; those rulings go into teasers.json, never
/// back into this file, so every file still has one writer (PRD 10). The site on the author's
/// Mac writes its own copy too, empty at startup and with verdicts when it has a key; the next
/// pull replaces it, so nothing of it reaches the live site.
/// </remarks>
public sealed class JudgedAnswerStore
{
    public const string FileName = "judged-answers.json";

    /// <summary>
    /// The most verdicts kept. Past it each new verdict pushes out the oldest, so Jev is never
    /// switched off for good by a full file. Real use takes years to get here; the daily caps
    /// keep a flood from getting here in less than ten days.
    /// </summary>
    public const int MaxAnswers = 10_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Typed answers are kept as typed. The default escapes every non-ASCII character as
        // \uXXXX, which made a full file of other scripts four times the size.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _filePath;
    private readonly int _capacity;
    private readonly object _lock = new();

    /// <summary>In the order they were judged, oldest first.</summary>
    private readonly List<JudgedAnswer> _answers = [];

    private readonly Dictionary<(Guid, string), JudgedAnswer> _byAnswer = [];

    public JudgedAnswerStore(IWebHostEnvironment env) : this(env, MaxAnswers)
    {
    }

    /// <summary>With a smaller <paramref name="capacity"/>, so tests can fill it.</summary>
    public JudgedAnswerStore(IWebHostEnvironment env, int capacity)
    {
        _capacity = capacity;
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, FileName);
        if (!File.Exists(_filePath))
        {
            // Written now, empty, so deploy/pull-live-data.sh finds it on a site where Jev hasn't
            // judged anything yet: one missing file there stops the whole download.
            AtomicFile.WriteAllText(_filePath, "[]");
        }
        foreach (var answer in JsonSerializer.Deserialize<List<JudgedAnswer>>(File.ReadAllText(_filePath), JsonOptions) ?? [])
        {
            var key = Key(answer.TeaserId, answer.Submission);
            if (key.Item2.Length > 0 && _byAnswer.TryAdd(key, answer))
            {
                _answers.Add(answer);
            }
        }
    }

    /// <summary>
    /// The earlier verdict on this answer to this question, ignoring case, spacing and punctuation
    /// outside numbers. The caller checks it <see cref="JudgedAnswer.StillStandsFor"/> the question.
    /// </summary>
    public JudgedAnswer? Find(Guid teaserId, string submission)
    {
        lock (_lock)
        {
            return _byAnswer.GetValueOrDefault(Key(teaserId, submission));
        }
    }

    public int Count
    {
        get { lock (_lock) { return _answers.Count; } }
    }

    /// <summary>
    /// Remembers a verdict. One already held for the same answer, against the same question, is
    /// kept: two visitors sent it at once. One made against the question before it was edited is
    /// replaced. A full store drops its oldest verdict to make room. An answer with no letters or
    /// digits is never kept, since its key would be shared by all of them.
    /// </summary>
    public void Add(JudgedAnswer answer)
    {
        var key = Key(answer.TeaserId, answer.Submission);
        if (key.Item2.Length == 0)
        {
            return;
        }

        lock (_lock)
        {
            if (_byAnswer.TryGetValue(key, out var held))
            {
                if (held.Against == answer.Against)
                {
                    return;
                }
                _answers.Remove(held);
            }
            else if (_answers.Count >= _capacity)
            {
                var oldest = _answers[0];
                _answers.RemoveAt(0);
                _byAnswer.Remove(Key(oldest.TeaserId, oldest.Submission));
            }

            _answers.Add(answer);
            _byAnswer[key] = answer;
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
