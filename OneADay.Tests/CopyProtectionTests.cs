using Bunit;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
using OneADay.Components;
using OneADay.Components.Pages;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// Today's question, and everything on yesterday's page, can't be copied (PRD 01), so someone
/// who wants to look a puzzle up has to type it out. Pasting an answer in still works. The
/// browser side, wwwroot/js/nocopy.js, is checked by hand; what's pinned here is which text
/// carries the guard and that the page loads it.
/// </summary>
public class CopyProtectionTests : BunitContext
{
    private const string Guard = "oad-no-copy";

    private readonly TestEnvironment _env = new();

    public CopyProtectionTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDataProtection();
        Services.AddScoped<ProtectedLocalStorage>();
        Services.AddSingleton(_env.NewStatsStore());
        Services.AddMetrics(_env);
        Services.AddGrading(_env);
        Services.AddReportDialog(_env);
    }

    private IRenderedComponent<ChallengeView> TodaysChallenge() =>
        Render<ChallengeView>(p => p
            .Add(c => c.Teaser, TeaserFactory.On("2026-08-12", "What has keys but opens no locks?"))
            .Add(c => c.IsDaily, true));

    [Fact]
    public void Todays_question_cannot_be_copied()
    {
        var question = TodaysChallenge().Find(".cv-question-text");

        Assert.Contains(Guard, question.ClassList);
        Assert.Equal("What has keys but opens no locks?", question.TextContent);
    }

    [Fact]
    public void The_answer_box_is_left_alone_so_an_answer_can_be_pasted_in()
    {
        var box = TodaysChallenge().Find("textarea.cv-input");

        Assert.Null(box.Closest($".{Guard}"));
    }

    [Fact]
    public void The_report_box_cannot_copy_the_question_either()
    {
        // Opened on the challenge, it quotes the question's first 90 characters: plenty to
        // search with.
        TodaysChallenge();
        var dialog = Render<ReportIssue>();
        dialog.InvokeAsync(dialog.Instance.Open);

        var quoted = dialog.Find(".ri-context em");
        Assert.Contains(Guard, quoted.ClassList);
        Assert.Contains("What has keys", quoted.TextContent);
    }

    [Fact]
    public void Nothing_on_yesterdays_page_can_be_copied_revealed_or_not()
    {
        // A bank big enough for the rotation to tell yesterday from today (see
        // YesterdaySolutionTests).
        _env.SeedTeasers(Enumerable.Range(1, 6)
            .Select(i => TeaserFactory.On(new DateOnly(2026, 8, 1).AddDays(i).ToString("yyyy-MM-dd"),
                $"question {i}", answer: $"{i}", hint: $"hint {i}", solution: $"solution {i}"))
            .ToArray());
        Services.AddSingleton(new DailySchedule(_env.NewTeaserStore(), _env.NewRotationStore()));

        var page = Render<Yesterday>();
        foreach (var veil in page.FindAll("button.ys-unveil").ToList()) { veil.Click(); }

        foreach (var part in new[] { ".oad-banner", ".ys-spoiler-warning", ".ys-question-text",
                                     ".ys-answer", ".ys-explanation", ".ys-back" })
        {
            Assert.True(page.Find(part).Closest($".{Guard}") is not null, $"{part} can be copied.");
        }
    }

    [Fact]
    public void Revealing_yesterdays_solution_doesnt_make_it_selectable_again()
    {
        // The revealed veil once set user-select back to auto, and Chrome let the solution be
        // selected inside the guarded page (found by hand, 2026-10-18).
        var css = File.ReadAllText(Path.Combine(RepoFiles.Root, "OneADay", "Components", "Pages", "Yesterday.razor.css"));

        Assert.DoesNotMatch(@"user-select:\s*(auto|text|all)", css);
    }

    [Fact]
    public void The_guarded_text_cannot_be_selected()
    {
        var css = File.ReadAllText(Path.Combine(RepoFiles.Wwwroot, "app.css"));
        var start = css.IndexOf($".{Guard} {{", StringComparison.Ordinal);
        Assert.True(start >= 0, $"app.css has no .{Guard} rule.");
        var rule = css[start..css.IndexOf('}', start)];

        // Both spellings: Safari still needs the prefix. The callout stops an iPhone's long press
        // offering Copy.
        Assert.Contains("-webkit-user-select: none;", rule);
        Assert.Contains("\n    user-select: none;", rule);
        Assert.Contains("-webkit-touch-callout: none;", rule);
    }

    [Fact]
    public void Every_page_loads_the_copy_guard_and_it_never_touches_pasting()
    {
        Assert.Contains("<script src=\"@Assets[\"js/nocopy.js\"]\"></script>", RepoFiles.AppRazor());

        var script = File.ReadAllText(Path.Combine(RepoFiles.Wwwroot, "js", "nocopy.js"));
        Assert.Contains($"querySelectorAll('.{Guard}')", script);
        Assert.Contains("addEventListener('copy', refuse)", script);
        Assert.Contains("addEventListener('cut', refuse)", script);
        Assert.DoesNotContain("'paste'", script);
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
