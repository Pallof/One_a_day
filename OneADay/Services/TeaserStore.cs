using System.Text.Json;
using OneADay.Models;

namespace OneADay.Services;

/// <summary>
/// File-backed store for brain teasers. Everything is kept in App_Data/teasers.json,
/// so the whole question bank can also be edited or backed up by hand.
/// </summary>
public class TeaserStore
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private List<BrainTeaser> _teasers;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        // Store Difficulty as "Easy"/"Medium"/"Hard" so teasers.json stays hand-editable.
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public TeaserStore(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "teasers.json");
        _teasers = Load();
    }

    public IReadOnlyList<BrainTeaser> GetAll()
    {
        lock (_lock)
        {
            return _teasers.OrderByDescending(t => t.Date).ToList();
        }
    }

    public BrainTeaser? GetForDate(DateOnly date)
    {
        lock (_lock)
        {
            return _teasers.FirstOrDefault(t => t.Date == date);
        }
    }

    /// <summary>Today's teaser, or the most recent past one if none is set for today.</summary>
    public BrainTeaser? GetCurrent(DateOnly today)
    {
        lock (_lock)
        {
            return _teasers
                .Where(t => t.Date <= today)
                .OrderByDescending(t => t.Date)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// All teasers (past, today, and upcoming) for the All Questions page,
    /// newest first. The page labels each by date and protects upcoming answers.
    /// </summary>
    public IReadOnlyList<BrainTeaser> Search(string? term)
    {
        lock (_lock)
        {
            IEnumerable<BrainTeaser> matches = _teasers;
            if (!string.IsNullOrWhiteSpace(term))
            {
                matches = matches.Where(t =>
                    t.Question.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            return matches.OrderByDescending(t => t.Date).ToList();
        }
    }

    public void Save(BrainTeaser teaser)
    {
        lock (_lock)
        {
            _teasers.RemoveAll(t => t.Id == teaser.Id);
            _teasers.Add(teaser);
            Persist();
        }
    }

    public void Delete(Guid id)
    {
        lock (_lock)
        {
            _teasers.RemoveAll(t => t.Id == id);
            Persist();
        }
    }

    private List<BrainTeaser> Load()
    {
        if (!File.Exists(_filePath))
        {
            var seeded = Seed();
            _teasers = seeded;
            Persist();
            return seeded;
        }
        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<List<BrainTeaser>>(json, JsonOptions) ?? [];
    }

    private void Persist()
    {
        File.WriteAllText(_filePath, JsonSerializer.Serialize(_teasers, JsonOptions));
    }

    private static List<BrainTeaser> Seed()
    {
        var today = AppTime.Today;
        return
        [
            new BrainTeaser
            {
                Date = today,
                Difficulty = Difficulty.Medium,
                Question = "I speak without a mouth and hear without ears. I have no body, but I come alive with wind. What am I?",
                Answer = "an echo; echo",
                Hint = "You might meet me in the mountains or an empty hall.",
                Solution = "An echo — it \"speaks\" by repeating sound and needs no body to do it.",
            },
            new BrainTeaser
            {
                Date = today.AddDays(-1),
                Difficulty = Difficulty.Easy,
                Question = "What has keys but can't open locks, space but no room, and you can enter but not go inside?",
                Answer = "a keyboard; keyboard",
                Hint = "You are probably touching one right now.",
                Solution = "A keyboard — it has keys, a space bar, and an enter key.",
            },
            new BrainTeaser
            {
                Date = today.AddDays(-2),
                Difficulty = Difficulty.Hard,
                Question = "A farmer has 17 sheep and all but 9 run away. How many are left?",
                Answer = "9; nine",
                Hint = "Read the question again, slowly.",
                Solution = "\"All but 9\" run away, so 9 remain.",
            },
        ];
    }
}
