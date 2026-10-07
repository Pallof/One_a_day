using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
using OneADay.Components;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// Answers sent after a puzzle is solved or its solution revealed count for nothing.
///
/// <para>The box and the button are disabled then, but only in the browser. Before the fix
/// (security review, 2026-09-28) a client driving the live connection could keep calling
/// the submit handler and push "n out of n were correct" to any number it liked. Re-solving
/// is still intended: coming back to the page starts fresh.</para>
/// </summary>
public class SolvedAnswerTests : BunitContext
{
    private readonly TestEnvironment _env = new();
    private readonly StatsStore _stats;
    private static readonly BrainTeaser Teaser = TeaserFactory.On("2026-08-12", answer: "a");

    public SolvedAnswerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;   // confetti and storage are fire-and-forget here
        _stats = _env.NewStatsStore();
        Services.AddSingleton(_stats);
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

    /// <summary>Calls the submit handler directly, as a client ignoring the disabled button would.</summary>
    private static Task SubmitAnyway(IRenderedComponent<ChallengeView> cut)
    {
        var handler = typeof(ChallengeView).GetMethod("CheckAnswer", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "'CheckAnswer' not found on ChallengeView — was it renamed? These tests call it " +
                "directly, the way a crafted message would.");
        return cut.InvokeAsync(() => (Task)handler.Invoke(cut.Instance, null)!);
    }

    [Fact]
    public void Every_answer_counts_until_the_puzzle_is_solved()
    {
        // The control. Without it, the tests below would pass on a page that counted nothing.
        var cut = RenderPuzzle();

        Answer(cut, "wrong");
        Answer(cut, "a");

        Assert.Equal(new StatsSnapshot(TotalSubmissions: 2, SuccessfulSubmissions: 1), _stats.Get(Teaser.Id));
    }

    [Fact]
    public async Task Once_solved_further_answers_count_for_nothing()
    {
        var cut = RenderPuzzle();
        Answer(cut, "a");

        await SubmitAnyway(cut);   // the right answer is still in the box
        await SubmitAnyway(cut);

        Assert.Equal(new StatsSnapshot(1, 1), _stats.Get(Teaser.Id));
    }

    [Fact]
    public async Task Once_the_solution_is_revealed_answers_count_for_nothing()
    {
        // Revealed, the answer is on screen: copying it in would be a free "correct".
        var cut = RenderPuzzle(canReveal: true);
        cut.Find("textarea.cv-input").Input("a");
        cut.Find("button.cv-reveal").Click();

        await SubmitAnyway(cut);

        Assert.Equal(new StatsSnapshot(0, 0), _stats.Get(Teaser.Id));
    }

    [Fact]
    public void Coming_back_to_the_page_can_solve_it_again()
    {
        // Re-solving is intended (PRD 06): a fresh page starts unsolved and counts again.
        Answer(RenderPuzzle(), "a");
        Answer(RenderPuzzle(), "a");

        Assert.Equal(new StatsSnapshot(2, 2), _stats.Get(Teaser.Id));
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
