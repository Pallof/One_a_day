using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
using OneADay.Components;
using OneADay.Components.Pages;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>The pages that count (PRD 16), and admin's Site metrics section.</summary>
public class MetricsPageTests : BunitContext
{
    private readonly TestEnvironment _env = new();
    private readonly MetricsStore _metrics;
    private static readonly BrainTeaser Teaser = TeaserFactory.On("2026-08-12", answer: "a");

    public MetricsPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(_env.NewStatsStore());
        Services.AddScoped<CurrentTeaserContext>();
        Services.AddDataProtection();
        Services.AddScoped<ProtectedLocalStorage>();
        _metrics = Services.AddMetrics(_env);
    }

    private DayMetrics? Today => _metrics.Read().Days.GetValueOrDefault(MetricsData.Key(AppTime.Today));

    private IRenderedComponent<ChallengeView> Challenge(bool daily) =>
        Render<ChallengeView>(p => p.Add(c => c.Teaser, Teaser).Add(c => c.IsDaily, daily));

    private static void Answer(IRenderedComponent<ChallengeView> cut, string text)
    {
        cut.Find("textarea.cv-input").Input(text);
        cut.Find("button.oad-btn-green").Click();
    }

    [Fact]
    public void The_daily_challenge_counts_a_visitor_who_saw_tried_and_solved()
    {
        var cut = Challenge(daily: true);
        Answer(cut, "wrong");
        Answer(cut, "a");

        var day = Today!;
        Assert.Equal(1, day.Visitors.New);
        var puzzle = day.Puzzles[Teaser.Id];
        Assert.Equal((1, 1, 1), (puzzle.Saw, puzzle.Tried, puzzle.Solved));
        Assert.Equal(new[] { 0, 1, 0, 0, 0 }, puzzle.SolvedIn);
    }

    [Fact]
    public void Only_the_daily_challenge_counts()
    {
        var cut = Challenge(daily: false);
        Answer(cut, "a");

        Assert.Null(Today);
    }

    // ---- Twenty Four ---------------------------------------------------------------

    private IRenderedComponent<TwentyFour> DealtSixes()
    {
        var page = Render<TwentyFour>();
        typeof(TwentyFour).GetField("_hand", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(page.Instance, new[] { 6, 6, 6, 6 });
        page.Render();
        return page;
    }

    private static Task SubmitAnyway(IRenderedComponent<TwentyFour> page, string method = "Submit")
    {
        var handler = typeof(TwentyFour).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"'{method}' not found on TwentyFour — was it renamed?");
        return page.InvokeAsync(() => (Task)handler.Invoke(page.Instance, null)!);
    }

    [Fact]
    public void Opening_twenty_four_counts_nothing_until_an_answer_is_submitted()
    {
        var page = DealtSixes();
        Assert.Null(Today);

        page.Find("input.tf-input").Input("6 + 6 + 6 + 6");
        page.Find("button.oad-btn-green").Click();

        Assert.Equal(1, Today!.Visitors.New);
        Assert.Equal((1, 1), (Today!.TwentyFour.Players, Today!.TwentyFour.HandsSolved));
    }

    [Fact]
    public async Task A_solved_hand_cant_be_solved_or_passed_again()
    {
        // The buttons are disabled once solved, but only in the browser; these calls are what a
        // client driving the live connection could still send.
        var page = DealtSixes();
        page.Find("input.tf-input").Input("6 + 6 + 6 + 6");
        page.Find("button.oad-btn-green").Click();

        await SubmitAnyway(page);
        await SubmitAnyway(page);
        await SubmitAnyway(page, "Pass");

        Assert.Equal((1, 0), (Today!.TwentyFour.HandsSolved, Today!.TwentyFour.HandsPassed));
        Assert.Contains("Solved 1 of 1 hand this visit", page.Markup);
    }

    // ---- admin's section -----------------------------------------------------------

    [Fact]
    public void The_admin_section_shows_every_percentage_with_its_count()
    {
        _env.SeedTeasers(Teaser);
        var rotation = _env.NewRotationStore();
        rotation.RecordScheduled(AppTime.Today.AddDays(-1), Teaser.Id);
        Services.AddSingleton(_env.NewTeaserStore());
        Services.AddSingleton(rotation);

        var yesterday = AppTime.Today.AddDays(-1);
        for (var i = 0; i < 12; i++)
        {
            _metrics.RecordVisit(yesterday, VisitKind.New);
        }
        for (var i = 0; i < 7; i++)
        {
            _metrics.RecordVisit(AppTime.Today, VisitKind.BackFromYesterday);
        }

        var panel = Render<MetricsPanel>();

        Assert.Contains("58%", panel.Markup);              // 7 of 12 came back the next day
        Assert.Contains("· 7 of 12", panel.Markup);
        // On the figure itself — the explanatory note above the tables says it too.
        Assert.Contains(panel.FindAll(".mx-share .mx-few"), few => few.TextContent == "too few to judge");
        Assert.Contains(Teaser.Question, panel.Markup);    // listed under rotation fairness
    }

    [Fact]
    public void With_no_counts_the_admin_section_says_so()
    {
        _env.SeedTeasers(Teaser);
        Services.AddSingleton(_env.NewTeaserStore());
        Services.AddSingleton(_env.NewRotationStore());

        var panel = Render<MetricsPanel>();

        Assert.Contains("No counts saved yet", panel.Markup);
        Assert.Contains("Nothing counted in this range yet", panel.Markup);
        Assert.Contains("deploy/pull-live-data.sh", panel.Markup);
    }
}
