using System.Text.Json;
using OneADay.Models;

namespace OneADay.Services;

/// <summary>Everything in one scoreboard's file.</summary>
public sealed class BoardData
{
    /// <summary>The Pacific day <see cref="Today"/> belongs to. Another day's list is empty.</summary>
    public DateOnly? Day { get; set; }
    public List<BoardEntry> Today { get; set; } = [];
    public List<BoardEntry> AllTime { get; set; } = [];
}

/// <summary>
/// One Twenty Four scoreboard and its file: the ten best today and of all time, one row per player
/// (PRD 07). The longest runs are one; 24 Blitz's best scores are another, kept apart.
/// </summary>
/// <remarks>
/// <para>Today's list starts empty at midnight Pacific, like the daily teaser. A run that spans
/// midnight counts in full on the new day's list.</para>
///
/// <para>Made only by <see cref="TwentyFourBoard"/>, which gives out the players' numbers for
/// every board.</para>
/// </remarks>
public sealed class Scoreboard
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly ILogger _log;
    private readonly Func<DateTime> _utcNow;
    private BoardData _data;

    internal Scoreboard(string filePath, ILogger log, Func<DateTime> utcNow)
    {
        _filePath = filePath;
        _log = log;
        _utcNow = utcNow;
        _data = Load();
    }

    /// <summary>The saved board, or an empty one if it can't be read, set aside rather than lost.</summary>
    private BoardData Load()
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
            _log.LogError("Couldn't read the Twenty Four board {File} ({Error}), so it starts empty. The old file is {Aside}.",
                Path.GetFileName(_filePath), e.Message, Path.GetFileName(aside));
            return new();
        }
    }

    /// <summary>The highest player number on either list, 0 when there's none.</summary>
    internal int HighestNumber
    {
        get
        {
            lock (_lock)
            {
                return _data.Today.Concat(_data.AllTime).Select(e => e.Player.Number).DefaultIfEmpty(0).Max();
            }
        }
    }

    /// <summary>Today's ten best, best first.</summary>
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

    /// <summary>The ten best ever, best first.</summary>
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
    /// The player's run (or Blitz score) has reached this. Goes on either list it earns a place
    /// on, and saves only when one changed.
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
                _log.LogWarning("Couldn't save the Twenty Four board {File} ({Error}). It's kept until the next restart.",
                    Path.GetFileName(_filePath), e.GetType().Name);
            }
        }
    }

    private static DateOnly PacificDay(DateTime utc) => DateOnly.FromDateTime(AppTime.ToPacific(utc));
}
