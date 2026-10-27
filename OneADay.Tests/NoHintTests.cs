using Bunit;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
using OneADay.Components;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// Today's challenge shows no hint, though the bank still holds one for most teasers (author's
/// decision, 2026-10-27; PRD 03). It used to offer one after the first wrong answer.
/// Yesterday's page is covered in YesterdaySolutionTests.
/// </summary>
public class NoHintTests : BunitContext
{
    private readonly TestEnvironment _env = new();
    private static readonly BrainTeaser Teaser =
        TeaserFactory.On("2026-08-12", answer: "a", hint: "zq-hint-text", solution: "zq-solution-text");

    public NoHintTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(_env.NewStatsStore());
        Services.AddScoped<CurrentTeaserContext>();
        Services.AddDataProtection();
        Services.AddScoped<ProtectedLocalStorage>();
        Services.AddMetrics(_env);
    }

    private IRenderedComponent<ChallengeView> RenderPuzzle(bool canReveal = false) =>
        Render<ChallengeView>(p => p
            .Add(c => c.Teaser, Teaser)
            .Add(c => c.CanReveal, canReveal));

    private static void Answer(IRenderedComponent<ChallengeView> cut, string text)
    {
        cut.Find("textarea.cv-input").Input(text);
        cut.Find("button.oad-btn-green").Click();
    }

    [Fact]
    public void No_hint_is_offered_before_or_after_wrong_answers()
    {
        var cut = RenderPuzzle();
        Assert.DoesNotContain("hint", cut.Markup, StringComparison.OrdinalIgnoreCase);

        Answer(cut, "wrong");
        Answer(cut, "still wrong");

        Assert.Contains("Not quite", cut.Find(".cv-feedback").TextContent);   // the answers did land
        Assert.DoesNotContain("hint", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Revealing_the_solution_shows_the_answer_and_solution_but_not_the_hint()
    {
        var cut = RenderPuzzle(canReveal: true);

        cut.Find("button.cv-reveal").Click();

        Assert.Contains("zq-solution-text", cut.Find(".cv-solution").TextContent);
        Assert.DoesNotContain("zq-hint-text", cut.Markup);
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
