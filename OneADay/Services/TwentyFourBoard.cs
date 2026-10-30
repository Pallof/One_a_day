using System.Text.Json;
using OneADay.Models;

namespace OneADay.Services;

/// <summary>Everything in twentyfour-board.json.</summary>
public sealed class BoardData
{
    /// <summary>The Pacific day <see cref="Today"/> belongs to. Another day's list is empty.</summary>
    public DateOnly? Day { get; set; }
    public List<BoardEntry> Today { get; set; } = [];
    public List<BoardEntry> AllTime { get; set; } = [];
}

/// <summary>A player kept off the board, one line of twentyfour-hidden.jsonl, and since when.</summary>
public sealed record HiddenPlayer(Guid Player, DateTime HiddenAtUtc);

/// <summary>
/// The Twenty Four scoreboard (App_Data/twentyfour-board.json): the ten longest runs today and
/// of all time, one row per player (PRD 07).
/// </summary>
/// <remarks>
/// <para>Written by the live site as visitors play, and read only there. Each row holds a
/// player's random id, their number and name (<see cref="TwentyFourPlayer"/>), and their run;
/// nothing about who they are.</para>
///
/// <para><b>Numbers</b> are given out here, in order, from twentyfour-players.txt, which holds the
/// last one given. No number is given twice: one that can't be saved isn't given at all.</para>
///
/// <para>Today's list starts empty at midnight Pacific, like the daily teaser. A run that spans
/// midnight counts in full on the new day's list.</para>
///
/// <para><b>Hidden players, a shadow ban.</b> A player caught answering faster than anyone
/// types (<see cref="AnswerPace"/>) is kept off the board for good, without being told
/// (author's decision, 2026-10-29). Their rows leave both lists, and from then on their runs
/// never reach them, so they take no more places. Only they still see their own runs, ranked
/// where they'd be, so the board looks no different to them until the next restart: their own
/// runs are kept only in memory.</para>
///
/// <para><b>twentyfour-hidden.jsonl</b> has one hidden player per line, and hiding someone adds
/// a line rather than rewriting the file, so it stays cheap however long the list grows. The
/// author bans or lets back on by hand by adding or deleting a line, the id alone will do, and
/// restarting; a line the site adds in the meantime doesn't undo the edit. A line that can't be
/// read is skipped, never a reason for the game to stop.</para>
/// </remarks>
public sealed class TwentyFourBoard
{
    public const string FileName = "twentyfour-board.json";
    public const string HiddenFileName = "twentyfour-hidden.jsonl";
    public const string NumbersFileName = "twentyfour-players.txt";

    /// <summary>Most players kept off at once; past that, the longest-hidden one is let back on.</summary>
    public const int MaxHidden = 10_000;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions LineOptions = new();

    private readonly string _filePath;
    private readonly string _hiddenPath;
    private readonly string _numbersPath;
    private readonly object _lock = new();
    private readonly ILogger<TwentyFourBoard> _log;
    private readonly Func<DateTime> _utcNow;
    private readonly int _maxHidden;
    private BoardData _data;

    /// <summary>The last player number given out.</summary>
    private int _lastNumber;

    /// <summary>Hidden players, longest-hidden first.</summary>
    private readonly List<HiddenPlayer> _hidden;
    private readonly HashSet<Guid> _hiddenIds;

    /// <summary>The runs only a hidden player sees: their best today, and ever.</summary>
    private readonly Dictionary<Guid, OwnRuns> _ownRuns = [];

    private sealed record OwnRuns(DateOnly Day, BoardEntry? Today, BoardEntry? AllTime);

    public TwentyFourBoard(IWebHostEnvironment env, ILogger<TwentyFourBoard> log,
        Func<DateTime>? utcNow = null, int maxHidden = MaxHidden)
    {
        _log = log;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _maxHidden = maxHidden;
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, FileName);
        _hiddenPath = Path.Combine(dataDir, HiddenFileName);
        _numbersPath = Path.Combine(dataDir, NumbersFileName);
        _data = LoadBoard();
        _lastNumber = LoadLastNumber();
        _hidden = LoadHidden();
        _hiddenIds = _hidden.Select(h => h.Player).ToHashSet();

        // A player banned by hand may still have rows: they leave now, as Hide would take them.
        var day = PacificDay(_utcNow());
        foreach (var id in _data.Today.Concat(_data.AllTime).Select(e => e.Player.Id).Where(_hiddenIds.Contains).Distinct().ToList())
        {
            TakeOffBoard(id, day);
        }
    }

    /// <summary>The saved board, or an empty one if it can't be read, set aside rather than lost.</summary>
    private BoardData LoadBoard()
    {
        if (!File.Exists(_filePath))
        {
            return new();
        }
        try
        {
            return JsonSerializer.Deserialize<BoardData>(File.ReadAllText(_filePath), JsonOptions) ?? new();
        }
        catch (JsonException e)
        {
            // A board is a game's: better empty than the game down for everyone. The file is kept
            // beside it, so nothing is lost and the next save can't write over it.
            var aside = Path.ChangeExtension(_filePath, $".unreadable-{_utcNow():yyyyMMddHHmmss}.json");
            File.Move(_filePath, aside);
            _log.LogError("Couldn't read the Twenty Four board ({Error}), so it starts empty. The old file is {Aside}.",
                e.Message, Path.GetFileName(aside));
            return new();
        }
    }

    /// <summary>
    /// The last number given out. Never lower than any number on the board, so a counter that's
    /// missing, unreadable or restored from an older backup can't give out one that's on show.
    /// </summary>
    private int LoadLastNumber()
    {
        var onBoard = _data.Today.Concat(_data.AllTime).Select(e => e.Player.Number).DefaultIfEmpty(0).Max();
        if (!File.Exists(_numbersPath))
        {
            return onBoard;
        }
        if (int.TryParse(File.ReadAllText(_numbersPath).Trim(), out var saved) && saved >= 0)
        {
            return Math.Max(saved, onBoard);
        }
        _log.LogError("Couldn't read {File}, so player numbers go on from the highest on the board, {Number}. Players off the board may share a number with a new one.",
            NumbersFileName, onBoard);
        return onBoard;
    }

    /// <summary>
    /// A new player, with the next number. Null when the number can't be saved: given anyway, a
    /// restart could give it again, and two players would share a name.
    /// </summary>
    public TwentyFourPlayer? NewPlayer()
    {
        lock (_lock)
        {
            var number = _lastNumber + 1;
            try
            {
                AtomicFile.WriteAllText(_numbersPath, number.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _log.LogWarning("Couldn't save {File} ({Error}), so no new player gets a name until it can be.",
                    NumbersFileName, e.GetType().Name);
                return null;
            }
            _lastNumber = number;
            return new TwentyFourPlayer(Guid.NewGuid(), number);
        }
    }

    /// <summary>
    /// The hidden players, longest-hidden first: one per line, as JSON or the id alone. Lines that
    /// can't be read are skipped. Tidied on the way in, when there's anything to tidy.
    /// </summary>
    private List<HiddenPlayer> LoadHidden()
    {
        if (!File.Exists(_hiddenPath))
        {
            return [];
        }

        var lines = File.ReadAllLines(_hiddenPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var read = new List<HiddenPlayer>();
        var skipped = 0;
        foreach (var line in lines)
        {
            if (ParseHidden(line) is { } player)
            {
                read.Add(player);
            }
            else
            {
                skipped++;
            }
        }
        if (skipped > 0)
        {
            _log.LogWarning("Skipped {Count} unreadable line(s) in {File}; those players are back on the board.",
                skipped, HiddenFileName);
        }

        // Each player once, at their first line, and only the newest MaxHidden.
        var seen = new HashSet<Guid>();
        var hidden = read.Where(h => seen.Add(h.Player)).ToList();
        if (hidden.Count > _maxHidden)
        {
            hidden.RemoveRange(0, hidden.Count - _maxHidden);
        }
        if (hidden.Count != lines.Count)
        {
            Save(_hiddenPath, string.Concat(hidden.Select(Line)), "the list of players kept off the Twenty Four board");
        }
        return hidden;
    }

    private static HiddenPlayer? ParseHidden(string line)
    {
        var trimmed = line.Trim().Trim('"');
        if (Guid.TryParse(trimmed, out var id))
        {
            return new HiddenPlayer(id, default);
        }
        try
        {
            return JsonSerializer.Deserialize<HiddenPlayer>(line, LineOptions) is { Player: var p } h && p != Guid.Empty ? h : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Line(HiddenPlayer player) => JsonSerializer.Serialize(player, LineOptions) + "\n";

    /// <summary>Today's ten longest runs, longest first, as everyone sees them.</summary>
    public IReadOnlyList<BoardEntry> Today => TodayAsSeenBy(null);

    /// <summary>The ten longest runs ever, longest first, as everyone sees them.</summary>
    public IReadOnlyList<BoardEntry> AllTime => AllTimeAsSeenBy(null);

    /// <summary>Today's list as this player sees it: a hidden player sees their own run on it too.</summary>
    public IReadOnlyList<BoardEntry> TodayAsSeenBy(Guid? viewer)
    {
        lock (_lock)
        {
            var day = PacificDay(_utcNow());
            var shown = _data.Day == day ? _data.Today : [];
            return OwnRunsOf(viewer) is { } own && own.Day == day && own.Today is { } row
                ? RunBoard.With(shown, row) ?? shown
                : shown;
        }
    }

    /// <summary>The all-time list as this player sees it: a hidden player sees their own run on it too.</summary>
    public IReadOnlyList<BoardEntry> AllTimeAsSeenBy(Guid? viewer)
    {
        lock (_lock)
        {
            return OwnRunsOf(viewer)?.AllTime is { } row
                ? RunBoard.With(_data.AllTime, row) ?? _data.AllTime
                : _data.AllTime;
        }
    }

    /// <summary>
    /// The player's run has reached this length. Goes on either list it earns a place on, and
    /// saves only when one changed. A hidden player's run goes only where they alone see it.
    /// </summary>
    public void Record(TwentyFourPlayer player, int run)
    {
        lock (_lock)
        {
            var now = _utcNow();
            var day = PacificDay(now);
            var entry = new BoardEntry(player, run, now);

            if (_hiddenIds.Contains(player.Id))
            {
                var own = OwnRunsOf(player.Id);
                var ownToday = own?.Day == day ? own.Today : null;
                _ownRuns[player.Id] = new OwnRuns(day,
                    ownToday is null || run > ownToday.Run ? entry : ownToday,
                    own?.AllTime is null || run > own.AllTime.Run ? entry : own.AllTime);
                return;
            }

            var today = _data.Day == day ? _data.Today : [];
            var newToday = RunBoard.With(today, entry);
            var newAllTime = RunBoard.With(_data.AllTime, entry);
            if (newToday is null && newAllTime is null)
            {
                return;
            }

            // A new object, never the old one changed, so a list handed out stays as it was.
            _data = new BoardData { Day = day, Today = newToday ?? today, AllTime = newAllTime ?? _data.AllTime };
            Save(_filePath, JsonSerializer.Serialize(_data, JsonOptions), "the Twenty Four board");
        }
    }

    /// <summary>
    /// Keeps this player off the board from now on, without telling them. Their rows leave both
    /// lists and become theirs alone to see. Asking again changes nothing.
    /// </summary>
    public void Hide(TwentyFourPlayer player)
    {
        lock (_lock)
        {
            if (!_hiddenIds.Add(player.Id))
            {
                return;
            }

            var now = _utcNow();
            var hidden = new HiddenPlayer(player.Id, now);
            _hidden.Add(hidden);
            if (_hidden.Count > _maxHidden)
            {
                var longest = _hidden[0];
                _hidden.RemoveAt(0);
                _hiddenIds.Remove(longest.Player);
                _ownRuns.Remove(longest.Player);
            }

            // One line added, never the file rewritten: cheap while every page waits on this
            // lock, and an edit the author made since the last restart stays as they left it.
            try
            {
                File.AppendAllText(_hiddenPath, Line(hidden));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _log.LogWarning("Couldn't save the list of players kept off the Twenty Four board ({Error}). It's kept until the next restart.",
                    e.GetType().Name);
            }

            TakeOffBoard(player.Id, PacificDay(now));

            // The id's start beside the name: it's what the author looks for in
            // twentyfour-hidden.jsonl to let someone back on.
            _log.LogInformation(
                "Kept {Name} ({Id}) off the Twenty Four board: their answers came faster than anyone types. Only they still see their runs.",
                player.Name, player.Id.ToString("N")[..8]);
        }
    }

    /// <summary>A hidden player's rows leave both lists, and become theirs alone to see.</summary>
    private void TakeOffBoard(Guid player, DateOnly day)
    {
        var today = _data.Day == day ? _data.Today.FirstOrDefault(e => e.Player.Id == player) : null;
        var allTime = _data.AllTime.FirstOrDefault(e => e.Player.Id == player);
        _ownRuns[player] = new OwnRuns(day, today, allTime);
        if (today is null && allTime is null)
        {
            return;
        }
        _data = new BoardData
        {
            Day = _data.Day,
            Today = _data.Today.Where(e => e.Player.Id != player).ToList(),
            AllTime = _data.AllTime.Where(e => e.Player.Id != player).ToList(),
        };
        Save(_filePath, JsonSerializer.Serialize(_data, JsonOptions), "the Twenty Four board");
    }

    private OwnRuns? OwnRunsOf(Guid? player) =>
        player is { } id && _ownRuns.TryGetValue(id, out var own) ? own : null;

    private void Save(string path, string contents, string what)
    {
        try
        {
            AtomicFile.WriteAllText(path, contents);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A full disk, say. The board is a game's: the player's page carries on, and what's
            // held in memory is saved with the next change.
            _log.LogWarning("Couldn't save {What} ({Error}). It's kept until the next restart.", what, e.GetType().Name);
        }
    }

    private static DateOnly PacificDay(DateTime utc) => DateOnly.FromDateTime(AppTime.ToPacific(utc));
}
