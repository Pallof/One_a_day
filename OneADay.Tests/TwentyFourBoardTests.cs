using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The Twenty Four scoreboard (PRD 07): ten longest runs today and of all time, one row per
/// player, under a name made from their id.
/// </summary>
public class TwentyFourBoardTests : IDisposable
{
    private readonly TestEnvironment _env = new();

    // Noon Pacific on 29 Oct 2026 (UTC-7), well clear of midnight either way.
    private DateTime _now = new(2026, 10, 29, 19, 0, 0, DateTimeKind.Utc);

    private TwentyFourBoard NewBoard() => new(_env, NullLogger<TwentyFourBoard>.Instance, () => _now);

    private string HiddenPath => Path.Combine(_env.ContentRootPath, "App_Data", TwentyFourBoard.HiddenFileName);

    /// <summary>The ids in the hidden list, a line each, as saved.</summary>
    private Guid[] HiddenLines() =>
        File.ReadAllLines(HiddenPath).Where(l => l.Length > 0)
            .Select(l => JsonSerializer.Deserialize<HiddenPlayer>(l)!.Player).ToArray();

    private static readonly TwentyFourPlayer Ann = new(Guid.Parse("11111111-1111-4111-8111-111111111111"), 1);
    private static readonly TwentyFourPlayer Bo = new(Guid.Parse("22222222-2222-4222-8222-222222222222"), 2);
    private int _nextNumber = 3;

    private TwentyFourPlayer NewPlayer() => new(Guid.NewGuid(), _nextNumber++);

    private static (TwentyFourPlayer, int)[] Rows(IReadOnlyList<BoardEntry> board) =>
        board.Select(e => (e.Player, e.Run)).ToArray();

    // ---- ranking ---------------------------------------------------------------------

    [Fact]
    public void A_player_has_one_row_holding_their_best_run()
    {
        var board = NewBoard();
        board.Record(Ann, 1);
        board.Record(Ann, 2);
        board.Record(Ann, 3);
        board.Record(Ann, 2);   // a later, shorter run doesn't lower it

        Assert.Equal([(Ann, 3)], Rows(board.Today));
        Assert.Equal([(Ann, 3)], Rows(board.AllTime));
    }

    [Fact]
    public void Longest_first_and_of_equal_runs_whoever_got_there_first()
    {
        var board = NewBoard();
        board.Record(Bo, 4);
        _now = _now.AddMinutes(1);
        board.Record(Ann, 4);
        _now = _now.AddMinutes(1);
        var cy = NewPlayer();
        board.Record(cy, 6);

        Assert.Equal([(cy, 6), (Bo, 4), (Ann, 4)], Rows(board.AllTime));
    }

    [Fact]
    public void Only_the_ten_longest_runs_stay_on()
    {
        var board = NewBoard();
        var players = Enumerable.Range(0, 10).Select(_ => NewPlayer()).ToArray();
        for (var i = 0; i < 10; i++)
        {
            board.Record(players[i], i + 2);   // runs 2 to 11
        }

        board.Record(Ann, 2);   // ties the shortest, but got there last
        Assert.DoesNotContain(board.AllTime, e => e.Player == Ann);
        Assert.Equal(10, board.AllTime.Count);

        board.Record(Ann, 3);   // beats the shortest, which goes
        Assert.Equal(10, board.AllTime.Count);
        Assert.Contains(board.AllTime, e => e.Player == Ann && e.Run == 3);
        Assert.DoesNotContain(board.AllTime, e => e.Player == players[0]);
    }

    [Fact]
    public void A_list_already_read_doesnt_change_under_the_reader()
    {
        // The page reads the lists while rendering; the next record mustn't rewrite them mid-loop.
        var board = NewBoard();
        board.Record(Ann, 1);
        var read = board.AllTime;

        board.Record(Bo, 5);

        Assert.Equal([(Ann, 1)], Rows(read));
    }

    // ---- days ------------------------------------------------------------------------

    [Fact]
    public void Today_starts_empty_at_midnight_pacific_and_all_time_keeps_its_runs()
    {
        var board = NewBoard();
        board.Record(Ann, 5);

        _now = new DateTime(2026, 10, 30, 6, 59, 0, DateTimeKind.Utc);   // 11:59pm Pacific
        Assert.Equal([(Ann, 5)], Rows(board.Today));

        _now = _now.AddMinutes(2);                                        // 12:01am
        Assert.Empty(board.Today);
        Assert.Equal([(Ann, 5)], Rows(board.AllTime));

        board.Record(Bo, 2);
        Assert.Equal([(Bo, 2)], Rows(board.Today));
        Assert.Equal([(Ann, 5), (Bo, 2)], Rows(board.AllTime));
    }

    // ---- the file --------------------------------------------------------------------

    [Fact]
    public void Runs_survive_a_restart()
    {
        NewBoard().Record(Ann, 3);

        var reloaded = NewBoard();

        Assert.Equal([(Ann, 3)], Rows(reloaded.Today));
        Assert.Equal([(Ann, 3)], Rows(reloaded.AllTime));
    }

    [Fact]
    public void A_run_that_changes_nothing_isnt_saved()
    {
        var board = NewBoard();
        board.Record(Ann, 3);
        var path = Path.Combine(_env.ContentRootPath, "App_Data", TwentyFourBoard.FileName);
        File.WriteAllText(path, File.ReadAllText(path) + " ");   // marks the file as written

        board.Record(Ann, 2);

        Assert.EndsWith(" ", File.ReadAllText(path));
    }

    [Fact]
    public void The_file_holds_each_players_id_number_name_and_run_and_nothing_else()
    {
        // The name is saved so the file can be read by eye. A field that stored anything else
        // about a player would fail here.
        NewBoard().Record(Ann, 3);

        var json = _env.ReadDataFile(TwentyFourBoard.FileName);
        var names = new HashSet<string>();
        void Walk(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in e.EnumerateObject()) { names.Add(p.Name); Walk(p.Value); }
            }
            else if (e.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in e.EnumerateArray()) { Walk(item); }
            }
        }
        Walk(JsonDocument.Parse(json).RootElement);

        Assert.Equal(["AllTime", "Day", "Id", "Name", "Number", "Player", "ReachedAtUtc", "Run", "Today"], names.Order());
        Assert.Contains($"\"Name\": \"{Ann.Name}\"", json);
    }

    // ---- numbers ---------------------------------------------------------------------

    private string NumbersPath => Path.Combine(_env.ContentRootPath, "App_Data", TwentyFourBoard.NumbersFileName);

    [Fact]
    public void Players_get_numbers_in_order_and_a_restart_goes_on_from_the_last()
    {
        var board = NewBoard();
        var first = board.NewPlayer()!;
        var second = board.NewPlayer()!;

        var third = NewBoard().NewPlayer()!;

        Assert.Equal([1, 2, 3], new[] { first.Number, second.Number, third.Number });
        Assert.Equal(3, new[] { first.Id, second.Id, third.Id }.Distinct().Count());
        Assert.Equal("3", File.ReadAllText(NumbersPath));
    }

    [Fact]
    public void A_missing_or_older_counter_never_gives_out_a_number_on_the_board()
    {
        // Lost, or put back from an older backup: numbers go on past the highest on show. A
        // number given since the backup to someone off the board can come round again; that's
        // the price, and PRD 07 says so.
        NewBoard().Record(new TwentyFourPlayer(Guid.NewGuid(), 40), 2);
        Assert.Equal(41, NewBoard().NewPlayer()!.Number);

        File.Delete(NumbersPath);
        Assert.Equal(41, NewBoard().NewPlayer()!.Number);
        File.WriteAllText(NumbersPath, "7");
        Assert.Equal(41, NewBoard().NewPlayer()!.Number);
        File.WriteAllText(NumbersPath, "60");
        Assert.Equal(61, NewBoard().NewPlayer()!.Number);   // a saved count above the board wins
    }

    [Fact]
    public void An_unreadable_counter_goes_on_from_the_board_and_says_so()
    {
        NewBoard().Record(new TwentyFourPlayer(Guid.NewGuid(), 12), 2);
        File.WriteAllText(NumbersPath, "twelve");
        var log = new CapturingLogger<TwentyFourBoard>();

        var board = new TwentyFourBoard(_env, log, () => _now);

        Assert.Equal(13, board.NewPlayer()!.Number);
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(TwentyFourBoard.NumbersFileName));
    }

    [Fact]
    public void A_number_that_cant_be_saved_isnt_given_out()
    {
        // Given anyway, a restart would give it again, and two players would share a name.
        var board = NewBoard();
        board.NewPlayer();
        var dataDir = Path.Combine(_env.ContentRootPath, "App_Data");
        Directory.Delete(dataDir, recursive: true);
        File.WriteAllText(dataDir, "a file where the folder was");

        Assert.Null(board.NewPlayer());

        File.Delete(dataDir);
        Directory.CreateDirectory(dataDir);
        Assert.Equal(2, board.NewPlayer()!.Number);   // the one that failed wasn't used up
    }

    [Fact]
    public void A_board_that_cant_be_saved_still_counts_until_a_restart()
    {
        var board = NewBoard();
        var dataDir = Path.Combine(_env.ContentRootPath, "App_Data");
        Directory.Delete(dataDir, recursive: true);
        File.WriteAllText(dataDir, "a file where the folder was");   // every save now fails

        board.Record(Ann, 3);   // throws if the failure escaped

        Assert.Equal([(Ann, 3)], Rows(board.AllTime));
    }

    // ---- the shadow ban --------------------------------------------------------------

    [Fact]
    public void A_hidden_player_leaves_both_lists_but_still_sees_their_own_runs()
    {
        var board = NewBoard();
        board.Record(Bo, 2);
        board.Record(Ann, 5);

        board.Hide(Ann);

        Assert.Equal([(Bo, 2)], Rows(board.Today));
        Assert.Equal([(Bo, 2)], Rows(board.AllTime));
        Assert.Equal([(Bo, 2)], Rows(board.AllTimeAsSeenBy(Bo.Id)));
        Assert.Equal([(Ann, 5), (Bo, 2)], Rows(board.TodayAsSeenBy(Ann.Id)));
        Assert.Equal([(Ann, 5), (Bo, 2)], Rows(board.AllTimeAsSeenBy(Ann.Id)));
        Assert.Equal([(Bo, 2)], Rows(NewBoard().AllTime));   // and the saved board agrees
    }

    [Fact]
    public void A_hidden_players_later_runs_reach_only_their_own_view_ranked_where_theyd_be()
    {
        var board = NewBoard();
        board.Hide(Ann);
        board.Record(Bo, 4);

        board.Record(Ann, 3);
        Assert.Equal([(Bo, 4), (Ann, 3)], Rows(board.AllTimeAsSeenBy(Ann.Id)));
        board.Record(Ann, 6);
        board.Record(Ann, 2);   // a shorter later run doesn't lower their best

        Assert.Equal([(Bo, 4)], Rows(board.Today));
        Assert.Equal([(Bo, 4)], Rows(board.AllTime));
        Assert.Equal([(Ann, 6), (Bo, 4)], Rows(board.TodayAsSeenBy(Ann.Id)));
        Assert.Equal([(Ann, 6), (Bo, 4)], Rows(board.AllTimeAsSeenBy(Ann.Id)));
    }

    [Fact]
    public void A_hidden_player_takes_no_place_from_anyone()
    {
        var board = NewBoard();
        var players = Enumerable.Range(0, 10).Select(_ => NewPlayer()).ToArray();
        for (var i = 0; i < 10; i++)
        {
            board.Record(players[i], i + 2);
        }
        board.Hide(Ann);

        board.Record(Ann, 50);

        Assert.Equal(10, board.AllTime.Count);
        Assert.DoesNotContain(board.AllTime, e => e.Player == Ann);
        var theirs = board.AllTimeAsSeenBy(Ann.Id);
        Assert.Equal(10, theirs.Count);
        Assert.Equal((Ann, 50), (theirs[0].Player, theirs[0].Run));
    }

    [Fact]
    public void A_hidden_players_today_ends_at_midnight_like_everyone_elses()
    {
        var board = NewBoard();
        board.Hide(Ann);
        board.Record(Ann, 4);

        _now = _now.AddDays(1);

        Assert.Empty(board.TodayAsSeenBy(Ann.Id));
        Assert.Equal([(Ann, 4)], Rows(board.AllTimeAsSeenBy(Ann.Id)));
    }

    [Fact]
    public void Hiding_someone_again_changes_nothing()
    {
        // The page asks on every answer once a visit is caught.
        var board = NewBoard();
        board.Hide(Ann);
        board.Record(Ann, 5);

        board.Hide(Ann);

        Assert.Equal([(Ann, 5)], Rows(board.AllTimeAsSeenBy(Ann.Id)));
        Assert.Equal([Ann.Id], HiddenLines());
    }

    [Fact]
    public void A_hidden_player_stays_hidden_after_a_restart()
    {
        NewBoard().Hide(Ann);

        var reloaded = NewBoard();
        reloaded.Record(Ann, 7);

        Assert.Empty(reloaded.AllTime);
        Assert.Equal([(Ann, 7)], Rows(reloaded.AllTimeAsSeenBy(Ann.Id)));
    }

    [Fact]
    public void Past_the_most_kept_hidden_the_longest_hidden_is_let_back_on()
    {
        var board = new TwentyFourBoard(_env, NullLogger<TwentyFourBoard>.Instance, () => _now, maxHidden: 2);
        var cy = NewPlayer();
        board.Hide(Ann);
        _now = _now.AddMinutes(1);
        board.Hide(Bo);
        _now = _now.AddMinutes(1);
        board.Hide(cy);

        board.Record(Ann, 1);
        board.Record(Bo, 1);
        board.Record(cy, 1);
        Assert.Equal([(Ann, 1)], Rows(board.AllTime));

        // The file only ever grows while running; the next start tidies it to the newest two.
        var restarted = new TwentyFourBoard(_env, NullLogger<TwentyFourBoard>.Instance, () => _now, maxHidden: 2);
        Assert.Equal([Bo.Id, cy.Id], HiddenLines());
        restarted.Record(Ann, 2);
        Assert.Equal([(Ann, 2)], Rows(restarted.AllTime));
    }

    [Fact]
    public void The_hidden_list_holds_ids_and_times_only()
    {
        NewBoard().Hide(Ann);

        var text = _env.ReadDataFile(TwentyFourBoard.HiddenFileName);
        var fields = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(l => JsonDocument.Parse(l).RootElement.EnumerateObject().Select(p => p.Name)).Distinct().Order();

        Assert.Equal(["HiddenAtUtc", "Player"], fields);
        Assert.DoesNotContain(Ann.Name, text);
    }

    [Fact]
    public void Hiding_someone_adds_a_line_and_leaves_an_edit_made_since_the_start_alone()
    {
        // The author lets Ann back on by deleting her line while the site runs. A hide in the
        // moment before the restart mustn't write the old list back over the edit.
        var board = NewBoard();
        board.Hide(Ann);
        File.WriteAllText(HiddenPath, "");

        board.Hide(Bo);

        Assert.Equal([Bo.Id], HiddenLines());
        var restarted = NewBoard();
        restarted.Record(Ann, 3);
        Assert.Equal([(Ann, 3)], Rows(restarted.AllTime));
    }

    [Fact]
    public void A_ban_by_hand_is_the_id_on_a_line_and_takes_their_rows_off_at_the_start()
    {
        var board = NewBoard();
        board.Record(Ann, 6);
        board.Record(Bo, 2);
        File.WriteAllText(HiddenPath, $"{Ann.Id}\n");

        var restarted = NewBoard();

        Assert.Equal([(Bo, 2)], Rows(restarted.Today));
        Assert.Equal([(Bo, 2)], Rows(restarted.AllTime));
        Assert.Equal([(Ann, 6), (Bo, 2)], Rows(restarted.AllTimeAsSeenBy(Ann.Id)));   // still theirs to see
        Assert.Equal([(Bo, 2)], Rows(NewBoard().AllTime));                         // and saved without them
    }

    [Fact]
    public void An_unreadable_line_is_skipped_a_repeat_dropped_and_the_rest_still_count()
    {
        var log = new CapturingLogger<TwentyFourBoard>();
        File.WriteAllText(HiddenPath, $"{{\"Player\":\"{Ann.Id}\",\"HiddenAtUtc\":\"2026-10-29T18:00:00Z\"}}\nnot a line,\n{Bo.Id}\n{Ann.Id}\n");

        var board = new TwentyFourBoard(_env, log, () => _now);
        board.Record(Ann, 1);
        board.Record(Bo, 1);

        Assert.Empty(board.AllTime);
        Assert.Equal([Ann.Id, Bo.Id], HiddenLines());   // tidied: the bad line and the repeat gone
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Skipped 1 unreadable line"));
    }

    [Fact]
    public void An_unreadable_board_is_set_aside_and_the_game_goes_on()
    {
        var log = new CapturingLogger<TwentyFourBoard>();
        _env.WriteDataFile(TwentyFourBoard.FileName, "{ \"Day\": \"2026-10-29\", \"Today\": [ ,");

        var board = new TwentyFourBoard(_env, log, () => _now);
        board.Record(Ann, 2);

        Assert.Equal([(Ann, 2)], Rows(board.AllTime));
        var aside = Assert.Single(Directory.GetFiles(Path.Combine(_env.ContentRootPath, "App_Data"), "twentyfour-board.unreadable-*.json"));
        Assert.Contains("\"Today\": [ ,", File.ReadAllText(aside));   // kept as it was, for mending
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(Path.GetFileName(aside)));
    }

    [Fact]
    public void The_log_names_who_was_hidden_with_the_start_of_their_id()
    {
        // Names can repeat; the id's start is what to look for in the hidden list.
        var log = new CapturingLogger<TwentyFourBoard>();
        new TwentyFourBoard(_env, log, () => _now).Hide(Ann);

        var entry = Assert.Single(log.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains($"{Ann.Name} (11111111)", entry.Message);
    }

    public void Dispose() => _env.Dispose();
}

/// <summary>The names players go by: an animal from their id, and their number.</summary>
public class PlayerNameTests
{
    [Fact]
    public void A_name_is_the_animal_from_the_id_and_the_number()
    {
        // Pinned, so a change to how animals are picked is a deliberate one: it changes everyone's.
        var id = Guid.Parse("3f2b8c1e-9d4a-4e6f-8a2b-5c7d9e1f3a4b");

        Assert.Equal("Penguin 17", PlayerName.For(id, 17));
        Assert.Equal("Penguin 17", new TwentyFourPlayer(id, 17).Name);
        Assert.Equal("Penguin 1", PlayerName.For(id, 1));
    }

    [Fact]
    public void Every_animal_turns_up_and_only_those()
    {
        var animals = Enumerable.Range(0, 5000)
            .Select(_ => PlayerName.For(Guid.NewGuid(), 1).Split(' ')[0])
            .ToHashSet();

        Assert.Equal(PlayerName.Animals.Order(), animals.Order());
    }
}
