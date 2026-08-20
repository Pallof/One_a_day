using Bunit;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
using OneADay.Components;
using OneADay.Components.Pages;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// Covers the "See yesterday's solution" button and the page it leads to:
/// which teaser is published, that the page can't be used to replay a puzzle,
/// and that both spoiler veils stay shut until deliberately opened.
/// </summary>
public class YesterdaySolutionTests : BunitContext
{
    private readonly TestEnvironment _env = new();

    private TeaserStore StoreWith(params BrainTeaser[] teasers)
    {
        _env.SeedTeasers(teasers);
        return _env.NewTeaserStore();
    }

    /// <summary>Registers everything ChallengeView and Yesterday need to render.</summary>
    private void RegisterAppServices(TeaserStore store)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;   // confetti + storage interop are fire-and-forget here
        Services.AddSingleton(store);
        Services.AddSingleton(_env.NewStatsStore());
        Services.AddScoped<CurrentTeaserContext>();
        Services.AddDataProtection();
        Services.AddScoped<ProtectedLocalStorage>();
    }

    // ---- which teaser counts as "yesterday's" ---------------------------------

    [Fact]
    public void Previous_is_the_teaser_before_the_given_date()
    {
        var store = StoreWith(
            TeaserFactory.On("2026-08-10", "ten"),
            TeaserFactory.On("2026-08-11", "eleven"),
            TeaserFactory.On("2026-08-12", "twelve"));

        Assert.Equal("eleven", store.GetPreviousBefore(DateOnly.Parse("2026-08-12"))?.Question);
    }

    [Fact]
    public void Previous_skips_gaps_in_the_schedule()
    {
        var store = StoreWith(
            TeaserFactory.On("2026-08-01", "first"),
            TeaserFactory.On("2026-08-09", "ninth"));

        // Nothing on the 8th; the most recent earlier teaser is the 1st.
        Assert.Equal("first", store.GetPreviousBefore(DateOnly.Parse("2026-08-09"))?.Question);
    }

    [Fact]
    public void Previous_is_null_when_nothing_came_before()
    {
        var store = StoreWith(TeaserFactory.On("2026-08-01", "only"));
        Assert.Null(store.GetPreviousBefore(DateOnly.Parse("2026-08-01")));
    }

    [Fact]
    public void Previous_never_returns_the_live_challenge_when_the_queue_is_dry()
    {
        // Today is the 20th but the queue ran out on the 12th, so the daily page
        // falls back to the 12th. "Yesterday's" must be the 11th — publishing the
        // 12th would spoil the puzzle people are being asked to solve.
        var store = StoreWith(
            TeaserFactory.On("2026-08-11", "eleventh"),
            TeaserFactory.On("2026-08-12", "twelfth"));

        var live = store.GetCurrent(DateOnly.Parse("2026-08-20"));
        Assert.Equal("twelfth", live!.Question);

        var yesterday = store.GetPreviousBefore(live.Date);
        Assert.Equal("eleventh", yesterday?.Question);
        Assert.NotEqual(live.Question, yesterday?.Question);
    }

    // ---- the button on the daily challenge ------------------------------------

    [Fact]
    public void Daily_challenge_shows_the_yesterday_button_linking_to_the_page()
    {
        var store = StoreWith(TeaserFactory.On("2026-08-12"));
        RegisterAppServices(store);

        var cut = Render<ChallengeView>(p => p
            .Add(c => c.Teaser, TeaserFactory.On("2026-08-12"))
            .Add(c => c.IsDaily, true));

        var link = cut.Find("a.cv-yesterday");
        Assert.Equal("yesterday", link.GetAttribute("href"));
        Assert.Contains("See yesterday's solution", link.TextContent);
    }

    [Fact]
    public void Yesterday_button_sits_outside_the_submission_box()
    {
        var store = StoreWith(TeaserFactory.On("2026-08-12"));
        RegisterAppServices(store);

        var cut = Render<ChallengeView>(p => p
            .Add(c => c.Teaser, TeaserFactory.On("2026-08-12"))
            .Add(c => c.IsDaily, true));

        var submissionBox = cut.Find("textarea.cv-input").Closest(".oad-box");
        var button = cut.Find("a.cv-yesterday");
        Assert.False(submissionBox!.Contains(button), "the button must be in its own container");
    }

    [Fact]
    public void Non_daily_challenge_has_no_yesterday_button()
    {
        var store = StoreWith(TeaserFactory.On("2026-08-12"));
        RegisterAppServices(store);

        var cut = Render<ChallengeView>(p => p
            .Add(c => c.Teaser, TeaserFactory.On("2026-08-12"))
            .Add(c => c.IsDaily, false));

        Assert.Empty(cut.FindAll("a.cv-yesterday"));
    }

    // ---- the page itself -------------------------------------------------------

    [Fact]
    public void Page_publishes_the_previous_teaser_not_the_live_one()
    {
        var store = StoreWith(
            TeaserFactory.On("2026-08-11", "the older question", solution: "older solution"),
            TeaserFactory.On("2026-08-12", "the live question", solution: "live solution"));
        RegisterAppServices(store);

        var cut = Render<Yesterday>();

        foreach (var b in cut.FindAll("button.ys-unveil").ToList()) { b.Click(); }   // open both veils
        Assert.Contains("the older question", cut.Markup);
        Assert.DoesNotContain("the live question", cut.Markup);
    }

    [Fact]
    public void Page_has_no_submission_box()
    {
        var store = StoreWith(
            TeaserFactory.On("2026-08-11", "older"),
            TeaserFactory.On("2026-08-12", "live"));
        RegisterAppServices(store);

        var cut = Render<Yesterday>();

        Assert.Empty(cut.FindAll("textarea"));
        Assert.Empty(cut.FindAll("button.oad-btn-green"));
    }

    [Fact]
    public void Page_warns_about_spoilers()
    {
        var store = StoreWith(
            TeaserFactory.On("2026-08-11", "older"),
            TeaserFactory.On("2026-08-12", "live"));
        RegisterAppServices(store);

        var cut = Render<Yesterday>();

        Assert.Contains("Spoilers ahead", cut.Find(".ys-spoiler-warning").TextContent);
    }

    [Fact]
    public void Both_veils_start_closed()
    {
        var store = StoreWith(
            TeaserFactory.On("2026-08-11", "older", solution: "because"),
            TeaserFactory.On("2026-08-12", "live"));
        RegisterAppServices(store);

        var cut = Render<Yesterday>();

        var veils = cut.FindAll(".ys-veil");
        Assert.Equal(2, veils.Count);
        Assert.All(veils, v => Assert.DoesNotContain("ys-open", v.ClassName));
        Assert.Equal(2, cut.FindAll("button.ys-unveil").Count);
    }

    [Fact]
    public void Opening_the_question_veil_leaves_the_solution_hidden()
    {
        var store = StoreWith(
            TeaserFactory.On("2026-08-11", "older", solution: "because"),
            TeaserFactory.On("2026-08-12", "live"));
        RegisterAppServices(store);

        var cut = Render<Yesterday>();
        cut.FindAll("button.ys-unveil")[0].Click();   // question only

        var veils = cut.FindAll(".ys-veil");
        Assert.Contains("ys-open", veils[0].ClassName);
        Assert.DoesNotContain("ys-open", veils[1].ClassName);
        // the solution's own reveal button must still be there
        Assert.Single(cut.FindAll("button.ys-unveil"));
    }

    [Fact]
    public void Opening_both_veils_reveals_the_answer_and_explanation()
    {
        var store = StoreWith(
            TeaserFactory.On("2026-08-11", "older", answer: "42; forty two", solution: "because maths"),
            TeaserFactory.On("2026-08-12", "live"));
        RegisterAppServices(store);

        var cut = Render<Yesterday>();
        foreach (var b in cut.FindAll("button.ys-unveil").ToList()) { b.Click(); }

        Assert.All(cut.FindAll(".ys-veil"), v => Assert.Contains("ys-open", v.ClassName));
        Assert.Contains("42", cut.Find(".ys-answer").TextContent);
        Assert.Contains("because maths", cut.Find(".ys-explanation").TextContent);
        Assert.Empty(cut.FindAll("button.ys-unveil"));
    }

    [Fact]
    public void Page_explains_itself_when_there_is_no_earlier_teaser()
    {
        var store = StoreWith(TeaserFactory.On("2026-08-12", "the only one"));
        RegisterAppServices(store);

        var cut = Render<Yesterday>();

        Assert.Empty(cut.FindAll(".ys-veil"));
        Assert.Contains("no earlier challenge", cut.Markup);
        Assert.DoesNotContain("the only one", cut.Markup);   // must not leak the live puzzle
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
