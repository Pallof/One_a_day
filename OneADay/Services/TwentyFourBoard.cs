using OneADay.Models;

namespace OneADay.Services;

/// <summary>
/// The Twenty Four scoreboards and their players (PRD 07): the ten longest runs today and of all
/// time (App_Data/twentyfour-board.json), 24 Blitz's ten best scores beside them
/// (twentyfour-blitz-board.json), one row per player on each, and the players' numbers.
/// </summary>
/// <remarks>
/// <para>Written by the live site as visitors play, and read only there. Each row holds a
/// player's random id, their number and name (<see cref="TwentyFourPlayer"/>), and their run or
/// score; nothing about who they are.</para>
///
/// <para><b>Numbers</b> are given out here, in order, from twentyfour-players.txt, which holds the
/// last one given. A player is the same on both boards, under the same name. No number is given
/// twice: one that can't be saved isn't given at all.</para>
/// </remarks>
public sealed class TwentyFourBoard
{
    public const string FileName = "twentyfour-board.json";
    public const string BlitzFileName = "twentyfour-blitz-board.json";
    public const string NumbersFileName = "twentyfour-players.txt";

    private readonly string _numbersPath;
    private readonly object _lock = new();
    private readonly ILogger<TwentyFourBoard> _log;
    private readonly Scoreboard _runs;

    /// <summary>The last player number given out.</summary>
    private int _lastNumber;

    public TwentyFourBoard(IWebHostEnvironment env, ILogger<TwentyFourBoard> log, Func<DateTime>? utcNow = null)
    {
        _log = log;
        var now = utcNow ?? (() => DateTime.UtcNow);
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _numbersPath = Path.Combine(dataDir, NumbersFileName);
        _runs = new Scoreboard(Path.Combine(dataDir, FileName), log, now);
        Blitz = new Scoreboard(Path.Combine(dataDir, BlitzFileName), log, now);
        _lastNumber = LoadLastNumber();
    }

    /// <summary>
    /// 24 Blitz's board: each player's best score in a five-minute game, kept apart from the runs
    /// (issue 84). A score goes on when its game ends.
    /// </summary>
    public Scoreboard Blitz { get; }

    /// <summary>Today's ten longest runs, longest first.</summary>
    public IReadOnlyList<BoardEntry> Today => _runs.Today;

    /// <summary>The ten longest runs ever, longest first.</summary>
    public IReadOnlyList<BoardEntry> AllTime => _runs.AllTime;

    /// <summary>
    /// The player's run has reached this length. Goes on either list it earns a place on, and
    /// saves only when one changed.
    /// </summary>
    public void Record(TwentyFourPlayer player, int run) => _runs.Record(player, run);

    /// <summary>
    /// The last number given out. Never lower than any number on either board, so a counter that's
    /// missing, unreadable or restored from an older backup can't give out one that's on show.
    /// </summary>
    private int LoadLastNumber()
    {
        var onBoard = Math.Max(_runs.HighestNumber, Blitz.HighestNumber);
        if (!File.Exists(_numbersPath))
        {
            return onBoard;
        }
        if (int.TryParse(File.ReadAllText(_numbersPath).Trim(), out var saved) && saved >= 0)
        {
            return Math.Max(saved, onBoard);
        }
        _log.LogError("Couldn't read {File}, so player numbers go on from the highest on the boards, {Number}. Players off the boards may share a number with a new one.",
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
}
