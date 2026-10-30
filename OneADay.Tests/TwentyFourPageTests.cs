using System.Reflection;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using OneADay.Components.Pages;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The Twenty Four page (PRD 07): what its rules tell the player, its keypad — the dealt
/// numbers and the symbols as buttons, so a phone player never swaps keyboards — the Impossible
/// button, and the run and its scoreboard. The keypad only types; every answer is still judged
/// by the one check.
/// </summary>
public class TwentyFourPageTests : BunitContext
{
    private readonly TestEnvironment _env = new();
    private readonly InMemoryPlayerStorage _player = new();
    private readonly TwentyFourBoard _board;

    public TwentyFourPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMetrics(_env);
        Services.AddScoped<CurrentTeaserContext>();
        _board = Services.AddTwentyFour(_env, _player);
    }

    private IRenderedComponent<TwentyFour> Dealt(params int[] hand)
    {
        var page = Render<TwentyFour>();
        Deal(page, hand);
        return page;
    }

    /// <summary>Puts this hand on the table, as if it were the one dealt.</summary>
    private static void Deal(IRenderedComponent<TwentyFour> page, params int[] hand)
    {
        typeof(TwentyFour).GetField("_hand", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(page.Instance, hand);
        page.Render();
    }

    private static void Solve(IRenderedComponent<TwentyFour> page, string answer)
    {
        page.Find("input.tf-input").Input(answer);
        page.Find("button.oad-btn-green").Click();
    }

    private static string Message(IRenderedComponent<TwentyFour> page) => page.Find(".tf-message").TextContent.Trim();

    private static string Run(IRenderedComponent<TwentyFour> page) => page.Find(".tf-run strong").TextContent.Trim();

    /// <summary>Taps the first key showing this, among those that can still be tapped.</summary>
    private static void Tap(IRenderedComponent<TwentyFour> page, string key) =>
        page.FindAll("button.tf-key")
            .First(b => (b.GetAttribute("aria-label") == key || b.TextContent.Trim() == key)
                        && !b.HasAttribute("disabled"))
            .Click();

    private static void TapAll(IRenderedComponent<TwentyFour> page, string keys)
    {
        foreach (var key in keys.Split(' '))
        {
            Tap(page, key);
        }
    }

    private static string Answer(IRenderedComponent<TwentyFour> page) =>
        page.Find("input.tf-input").GetAttribute("value") ?? "";

    private static bool[] NumberKeysOut(IRenderedComponent<TwentyFour> page) =>
        page.FindAll("button.tf-key-number").Select(b => b.HasAttribute("disabled")).ToArray();

    /// <summary>
    /// Calls a handler the way a forged event would, then renders — a direct call alone
    /// doesn't, and an unchanged page would hide a handler that did change something.
    /// </summary>
    private static async Task CallAnyway(IRenderedComponent<TwentyFour> page, string method, params object[] args)
    {
        var handler = typeof(TwentyFour).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"'{method}' not found on TwentyFour — was it renamed?");
        await page.InvokeAsync(() => { handler.Invoke(page.Instance, args); });
        page.Render();
    }

    // ---- the rules -------------------------------------------------------------------

    [Fact]
    public void The_rules_say_not_every_hand_can_make_24_and_nothing_about_how()
    {
        // Players took every hand to be possible and got frustrated. The note is about the
        // deck, so it may say that much — and nothing that points at a method.
        var rules = Render<TwentyFour>().FindAll(".oad-box")[0].TextContent;

        Assert.Contains("Not every hand can make 24.", rules);
        Assert.DoesNotContain("fraction", rules, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_rules_say_what_adds_to_a_run_and_what_ends_it()
    {
        var rules = Regex.Replace(Render<TwentyFour>().FindAll(".oad-box")[0].TextContent, @"\s+", " ");

        Assert.Contains("If you think yours can't, press Impossible.", rules);
        Assert.Contains("Each hand you solve, or correctly call impossible, adds +1 to your run.", rules);
        Assert.Contains("Pass deals a fresh hand but ends your run, and so does calling a hand impossible when it can be done.", rules);
    }

    // ---- the answer box --------------------------------------------------------------

    [Fact]
    public void The_answer_box_is_a_text_box_named_by_its_heading()
    {
        // app.css styles every box by input[type="text"]; without the type this one was a bare
        // browser box (cold review, 2026-10-15). The heading names it for screen readers.
        var page = Render<TwentyFour>();
        var box = page.Find("input.tf-input");

        Assert.Equal("text", box.GetAttribute("type"));
        var label = page.Find($"#{box.GetAttribute("aria-labelledby")}");
        Assert.Equal("Your answer:", label.TextContent.Trim());
    }

    // ---- the keypad ------------------------------------------------------------------

    [Fact]
    public void The_keys_are_the_hand_as_dealt_then_the_symbols()
    {
        var page = Dealt(3, 10, 1, 7);

        Assert.Equal(["3", "10", "1", "7"],
            page.FindAll("button.tf-key-number").Select(b => b.TextContent.Trim()));
        Assert.Equal(["+", "−", "×", "÷", "(", ")"],
            page.FindAll("button.tf-key-symbol").Select(b => b.TextContent.Trim()));
        Assert.Single(page.FindAll("button.tf-key-delete"));
    }

    [Theory]
    [InlineData(new[] { 6, 6, 6, 6 }, "6 + 6 + 6 + 6", "6 + 6 + 6 + 6")]
    [InlineData(new[] { 10, 4, 2, 2 }, "( 10 − 4 ) × 2 × 2", "(10 − 4) × 2 × 2")]
    [InlineData(new[] { 8, 3, 2, 2 }, "8 × 3 ÷ 2 × 2", "8 × 3 ÷ 2 × 2")]
    public void A_tapped_answer_reads_as_written_and_is_judged_like_a_typed_one(int[] hand, string taps, string shown)
    {
        var page = Dealt(hand);

        TapAll(page, taps);

        Assert.Equal(shown, Answer(page));
        page.Find("button.oad-btn-green").Click();
        Assert.Contains("tf-correct", page.Find(".tf-message").ClassList);
    }

    [Fact]
    public void Two_numbers_tapped_in_a_row_stay_two_numbers()
    {
        // Run together they'd be 38, a number the player never tapped. Kept apart, the check
        // names the real mistake: a missing symbol.
        var page = Dealt(3, 8, 1, 1);

        TapAll(page, "3 8");

        Assert.Equal("3 8", Answer(page));
        page.Find("button.oad-btn-green").Click();
        Assert.Contains("isn't a complete answer", page.Find(".tf-message").TextContent);
    }

    [Fact]
    public void Delete_takes_back_one_tap_at_a_time()
    {
        var page = Dealt(10, 4, 2, 2);
        TapAll(page, "10 × (");

        Tap(page, "Delete");
        Assert.Equal("10 ×", Answer(page));
        Tap(page, "Delete");
        Assert.Equal("10", Answer(page));
        Tap(page, "Delete");               // the 10 went in with one tap, and goes with one
        Assert.Equal("", Answer(page));

        Assert.True(page.Find("button.tf-key-delete").HasAttribute("disabled"));
    }

    [Fact]
    public void A_number_key_goes_out_with_its_card_and_comes_back_with_it()
    {
        // Duplicates spend left to right, like the cards — and from typed text as well as taps.
        var page = Dealt(9, 4, 9, 2);

        Tap(page, "9");
        Assert.Equal([true, false, false, false], NumberKeysOut(page));
        TapAll(page, "+ 9");
        Assert.Equal([true, false, true, false], NumberKeysOut(page));
        Tap(page, "Delete");
        Assert.Equal([true, false, false, false], NumberKeysOut(page));

        page.Find("input.tf-input").Input("9 + 9 + 4");
        Assert.Equal([true, true, true, false], NumberKeysOut(page));
        Assert.Equal(3, page.FindAll(".tf-card.tf-card-used").Count);
    }

    [Fact]
    public async Task After_a_solve_the_keypad_is_locked_on_the_server_too()
    {
        var page = Dealt(6, 6, 6, 6);
        TapAll(page, "6 + 6 + 6 + 6");
        page.Find("button.oad-btn-green").Click();

        Assert.All(page.FindAll("button.tf-key"), key => Assert.True(key.HasAttribute("disabled")));

        // What a client driving the live connection could still send.
        await CallAnyway(page, "Tap", "+");
        await CallAnyway(page, "DeleteLast");
        Assert.Equal("6 + 6 + 6 + 6", Answer(page));
    }

    [Fact]
    public void The_keypad_stops_at_the_answer_length_limit()
    {
        var page = Dealt(6, 6, 6, 6);
        page.Find("input.tf-input").Input(new string('(', TwentyFourGame.MaxExpressionLength - 1));

        Tap(page, "open bracket");
        Assert.Equal(TwentyFourGame.MaxExpressionLength, Answer(page).Length);   // the last that fits
        Tap(page, "open bracket");
        Assert.Equal(TwentyFourGame.MaxExpressionLength, Answer(page).Length);
    }

    // ---- Impossible ------------------------------------------------------------------

    [Fact]
    public void Calling_an_impossible_hand_impossible_counts_like_a_solve()
    {
        var page = Dealt(1, 1, 1, 1);

        page.Find("button.tf-call").Click();

        Assert.Equal("Right, these four can't make 24. That starts a run.", Message(page));
        Assert.Contains("tf-correct", page.Find(".tf-message").ClassList);
        Assert.Equal("1", Run(page));
        // Twice: in the answer box for a laptop, and in a box of its own for a phone.
        Assert.Equal(2, page.FindAll("button.tf-next").Count);
    }

    [Fact]
    public void Calling_a_possible_hand_impossible_ends_the_run_and_says_only_that_it_could_be_done()
    {
        // The author's decision, 2026-10-29: whether it could be done, never how. 3 3 8 8 has one
        // answer, through a fraction, and nothing of it may show.
        var page = Dealt(6, 6, 6, 6);
        Solve(page, "6 + 6 + 6 + 6");
        page.Find("button.tf-next").Click();
        Deal(page, 3, 3, 8, 8);

        page.Find("button.tf-call").Click();

        Assert.Equal("It could be done. Your run of 1 ends here.", Message(page));
        Assert.Contains("tf-wrong", page.Find(".tf-message").ClassList);
        Assert.Equal("0", Run(page));
        var text = page.Find(".tf-page").TextContent;
        Assert.DoesNotContain(TwentyFourGame.FindSolution([3, 3, 8, 8])!, text);
        Assert.DoesNotContain("/", text);   // no division anywhere: no worked answer
    }

    [Fact]
    public void A_wrong_call_with_no_run_going_just_says_it_could_be_done()
    {
        var page = Dealt(6, 6, 6, 6);

        page.Find("button.tf-call").Click();

        Assert.Equal("It could be done.", Message(page));
    }

    [Fact]
    public async Task A_called_hand_is_over_on_the_server_too()
    {
        // The buttons go out with the round, but only in the browser. These are what a client
        // driving the live connection could still send.
        var page = Dealt(1, 1, 1, 1);
        page.Find("button.tf-call").Click();

        Assert.True(page.Find("input.tf-input").HasAttribute("disabled"));
        Assert.True(page.Find("button.tf-call").HasAttribute("disabled"));
        Assert.True(page.Find("button.tf-pass").HasAttribute("disabled"));
        Assert.All(page.FindAll("button.tf-key"), key => Assert.True(key.HasAttribute("disabled")));

        await CallAnyway(page, "CallImpossible");
        await CallAnyway(page, "Pass");
        await CallAnyway(page, "Tap", "1");
        Assert.Equal("1", Run(page));
        Assert.Equal("", Answer(page));
        Assert.Equal([1, 1, 1, 1], HandOnTable(page));
    }

    [Fact]
    public void A_called_hand_shows_every_card_lit()
    {
        // As after a solve: the round is over, and half-dimmed cards would read as a hand in play.
        var page = Dealt(1, 1, 1, 1);
        page.Find("input.tf-input").Input("1 + 1");
        Assert.Equal(2, page.FindAll(".tf-card.tf-card-used").Count);

        page.Find("button.tf-call").Click();

        Assert.Empty(page.FindAll(".tf-card.tf-card-used"));
    }

    [Fact]
    public async Task The_space_bar_deals_after_a_call_as_after_a_solve()
    {
        var page = Dealt(6, 6, 6, 6);
        page.Find("button.tf-call").Click();

        await page.InvokeAsync(() => page.Instance.DealFromSpaceBar());

        Assert.Empty(page.FindAll(".tf-message"));
        Assert.False(page.Find("button.tf-call").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Deal_a_new_hand_does_nothing_while_a_hand_is_in_play()
    {
        // A double tap, or a click sent again before the browser confirms the redraw, reaches
        // the server after the new hand is out. Dealing again then would be a free Pass that
        // keeps the run.
        var page = Dealt(6, 6, 6, 6);
        Solve(page, "6 + 6 + 6 + 6");
        page.Find("button.tf-next").Click();
        Deal(page, 3, 3, 8, 8);

        await CallAnyway(page, "NewHand", new object?[] { null }!);

        Assert.Equal([3, 3, 8, 8], HandOnTable(page));
        Assert.Equal("1", Run(page));
    }

    private static int[] HandOnTable(IRenderedComponent<TwentyFour> page) =>
        (int[])typeof(TwentyFour).GetField("_hand", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(page.Instance)!;

    // ---- the run ---------------------------------------------------------------------

    [Fact]
    public void Solves_in_a_row_make_a_run_and_a_wrong_answer_leaves_it_alone()
    {
        var page = Dealt(6, 6, 6, 6);
        Solve(page, "6 + 6 + 6 + 6");
        Assert.Equal("Correct! 6 + 6 + 6 + 6 = 24. That starts a run.", Message(page));

        page.Find("button.tf-next").Click();
        Deal(page, 6, 6, 6, 6);
        Solve(page, "6 + 6 + 6");                // wrong: try again as often as you like
        Assert.Equal("1", Run(page));
        Solve(page, "6 * 6 - 6 - 6");

        Assert.Equal("Correct! 6 * 6 - 6 - 6 = 24. That's 2 in a row.", Message(page));
        Assert.Equal("2", Run(page));
    }

    [Fact]
    public void Pass_ends_the_run_and_still_says_nothing_about_the_hand()
    {
        var page = Dealt(1, 1, 1, 1);
        page.Find("button.tf-call").Click();
        page.Find("button.tf-next").Click();

        page.Find("button.tf-pass").Click();

        Assert.Equal("Passed, ending your run of 1. Here's a fresh hand.", Message(page));
        Assert.Equal("0", Run(page));

        page.Find("button.tf-pass").Click();
        Assert.Equal("Passed. Here's a fresh hand.", Message(page));
    }

    [Fact]
    public void Enter_submits_on_a_hand_that_pass_dealt()
    {
        // Until 2026-10-29 Enter did nothing on a passed hand until Submit had been clicked once.
        var page = Dealt(1, 1, 1, 1);
        page.Find("button.tf-pass").Click();
        Deal(page, 6, 6, 6, 6);

        page.Find("input.tf-input").Input("6 + 6 + 6 + 6");
        page.Find("input.tf-input").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        Assert.Contains("tf-correct", page.Find(".tf-message").ClassList);
    }

    // ---- the board -------------------------------------------------------------------

    private string Note(IRenderedComponent<TwentyFour> page) => page.Find(".tf-board-note").TextContent.Trim();

    [Fact]
    public void A_new_player_gets_the_next_number_at_their_first_hand_right_and_keeps_it()
    {
        // Numbers count people who've scored: opening the page, passing or a wrong call gives none.
        var page = Dealt(6, 6, 6, 6);
        Assert.Equal("Get a hand right and you'll get a name on the board.", Note(page));
        page.Find("button.tf-call").Click();          // wrong
        page.Find("button.tf-next").Click();
        page.Find("button.tf-pass").Click();
        Assert.Null(_player.Player);

        Deal(page, 1, 1, 1, 1);
        page.Find("button.tf-call").Click();          // right
        var me = _player.Player!;
        Assert.Equal(1, me.Number);
        Assert.Equal($"You play as {me.Name}.", Note(page));

        page.Find("button.tf-next").Click();
        SolveAndDeal(page);
        Assert.Equal(me, _player.Player);
        Assert.Equal(2, _board.NewPlayer()!.Number);   // one number used, not one per hand
    }

    [Fact]
    public void A_returning_player_keeps_their_name_and_their_runs_go_on_the_board()
    {
        var me = new TwentyFourPlayer(Guid.NewGuid(), 9);
        _player.Player = me;
        var page = Dealt(1, 1, 1, 1);

        page.Find("button.tf-call").Click();

        Assert.Equal(me, _player.Player);
        Assert.Equal((me, 1), (_board.AllTime.Single().Player, _board.AllTime.Single().Run));
        Assert.Equal((me, 1), (_board.Today[0].Player, _board.Today[0].Run));
        Assert.Equal(1, _board.NewPlayer()!.Number);   // no number given out for them
    }

    [Fact]
    public void The_board_names_each_player_and_marks_your_own_row()
    {
        var other = new TwentyFourPlayer(Guid.NewGuid(), 4);
        _board.Record(other, 7);
        var me = new TwentyFourPlayer(Guid.NewGuid(), 5);
        _player.Player = me;
        _board.Record(me, 3);

        var rows = Render<TwentyFour>().FindAll(".tf-board-list")[1].QuerySelectorAll("li");

        Assert.Equal(
            [$"1 {other.Name} 7", $"2 {me.Name} (you) 3"],
            rows.Select(r => Regex.Replace(r.TextContent, @"\s+", " ").Trim()));
        Assert.Equal([false, true], rows.Select(r => r.ClassList.Contains("tf-row-you")));
    }

    [Fact]
    public void An_empty_board_says_how_to_get_on_it()
    {
        var lists = Render<TwentyFour>().FindAll(".tf-board-list");

        Assert.Equal("No runs yet today. Solve a hand to get on the board.", lists[0].QuerySelector(".tf-board-empty")!.TextContent);
        Assert.Equal("No runs yet.", lists[1].QuerySelector(".tf-board-empty")!.TextContent);
    }

    [Fact]
    public void A_browser_that_stores_nothing_still_plays_but_stays_off_the_board()
    {
        _player.FailWith = new Microsoft.JSInterop.JSException("localStorage is blocked");
        var page = Dealt(1, 1, 1, 1);

        page.Find("button.tf-call").Click();

        Assert.Equal("1", Run(page));
        Assert.Empty(_board.AllTime);
        Assert.Empty(page.FindAll(".tf-board-note"));
    }

    /// <summary>Solves 6 6 6 6 and deals the next hand.</summary>
    private static void SolveAndDeal(IRenderedComponent<TwentyFour> page)
    {
        Deal(page, 6, 6, 6, 6);
        Solve(page, "6 + 6 + 6 + 6");
        page.Find("button.tf-next").Click();
    }

    [Fact]
    public async Task A_returning_player_who_scores_before_the_browser_answers_keeps_their_name()
    {
        // No new player is made while the browser is still saying whether it has one: that would
        // replace the player it has.
        var me = new TwentyFourPlayer(Guid.NewGuid(), 4);
        _player.Player = me;
        _player.Hold = new TaskCompletionSource();
        var page = Render<TwentyFour>();

        SolveAndDeal(page);
        SolveAndDeal(page);
        await page.InvokeAsync(() => _player.Hold.SetResult());

        Assert.Equal(me, _player.Player);
        Assert.Equal((me, 2), (_board.AllTime[0].Player, _board.AllTime[0].Run));
    }

    [Fact]
    public async Task A_new_player_who_scores_before_the_browser_answers_gets_a_name_once_it_does()
    {
        _player.Hold = new TaskCompletionSource();
        var page = Render<TwentyFour>();

        SolveAndDeal(page);
        Assert.Null(_player.Player);   // not yet: the browser may have one already
        await page.InvokeAsync(() => _player.Hold.SetResult());

        var me = _player.Player!;
        Assert.Equal(1, me.Number);
        Assert.Equal((me, 1), (_board.AllTime[0].Player, _board.AllTime[0].Run));
    }

    [Fact]
    public void A_browser_that_wont_save_its_player_plays_on_but_stays_off_the_board()
    {
        // It can read, so it isn't caught by the storage-failure case: it says it has no player,
        // then can't keep the one it's given. On the board, each visit would be a new name. It's
        // asked once a visit, so it doesn't use up a number on every hand.
        _player.FailWritesWith = new Microsoft.JSInterop.JSException("QuotaExceededError");
        var page = Dealt(1, 1, 1, 1);

        page.Find("button.tf-call").Click();
        page.Find("button.tf-next").Click();
        SolveAndDeal(page);

        Assert.Equal("2", Run(page));
        Assert.Empty(_board.AllTime);
        Assert.Empty(page.FindAll(".tf-board-note"));
        Assert.Equal(2, _board.NewPlayer()!.Number);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void A_stored_player_without_a_number_counts_as_none(int number)
    {
        // Our own code never stores one; a value that reads as one is treated as a new player.
        _player.Player = new TwentyFourPlayer(Guid.NewGuid(), number);
        var page = Dealt(1, 1, 1, 1);

        page.Find("button.tf-call").Click();

        Assert.Equal(1, _player.Player!.Number);
    }

    [Fact]
    public void A_stored_player_without_an_id_counts_as_none()
    {
        _player.Player = new TwentyFourPlayer(Guid.Empty, 8);
        var page = Dealt(1, 1, 1, 1);

        page.Find("button.tf-call").Click();

        Assert.NotEqual(Guid.Empty, _player.Player!.Id);
        Assert.Equal(1, _player.Player.Number);
    }

    // ---- folding the board -----------------------------------------------------------

    private static bool BoardShown(IRenderedComponent<TwentyFour> page) =>
        !page.Find("#tf-board-body").HasAttribute("hidden");

    [Fact]
    public void The_board_folds_to_its_title_and_opens_again_and_the_browser_is_told_which()
    {
        var page = Render<TwentyFour>();
        Assert.True(BoardShown(page));
        Assert.Equal("true", page.Find("button.tf-board-toggle").GetAttribute("aria-expanded"));

        page.Find("button.tf-board-toggle").Click();

        Assert.False(BoardShown(page));
        Assert.Equal("false", page.Find("button.tf-board-toggle").GetAttribute("aria-expanded"));
        Assert.Contains("tf-board-closed", page.Find(".tf-board").ClassList);
        Assert.Equal(true, JSInterop.Invocations["oadTwentyFour.setBoardHidden"].Last().Arguments[0]);

        page.Find("button.tf-board-toggle").Click();

        Assert.True(BoardShown(page));
        Assert.DoesNotContain("tf-board-closed", page.Find(".tf-board").ClassList);
        Assert.Equal(false, JSInterop.Invocations["oadTwentyFour.setBoardHidden"].Last().Arguments[0]);
    }

    [Fact]
    public void A_board_folded_last_time_starts_folded()
    {
        JSInterop.Setup<bool>("oadTwentyFour.boardHidden").SetResult(true);

        var page = Render<TwentyFour>();

        Assert.False(BoardShown(page));
        Assert.Equal("false", page.Find("button.tf-board-toggle").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void A_browser_that_cant_say_starts_with_the_board_open_and_still_folds_it()
    {
        JSInterop.Setup<bool>("oadTwentyFour.boardHidden").SetException(new JSException("storage is off"));
        JSInterop.SetupVoid("oadTwentyFour.setBoardHidden", _ => true).SetException(new JSException("storage is off"));

        var page = Render<TwentyFour>();
        Assert.True(BoardShown(page));

        page.Find("button.tf-board-toggle").Click();

        Assert.False(BoardShown(page));
    }

    // ---- on a laptop -----------------------------------------------------------------
    //
    // From 1100px the board sits beside the game, so the page fits a laptop's window (PRD 09).
    // The stylesheet folds the rules marked tf-more and hides the parts marked for a phone; these
    // tests hold what it relies on in the markup.

    private static string Normalised(AngleSharp.Dom.IElement e) => Regex.Replace(e.TextContent, @"\s+", " ").Trim();

    [Fact]
    public void Folded_on_a_laptop_the_rules_still_say_what_to_do_and_that_not_every_hand_can_make_24()
    {
        // "Not every hand can make 24" came from players' frustration (feedback, 2026-10-07), so
        // it never folds away.
        var rules = Render<TwentyFour>().Find(".tf-rules-box");

        var shown = rules.QuerySelectorAll("p:not(.tf-more)").Select(Normalised).ToArray();
        var folded = rules.QuerySelectorAll("p.tf-more").Select(Normalised).ToArray();

        Assert.Equal(2, shown.Length);
        Assert.StartsWith("Make 24 from all four numbers", shown[0]);
        Assert.StartsWith("Not every hand can make 24.", shown[1]);
        Assert.Equal(3, folded.Length);
        Assert.StartsWith("Each number must be used exactly once.", folded[0]);
        Assert.StartsWith("Each hand you solve", folded[1]);
        Assert.StartsWith("For example", folded[2]);
    }

    [Fact]
    public void How_to_play_opens_the_folded_rules_and_Show_less_folds_them_again()
    {
        var page = Render<TwentyFour>();

        Assert.DoesNotContain("tf-rules-open", page.Find(".tf-rules-box").ClassList);
        Assert.Equal("false", page.Find("button.tf-how").GetAttribute("aria-expanded"));
        Assert.Equal("How to play", page.Find("button.tf-how").TextContent.Trim());

        page.Find("button.tf-how").Click();

        Assert.Contains("tf-rules-open", page.Find(".tf-rules-box").ClassList);
        Assert.Equal("true", page.Find("button.tf-how").GetAttribute("aria-expanded"));
        Assert.Equal("Show less", page.Find("button.tf-how").TextContent.Trim());

        page.Find("button.tf-how").Click();

        Assert.DoesNotContain("tf-rules-open", page.Find(".tf-rules-box").ClassList);
        Assert.Equal("How to play", page.Find("button.tf-how").TextContent.Trim());
    }

    [Fact]
    public void How_to_play_names_every_paragraph_it_opens()
    {
        // They're before it in the page as well as after, so a screen reader needs telling where
        // they are (cold review, 2026-10-30).
        var page = Render<TwentyFour>();

        var controlled = page.Find("button.tf-how").GetAttribute("aria-controls")!.Split(' ');
        var folded = page.FindAll(".tf-rules-box p.tf-more").Select(p => p.Id).ToArray();

        Assert.Equal(3, folded.Length);
        Assert.Equal(folded, controlled);
    }

    [Fact]
    public void Once_a_hand_is_over_a_laptop_deals_the_next_from_where_the_buttons_were()
    {
        var page = Dealt(1, 1, 1, 1);
        Assert.Empty(page.FindAll(".tf-next-here"));
        Assert.DoesNotContain("tf-buttons-over", page.Find(".tf-buttons").ClassList);

        page.Find("button.tf-call").Click();

        Assert.Contains("tf-buttons-over", page.Find(".tf-buttons").ClassList);
        var answerBox = page.Find("input.tf-input").ParentElement!;
        Assert.NotNull(answerBox.QuerySelector(".tf-next-here button.tf-next"));
        Assert.NotNull(answerBox.QuerySelector(".tf-next-here .tf-next-hint"));
        // A phone's own box is still there, for the stylesheet to show there instead.
        Assert.Single(page.FindAll(".tf-next-box button.tf-next"));

        page.Find(".tf-next-here button.tf-next").Click();

        Assert.Empty(page.FindAll(".tf-next-here"));
        Assert.Empty(page.FindAll(".tf-next-box"));
        Assert.DoesNotContain("tf-buttons-over", page.Find(".tf-buttons").ClassList);
        Assert.False(page.Find("input.tf-input").HasAttribute("disabled"));
    }

    [Fact]
    public void The_parts_the_laptop_grid_places_are_the_pages_own_children()
    {
        // The grid places .tf-page's children: the banner across the top, the board on the right,
        // the rest down the left. Wrapped in anything, a part would lose its place.
        var page = Render<TwentyFour>().Find(".tf-page");

        Assert.Contains("oad-banner", page.Children[0].ClassList);
        Assert.Contains("tf-rules-box", page.Children[1].ClassList);
        Assert.Contains("tf-board", page.Children[^1].ClassList);
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
