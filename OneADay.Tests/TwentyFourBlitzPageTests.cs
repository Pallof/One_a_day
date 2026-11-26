using System.Reflection;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using OneADay.Components.Pages;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// 24 Blitz (PRD 07, issue 84), the Twenty Four page's timed mode: its own board, nothing dealt
/// before Go and the clock from Go, a wrong call costing a point, three passes, and the server
/// holding the clock, the score and the hand.
/// </summary>
public class TwentyFourBlitzPageTests : BunitContext
{
    private readonly TestEnvironment _env = new();
    private readonly InMemoryPlayerStorage _player = new();
    private readonly ManualClock _clock = new();
    private readonly TwentyFourBoard _board;
    private readonly MetricsStore _metrics;

    public TwentyFourBlitzPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _metrics = Services.AddMetrics(_env);
        Services.AddScoped<CurrentTeaserContext>();
        Services.AddSingleton<TimeProvider>(_clock);
        _board = Services.AddTwentyFour(_env, _player);
    }

    private IRenderedComponent<TwentyFour> Blitz() => Render<TwentyFour>(p => p.Add(c => c.Blitz, true));

    /// <summary>A Blitz page with Go pressed and this hand on the table.</summary>
    private IRenderedComponent<TwentyFour> Playing(params int[] hand)
    {
        var page = Blitz();
        page.Find("button.tf-go").Click();
        Deal(page, hand);
        return page;
    }

    private static void Deal(IRenderedComponent<TwentyFour> page, params int[] hand)
    {
        HandField.SetValue(page.Instance, hand);
        page.Render();
    }

    private static readonly FieldInfo HandField =
        typeof(TwentyFour).GetField("_hand", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static int[] HandOnTable(IRenderedComponent<TwentyFour> page) => (int[])HandField.GetValue(page.Instance)!;

    private static void Solve(IRenderedComponent<TwentyFour> page, string answer)
    {
        page.Find("input.tf-input").Input(answer);
        page.Find("button.oad-btn-green").Click();
    }

    private static string Text(IRenderedComponent<TwentyFour> page, string selector) =>
        Regex.Replace(page.Find(selector).TextContent, @"\s+", " ").Trim();

    private static string Clock(IRenderedComponent<TwentyFour> page) => Text(page, ".tf-clock");
    private static string Score(IRenderedComponent<TwentyFour> page) => Text(page, ".tf-score");
    private static string PassesLeft(IRenderedComponent<TwentyFour> page) => Text(page, ".tf-passes-left");
    private static string Message(IRenderedComponent<TwentyFour> page) => Text(page, ".tf-message");

    private Task Tick(IRenderedComponent<TwentyFour> page, TimeSpan by) => page.InvokeAsync(() => _clock.Tick(by));

    /// <summary>Calls a handler the way a forged event would, then renders.</summary>
    private static async Task CallAnyway(IRenderedComponent<TwentyFour> page, string method, params object[] args)
    {
        var handler = typeof(TwentyFour).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"'{method}' not found on TwentyFour. Was it renamed?");
        await page.InvokeAsync(async () =>
        {
            if (handler.Invoke(page.Instance, args) is Task task)
            {
                await task;
            }
        });
        page.Render();
    }

    private static (TwentyFourPlayer, int)[] Rows(IReadOnlyList<BoardEntry> board) =>
        board.Select(e => (e.Player, e.Run)).ToArray();

    // ---- the page and its rules ------------------------------------------------------

    [Fact]
    public void Blitz_has_its_own_address_and_is_the_Twenty_Four_page_in_its_timed_mode()
    {
        var route = Assert.Single(typeof(TwentyFourBlitz).GetCustomAttributes<RouteAttribute>());
        Assert.Equal("/twentyfour/blitz", route.Template);

        var page = Render<TwentyFourBlitz>();

        Assert.Equal("24 Blitz", page.Find(".oad-banner").TextContent.Trim());
        Assert.True(page.FindComponent<TwentyFour>().Instance.Blitz);
        Assert.Single(page.FindAll("button.tf-go"));
    }

    [Fact]
    public void Each_game_links_to_the_other_and_marks_itself()
    {
        foreach (var (page, current) in new[] { (Render<TwentyFour>(), "Normal"), (Blitz(), "Blitz") })
        {
            var links = page.FindAll(".tf-modes a");
            Assert.Equal(["twentyfour", "twentyfour/blitz"], links.Select(a => a.GetAttribute("href")));
            Assert.Equal(current, Assert.Single(links, a => a.GetAttribute("aria-current") == "page").TextContent.Trim());
        }
    }

    [Fact]
    public void The_rules_say_what_scores_and_how_long_and_still_nothing_about_how()
    {
        var rules = Blitz().Find(".tf-rules-box");
        var text = Regex.Replace(rules.TextContent, @"\s+", " ");

        Assert.Contains("You have 5 minutes. Each hand you solve, or correctly call impossible, scores +1. "
                        + "Calling a hand impossible when it can be done scores −1. You get 3 passes, which score nothing.", text);
        Assert.Contains("Not every hand can make 24.", text);
        Assert.DoesNotContain("fraction", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("your run", text, StringComparison.OrdinalIgnoreCase);   // the normal game's rule isn't Blitz's
        // Folded on a laptop, what a game scores still shows.
        Assert.Empty(rules.QuerySelectorAll(".tf-blitz-rules.tf-more"));
        Assert.Equal(rules.QuerySelectorAll("p.tf-more").Select(p => p.Id),
            rules.QuerySelector("button.tf-how")!.GetAttribute("aria-controls")!.Split(' '));
    }

    // ---- nothing before Go, and the clock from Go --------------------------------------

    [Fact]
    public async Task Before_Go_no_hand_is_dealt_so_none_reaches_the_page()
    {
        // Hidden by the stylesheet, a dealt hand would still be in the page sent to the browser.
        var page = Blitz();

        Assert.Empty(HandOnTable(page));
        Assert.Empty(page.FindAll(".tf-card"));
        Assert.Empty(page.FindAll("button.tf-key"));
        Assert.Empty(page.FindAll("input.tf-input"));
        Assert.Null(Services.GetRequiredService<CurrentTeaserContext>().Hand);
        Assert.Equal("The clock starts when you press Go.", Text(page, ".tf-start-note"));
        Assert.Equal("Go", Text(page, "button.tf-go"));
        Assert.Empty(_clock.Running);

        // What a client driving the live connection could send before Go: there's nothing to play.
        await CallAnyway(page, "Submit");
        await CallAnyway(page, "CallImpossible");
        await CallAnyway(page, "Pass");
        await CallAnyway(page, "Tap", "+");
        Assert.Empty(HandOnTable(page));
        Assert.Equal("", typeof(TwentyFour).GetField("_expression", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(page.Instance));
        Assert.Empty(page.FindAll(".tf-message"));
        Assert.Empty(_clock.Running);
    }

    [Fact]
    public async Task Go_deals_the_first_hand_and_starts_five_minutes_on_the_servers_clock()
    {
        var page = Blitz();
        _clock.Skip(TimeSpan.FromSeconds(30));   // time spent reading the rules isn't on the clock

        page.Find("button.tf-go").Click();

        Assert.Equal(4, page.FindAll(".tf-card").Count);
        Assert.Equal(HandOnTable(page), Services.GetRequiredService<CurrentTeaserContext>().Hand);
        Assert.Equal(("5:00", "0", "3"), (Clock(page), Score(page), PassesLeft(page)));
        var ticker = Assert.Single(_clock.Running);
        Assert.Equal(TimeSpan.FromSeconds(1), ticker.DueTime);

        await Tick(page, TimeSpan.FromSeconds(1));
        Assert.Equal("4:59", Clock(page));
        await Tick(page, TimeSpan.FromSeconds(298));
        Assert.Equal("0:01", Clock(page));
        Assert.Empty(page.FindAll(".tf-result"));

        // A late tick sets the next for when the clock next changes, so the last lands on 5:00.
        await Tick(page, TimeSpan.FromMilliseconds(400));
        Assert.Equal("0:01", Clock(page));
        Assert.Equal(TimeSpan.FromMilliseconds(600), ticker.DueTime);
    }

    [Fact]
    public async Task At_five_minutes_the_game_ends_on_its_own_and_nothing_sent_after_counts()
    {
        var me = new TwentyFourPlayer(Guid.NewGuid(), 3);
        _player.Player = me;
        var page = Playing(6, 6, 6, 6);
        Solve(page, "6 + 6 + 6 + 6");

        await Tick(page, TimeSpan.FromMinutes(5));

        Assert.Equal("Time's up. You scored 1.", Text(page, ".tf-result"));
        Assert.Equal("Play again", Text(page, "button.tf-go"));
        Assert.Empty(page.FindAll(".tf-card"));
        Assert.Empty(page.FindAll("input.tf-input"));
        Assert.Empty(_clock.Running);
        Assert.Null(Services.GetRequiredService<CurrentTeaserContext>().Hand);

        // What a client driving the live connection could still send.
        Deal(page, 1, 1, 1, 1);
        await CallAnyway(page, "Tap", "1");
        await CallAnyway(page, "Submit");
        await CallAnyway(page, "CallImpossible");
        await CallAnyway(page, "Pass");
        page.Find("button.tf-go");   // still the end of the game
        Assert.Equal("Time's up. You scored 1.", Text(page, ".tf-result"));
        Assert.Equal([(me, 1)], Rows(_board.Blitz.AllTime));
    }

    [Theory]
    [InlineData("button.oad-btn-green", new[] { 6, 6, 6, 6 })]   // a right answer
    [InlineData("button.tf-call", new[] { 1, 1, 1, 1 })]         // a right call
    [InlineData("button.tf-pass", new[] { 1, 1, 1, 1 })]
    public void A_move_that_arrives_at_five_minutes_doesnt_count_even_before_the_clock_has_said(string button, int[] hand)
    {
        // The ticker may not have had its turn when a move arrives: the move asks the clock itself.
        var page = Playing(6, 6, 6, 6);
        Solve(page, "6 + 6 + 6 + 6");
        Deal(page, hand);
        page.Find("input.tf-input").Input("6 + 6 + 6 + 6");
        _clock.Skip(TimeSpan.FromMinutes(5));

        page.Find(button).Click();

        Assert.Equal("Time's up. You scored 1.", Text(page, ".tf-result"));
        Assert.Empty(page.FindAll(".tf-card"));
    }

    [Fact]
    public async Task Go_again_mid_game_changes_nothing()
    {
        // A double tap, or a click sent again, would deal a fresh hand without spending a pass.
        var page = Playing(3, 3, 8, 8);
        await Tick(page, TimeSpan.FromSeconds(10));

        await CallAnyway(page, "Go");

        Assert.Equal([3, 3, 8, 8], HandOnTable(page));
        Assert.Equal("4:50", Clock(page));
        Assert.Single(_clock.Running);
    }

    [Fact]
    public async Task Play_again_starts_a_fresh_game()
    {
        var page = Playing(1, 1, 1, 1);
        page.Find("button.tf-call").Click();
        page.Find("button.tf-pass").Click();
        await Tick(page, TimeSpan.FromMinutes(5));

        page.Find("button.tf-go").Click();

        Assert.Equal(("5:00", "0", "3"), (Clock(page), Score(page), PassesLeft(page)));
        Assert.Equal(4, page.FindAll(".tf-card").Count);
        Assert.Empty(page.FindAll(".tf-message"));
        Assert.Single(_clock.Running);
    }

    // ---- scoring ---------------------------------------------------------------------

    [Fact]
    public void A_solve_and_a_right_call_each_score_one_and_the_next_hand_comes_at_once()
    {
        var page = Playing(6, 6, 6, 6);
        var first = HandOnTable(page);

        Solve(page, "6 + 6 + 6 + 6");

        Assert.Equal("Correct! 6 + 6 + 6 + 6 = 24. +1 point.", Message(page));
        Assert.Contains("tf-correct", page.Find(".tf-message").ClassList);
        Assert.Equal("1", Score(page));
        Assert.NotSame(first, HandOnTable(page));
        Assert.Equal("", page.Find("input.tf-input").GetAttribute("value") ?? "");
        Assert.False(page.Find("input.tf-input").HasAttribute("disabled"));
        Assert.Empty(page.FindAll("button.tf-next"));   // no "Deal a new hand" step

        Deal(page, 1, 1, 1, 1);
        page.Find("button.tf-call").Click();

        Assert.Equal("Right, these four can't make 24. +1 point.", Message(page));
        Assert.Equal("2", Score(page));
        Assert.Equal(4, page.FindAll(".tf-card").Count);
    }

    [Fact]
    public void A_wrong_call_costs_a_point_says_only_that_it_could_be_done_and_can_take_the_score_below_zero()
    {
        // 3 3 8 8 has one answer, through a fraction, and nothing of it may show.
        var page = Playing(3, 3, 8, 8);

        page.Find("button.tf-call").Click();

        Assert.Equal("It could be done. −1 point.", Message(page));
        Assert.Contains("tf-wrong", page.Find(".tf-message").ClassList);
        Assert.Equal("−1", Score(page));
        var text = page.Find(".tf-page").TextContent;
        Assert.DoesNotContain(TwentyFourGame.FindSolution([3, 3, 8, 8])!, text);
        Assert.DoesNotContain("/", text);
    }

    [Fact]
    public void A_wrong_answer_costs_nothing_and_the_hand_stays_to_try_again()
    {
        var page = Playing(6, 6, 6, 6);

        Solve(page, "6 + 6 + 6 - 6");

        Assert.Equal("That comes to 12, not 24.", Message(page));
        Assert.Equal("0", Score(page));
        Assert.Equal([6, 6, 6, 6], HandOnTable(page));
        Solve(page, "6 * 6 - 6 - 6");
        Assert.Equal("1", Score(page));
    }

    [Fact]
    public void A_tapped_answer_scores_like_a_typed_one()
    {
        var page = Playing(10, 4, 2, 2);
        foreach (var key in "( 10 − 4 ) × 2 × 2".Split(' '))
        {
            page.FindAll("button.tf-key")
                .First(b => b.TextContent.Trim() == key && !b.HasAttribute("disabled"))
                .Click();
        }

        page.Find("button.oad-btn-green").Click();

        Assert.Equal("1", Score(page));
    }

    // ---- passes ----------------------------------------------------------------------

    [Fact]
    public async Task Three_passes_a_game_each_dealing_a_fresh_hand_for_nothing_and_saying_nothing_about_it()
    {
        var page = Playing(3, 3, 8, 8);
        var said = new List<string>();

        for (var i = 0; i < 3; i++)
        {
            var before = HandOnTable(page);
            page.Find("button.tf-pass").Click();
            Assert.NotSame(before, HandOnTable(page));
            said.Add(Message(page));
        }

        Assert.Equal(["Passed. 2 passes left.", "Passed. 1 pass left.", "Passed. That was your last pass."], said);
        Assert.Equal(("0", "0"), (Score(page), PassesLeft(page)));
        Assert.True(page.Find("button.tf-pass").HasAttribute("disabled"));
        Assert.DoesNotContain(TwentyFourGame.FindSolution([3, 3, 8, 8])!, page.Find(".tf-page").TextContent);

        // A fourth, sent anyway, deals nothing.
        Deal(page, 1, 2, 3, 4);
        await CallAnyway(page, "Pass");
        Assert.Equal([1, 2, 3, 4], HandOnTable(page));
        Assert.Equal("Passed. That was your last pass.", Message(page));
    }

    // ---- confetti and metrics --------------------------------------------------------

    private int Celebrations => JSInterop.Invocations["oadCelebrate"].Count;

    [Fact]
    public async Task Confetti_fires_once_at_the_end_for_a_score_above_zero_and_never_over_a_hand()
    {
        // Per hand, it would cover the next one, which is already out.
        var page = Playing(6, 6, 6, 6);
        Solve(page, "6 + 6 + 6 + 6");
        Deal(page, 1, 1, 1, 1);
        page.Find("button.tf-call").Click();
        Assert.Equal(0, Celebrations);

        await Tick(page, TimeSpan.FromMinutes(5));

        Assert.Equal(1, Celebrations);
    }

    [Fact]
    public async Task A_game_that_ends_on_zero_gets_no_confetti()
    {
        var page = Playing(1, 1, 1, 1);
        page.Find("button.tf-call").Click();
        Deal(page, 3, 3, 8, 8);
        page.Find("button.tf-call").Click();

        await Tick(page, TimeSpan.FromMinutes(5));

        Assert.Equal("Time's up. You scored 0.", Text(page, ".tf-result"));
        Assert.Equal(0, Celebrations);
    }

    [Fact]
    public async Task Times_up_shows_while_the_confetti_is_still_on_its_way()
    {
        // From the ticker nothing redraws until the call returns, and the browser can be slow.
        JSInterop.SetupVoid("oadCelebrate");   // never answered
        var page = Playing(6, 6, 6, 6);
        Solve(page, "6 + 6 + 6 + 6");

        await Tick(page, TimeSpan.FromMinutes(5));

        Assert.Equal("Time's up. You scored 1.", Text(page, ".tf-result"));
        Assert.Equal(1, Celebrations);
    }

    [Fact]
    public void Blitz_hands_count_in_the_site_metrics_like_normal_ones()
    {
        var page = Playing(6, 6, 6, 6);
        Solve(page, "6 + 6 + 6 + 6");
        Deal(page, 1, 1, 1, 1);
        page.Find("button.tf-call").Click();
        Deal(page, 3, 3, 8, 8);
        page.Find("button.tf-call").Click();
        page.Find("button.tf-pass").Click();

        var day = _metrics.Read().Days[MetricsData.Key(AppTime.Today)].TwentyFour;
        Assert.Equal((1, 1, 1, 1, 1),
            (day.Players, day.HandsSolved, day.ImpossibleRight, day.ImpossibleWrong, day.HandsPassed));
    }

    // ---- a new player's first point --------------------------------------------------

    [Theory]
    [InlineData("button.oad-btn-green", new[] { 6, 6, 6, 6 }, "6 + 6 + 6 + 6")]
    [InlineData("button.tf-call", new[] { 1, 1, 1, 1 }, "")]
    public async Task A_new_players_first_point_deals_the_next_hand_before_their_browser_has_saved_them(
        string button, int[] hand, string answer)
    {
        // Saving a new player waits on the browser. With the hand still on the table, a double
        // click, or a client holding that answer back, scored the same hand twice and then dealt
        // over the hand it got (cold review, 2026-11-16).
        _player.HoldWrites = new TaskCompletionSource();
        var page = Playing(hand);
        page.Find("input.tf-input").Input(answer);
        var scored = HandOnTable(page);

        var first = page.Find(button).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        var dealt = HandOnTable(page);
        Assert.NotSame(scored, dealt);   // out already, while the browser is still saving
        await CallAnyway(page, "Submit");   // a second click, sent meanwhile
        await page.InvokeAsync(() => _player.HoldWrites.SetResult());
        await first;

        Assert.Equal("1", Score(page));
        Assert.Same(dealt, HandOnTable(page));   // nothing dealt over it
        Assert.NotNull(_player.Player);
    }

    [Fact]
    public async Task A_new_players_score_still_goes_on_if_time_runs_out_while_their_browser_saves_them()
    {
        _player.HoldWrites = new TaskCompletionSource();
        var page = Playing(6, 6, 6, 6);
        page.Find("input.tf-input").Input("6 + 6 + 6 + 6");
        var first = page.Find("button.oad-btn-green").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await Tick(page, TimeSpan.FromMinutes(5));
        Assert.Empty(_board.Blitz.AllTime);   // no player yet to put there
        await page.InvokeAsync(() => _player.HoldWrites.SetResult());
        await first;

        Assert.Equal([(_player.Player!, 1)], Rows(_board.Blitz.AllTime));
    }

    // ---- the board -------------------------------------------------------------------

    [Fact]
    public async Task A_games_score_goes_on_the_Blitz_board_when_it_ends_and_never_on_the_runs()
    {
        var me = new TwentyFourPlayer(Guid.NewGuid(), 7);
        _player.Player = me;
        var page = Playing(6, 6, 6, 6);
        Solve(page, "6 + 6 + 6 + 6");
        Deal(page, 1, 1, 1, 1);
        page.Find("button.tf-call").Click();
        Deal(page, 3, 3, 8, 8);
        page.Find("button.tf-call").Click();   // wrong: 2 falls to 1
        Deal(page, 6, 6, 6, 6);
        Solve(page, "6 + 6 + 6 + 6");
        Assert.Empty(_board.Blitz.AllTime);   // not yet: a game's score goes on when it ends

        await Tick(page, TimeSpan.FromMinutes(5));

        Assert.Equal([(me, 2)], Rows(_board.Blitz.AllTime));
        Assert.Equal([(me, 2)], Rows(_board.Blitz.Today));
        Assert.Empty(_board.AllTime);
        Assert.Empty(_board.Today);
    }

    [Fact]
    public void A_normal_run_never_goes_on_the_Blitz_board()
    {
        _player.Player = new TwentyFourPlayer(Guid.NewGuid(), 7);
        var page = Render<TwentyFour>();
        Deal(page, 1, 1, 1, 1);

        page.Find("button.tf-call").Click();

        Assert.Single(_board.AllTime);
        Assert.Empty(_board.Blitz.AllTime);
    }

    [Fact]
    public void Each_game_shows_its_own_board()
    {
        var runner = new TwentyFourPlayer(Guid.NewGuid(), 1);
        var blitzer = new TwentyFourPlayer(Guid.NewGuid(), 2);
        _board.Record(runner, 9);
        _board.Blitz.Record(blitzer, 4);

        string[] Shown(IRenderedComponent<TwentyFour> page) =>
            page.FindAll(".tf-board-list")[1].QuerySelectorAll("li")
                .Select(r => Regex.Replace(r.TextContent, @"\s+", " ").Trim()).ToArray();

        var blitz = Blitz();
        Assert.Equal("Blitz high scores", Text(blitz, "button.tf-board-toggle"));
        Assert.Equal([$"1 {blitzer.Name} 4"], Shown(blitz));

        var normal = Render<TwentyFour>();
        Assert.Equal("High scores", Text(normal, "button.tf-board-toggle"));
        Assert.Equal([$"1 {runner.Name} 9"], Shown(normal));
    }

    [Fact]
    public void An_empty_Blitz_board_says_how_to_get_on_it()
    {
        var lists = Blitz().FindAll(".tf-board-list");

        Assert.Equal("No scores yet today. Finish a game on 1 or more to get on the board.",
            lists[0].QuerySelector(".tf-board-empty")!.TextContent);
        Assert.Equal("No scores yet.", lists[1].QuerySelector(".tf-board-empty")!.TextContent);
    }

    [Fact]
    public async Task A_game_that_ends_on_zero_or_less_stays_off_the_board()
    {
        _player.Player = new TwentyFourPlayer(Guid.NewGuid(), 7);
        var page = Playing(3, 3, 8, 8);
        page.Find("button.tf-call").Click();

        await Tick(page, TimeSpan.FromMinutes(5));

        Assert.Equal("Time's up. You scored −1.", Text(page, ".tf-result"));
        Assert.Empty(_board.Blitz.AllTime);
    }

    [Fact]
    public async Task Leaving_mid_game_ends_it_and_its_score_stands()
    {
        var me = new TwentyFourPlayer(Guid.NewGuid(), 7);
        _player.Player = me;
        var page = Playing(6, 6, 6, 6);
        Solve(page, "6 + 6 + 6 + 6");

        await DisposeComponentsAsync();

        Assert.Equal([(me, 1)], Rows(_board.Blitz.AllTime));
        Assert.Empty(_clock.Running);
    }

    [Fact]
    public async Task A_new_player_gets_their_name_at_their_first_point_and_goes_on_under_it()
    {
        var page = Playing(3, 3, 8, 8);
        page.Find("button.tf-call").Click();   // wrong: no name for that
        Assert.Null(_player.Player);

        Deal(page, 1, 1, 1, 1);
        page.Find("button.tf-call").Click();
        var me = _player.Player!;
        Assert.Equal(1, me.Number);
        Deal(page, 6, 6, 6, 6);
        Solve(page, "6 + 6 + 6 + 6");
        await Tick(page, TimeSpan.FromMinutes(5));

        Assert.Equal([(me, 1)], Rows(_board.Blitz.AllTime));
        Assert.Equal(2, _board.NewPlayer()!.Number);   // one number used
    }

    [Fact]
    public void Blitz_leaves_the_space_bar_alone()
    {
        // The normal game's space bar deals the next hand; Blitz deals at once, so it takes nothing.
        var page = Playing(6, 6, 6, 6);
        Solve(page, "6 + 6 + 6 + 6");

        Assert.Empty(JSInterop.Invocations["oadTwentyFour.register"]);
        Render<TwentyFour>();
        Assert.Single(JSInterop.Invocations["oadTwentyFour.register"]);   // the normal game does
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
