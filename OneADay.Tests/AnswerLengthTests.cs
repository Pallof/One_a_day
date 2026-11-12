using Bunit;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
using OneADay.Components;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// How long an answer can be (PRD 01): 300 characters, or 600 on an explanation question, whose
/// accepted answers are all methods written out (PRD 02).
///
/// <para>Why the two (author's decision, 2026-11-12): a whole method didn't fit in 300. A
/// visitor's right method stopped mid-step at 298 characters and Jev scored it 0.46, under the
/// 0.7 bar. Every other question's answers are a word or a number, so they keep 300.</para>
///
/// <para>The box's maxlength binds honest browsers only. bUnit's Input skips it, just as a
/// client driving the live connection would, so what reaches Jev shows the server's own cut.</para>
/// </summary>
public class AnswerLengthTests : BunitContext
{
    private readonly TestEnvironment _env = new();

    /// <summary>A made-up method, 15 words and no numbers, so every typed answer may go to Jev.</summary>
    private const string Method = "pour the small jug into the big one and fill it again to the top";

    public AnswerLengthTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;   // confetti and storage are fire-and-forget here
        Services.AddSingleton(_env.NewStatsStore());
        Services.AddScoped<CurrentTeaserContext>();
        Services.AddDataProtection();
        Services.AddScoped<ProtectedLocalStorage>();
        Services.AddMetrics(_env);
        Services.AddGrading(_env);
    }

    /// <summary>The kinds of question, by their accepted answers.</summary>
    public enum Kind { Word, Method, MethodOrNumber }

    private static BrainTeaser Question(Kind kind) => TeaserFactory.On("2026-08-12", answer: kind switch
    {
        Kind.Word => "a",
        Kind.Method => Method,
        // One short answer beside the method and it's an ordinary question again.
        Kind.MethodOrNumber => $"{Method}; 48",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    });

    private IRenderedComponent<ChallengeView> RenderPuzzle(BrainTeaser teaser) =>
        Render<ChallengeView>(p => p.Add(c => c.Teaser, teaser));

    private static void Answer(IRenderedComponent<ChallengeView> cut, string text)
    {
        cut.Find("textarea.cv-input").Input(text);
        cut.Find("button.oad-btn-green").Click();
    }

    [Theory]
    [InlineData(Kind.Word, 300)]
    [InlineData(Kind.Method, 600)]
    [InlineData(Kind.MethodOrNumber, 300)]
    public void The_box_takes_300_characters_or_600_on_an_explanation_question(Kind kind, int limit)
    {
        var cut = RenderPuzzle(Question(kind));

        Assert.Equal(limit.ToString(), cut.Find("textarea.cv-input").GetAttribute("maxlength"));
    }

    [Theory]
    // Up to the limit, the whole answer reaches Jev; past it, the server cuts it to the limit.
    [InlineData(Kind.Word, 300, 300)]
    [InlineData(Kind.Word, 301, 300)]
    [InlineData(Kind.Word, 32_000, 300)]          // about the most one live-connection message carries
    [InlineData(Kind.Method, 600, 600)]
    [InlineData(Kind.Method, 601, 600)]
    [InlineData(Kind.Method, 32_000, 600)]
    [InlineData(Kind.MethodOrNumber, 301, 300)]
    public void The_server_cuts_an_answer_to_the_same_length_as_the_box(Kind kind, int typed, int reachesJev)
    {
        var jev = Services.AddGrading(_env, GradingTestServices.On());   // the last registration wins
        jev.Chance = _ => 0.05;
        var cut = RenderPuzzle(Question(kind));

        Answer(cut, new string('x', typed));

        Assert.Equal(reachesJev, Assert.Single(jev.Asked).Length);
    }

    [Fact]
    public void A_right_method_longer_than_300_characters_solves_an_explanation_question()
    {
        // The case that started it: cut at 300, the method stops mid-step and Jev turns it down.
        var whole = string.Join(", ", Enumerable.Repeat("then pour the small jug into the big one until it is full", 8));
        Assert.InRange(whole.Length, 301, 600);
        var jev = Services.AddGrading(_env, GradingTestServices.On());
        jev.Chance = typed => typed == whole ? 0.9 : 0.46;
        var cut = RenderPuzzle(Question(Kind.Method));

        Answer(cut, whole);

        Assert.Contains("Correct!", cut.Find(".cv-feedback").TextContent);
        Assert.Equal([whole], jev.Asked);
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
