using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using OneADay.Components.Pages;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The Twenty Four page (PRD 07): what its rules tell the player, and its keypad — the dealt
/// numbers and the symbols as buttons, so a phone player never swaps keyboards. The keypad only
/// types; every answer is still judged by the one check.
/// </summary>
public class TwentyFourPageTests : BunitContext
{
    private readonly TestEnvironment _env = new();

    public TwentyFourPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMetrics(_env);
        Services.AddScoped<CurrentTeaserContext>();
    }

    private IRenderedComponent<TwentyFour> Dealt(params int[] hand)
    {
        var page = Render<TwentyFour>();
        typeof(TwentyFour).GetField("_hand", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(page.Instance, hand);
        page.Render();
        return page;
    }

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

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
