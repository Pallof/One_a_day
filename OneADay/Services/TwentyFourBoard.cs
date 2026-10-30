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
/// </remarks>
public sealed class TwentyFourBoard
{
    public const string FileName = "twentyfour-board.json";
    public const string NumbersFileName = "twentyfour-players.txt";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly string _numbersPath;
    private readonly object _lock = new();
    private readonly ILogger<TwentyFourBoard> _log;
    private readonly Func<DateTime> _utcNow;
    private BoardData _data;

    /// <summary>The last player number given out.</summary>
    private int _lastNumber;

    public TwentyFourBoard(IWebHostEnvironment env, ILogger<TwentyFourBoard> log, Func<DateTime>? utcNow = null)
    {
        _log = log;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, FileName);
        _numbersPath = Path.Combine(dataDir, NumbersFileName);
        _data = LoadBoard();
        _lastNumber = LoadLastNumber();
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

    /// <summary>Today's ten longest runs, longest first.</summary>
    public IReadOnlyList<BoardEntry> Today
    {
        get
        {
            lock (_lock)
            {
                return _data.Day == PacificDay(_utcNow()) ? _data.Today : [];
            }
        }
    }

    /// <summary>The ten longest runs ever, longest first.</summary>
    public IReadOnlyList<BoardEntry> AllTime
    {
        get
        {
            lock (_lock)
            {
                return _data.AllTime;
            }
        }
    }

    /// <summary>
    /// The player's run has reached this length. Goes on either list it earns a place on, and
    /// saves only when one changed.
    /// </summary>
    public void Record(TwentyFourPlayer player, int run)
    {
        lock (_lock)
        {
            var now = _utcNow();
            var day = PacificDay(now);
            var entry = new BoardEntry(player, run, now);
            var today = _data.Day == day ? _data.Today : [];

            var newToday = RunBoard.With(today, entry);
            var newAllTime = RunBoard.With(_data.AllTime, entry);
            if (newToday is null && newAllTime is null)
            {
                return;
            }

            // A new object, never the old one changed, so a list handed out stays as it was.
            _data = new BoardData { Day = day, Today = newToday ?? today, AllTime = newAllTime ?? _data.AllTime };
            try
            {
                AtomicFile.WriteAllText(_filePath, JsonSerializer.Serialize(_data, JsonOptions));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A full disk, say. The board is a game's: the player's page carries on, and the
                // runs held in memory are saved with the next one that changes the board.
                _log.LogWarning("Couldn't save the Twenty Four board ({Error}). It's kept until the next restart.",
                    e.GetType().Name);
            }
        }
    }

    private static DateOnly PacificDay(DateTime utc) => DateOnly.FromDateTime(AppTime.ToPacific(utc));
}
