using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
using OneADay.Components;
using OneADay.Components.Layout;
using OneADay.Components.Pages;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The scratch pad (PRD 01): on the challenge page only, behind a floating whiteboard button,
/// with a pen, an eraser, undo and clear. The drawing itself is wwwroot/js/scratchpad.js, checked by
/// hand — what's pinned here is where the pad is, what's on it, and that none of it reaches
/// the server.
/// </summary>
public class ScratchPadTests : BunitContext
{
    private readonly TestEnvironment _env = new();

    public ScratchPadTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void The_pad_has_a_pen_an_eraser_undo_and_clear_and_nothing_else()
    {
        var pad = Render<ScratchPad>();

        Assert.Equal(["Pen", "Eraser", "Undo", "Clear"],
            pad.FindAll(".sp-tools .sp-tool").Select(b => b.TextContent.Trim()));
        Assert.Single(pad.FindAll(".sp-panel canvas"));
    }

    [Fact]
    public void The_eraser_has_a_ring_over_the_drawing_area_to_show_its_reach()
    {
        // The script sizes and places it; it has to be beside the canvas to find it, and is
        // decoration to a screen reader.
        var pad = Render<ScratchPad>();

        var ring = pad.Find(".sp-sheet > .sp-ring");
        Assert.Equal("true", ring.GetAttribute("aria-hidden"));
        Assert.NotNull(ring.ParentElement!.QuerySelector(":scope > canvas.sp-canvas"));
    }

    [Fact]
    public void The_button_says_whiteboard_and_clicking_the_word_opens_the_pad_too()
    {
        // Inside the button, so it's part of what can be clicked. A screen reader hears the
        // button's own name, which is the same word, so voice control finds it by what it says.
        var pad = Render<ScratchPad>();

        var caption = pad.Find("button.sp-launcher > .sp-caption");
        Assert.Equal("Whiteboard", caption.TextContent.Trim());
        Assert.Equal("true", caption.GetAttribute("aria-hidden"));
        Assert.Equal(caption.TextContent.Trim(), pad.Find("button.sp-launcher").GetAttribute("aria-label"));
    }

    [Fact]
    public void The_word_shows_on_phones_as_well_as_computers()
    {
        // At first it was for computers only; phones got it the same day (author's decision,
        // 2026-10-18). No rule for it, at any screen size, may hide it.
        var css = File.ReadAllText(Path.Combine(RepoFiles.Root, "OneADay", "Components", "ScratchPad.razor.css"));

        Assert.Contains(".sp-caption {\n    display: block;", css);
        Assert.DoesNotMatch(@"\.sp-caption\s*\{[^}]*display:\s*none", css);
    }

    [Fact]
    public void It_starts_closed_behind_its_whiteboard_button_with_the_pen_picked()
    {
        var pad = Render<ScratchPad>();

        var launcher = pad.Find("button.sp-launcher");
        Assert.Equal("Whiteboard", launcher.GetAttribute("aria-label"));
        Assert.Equal("false", launcher.GetAttribute("aria-expanded"));
        Assert.NotNull(launcher.QuerySelector("svg.sp-icon"));
        Assert.False(pad.Find(".sp").HasAttribute("data-open"));
        Assert.Equal("true", pad.Find("[data-sp='pen']").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Nothing_on_the_pad_talks_to_the_server()
    {
        // The script does it all in the browser. A Blazor handler here would send every tap
        // over the live connection — and invite strokes to follow.
        var pad = Render<ScratchPad>();

        var handlers = pad.FindAll("*")
            .SelectMany(e => e.Attributes)
            .Where(a => a.Name.StartsWith("blazor:on", StringComparison.Ordinal));
        Assert.Empty(handlers);
    }

    [Fact]
    public void The_challenge_page_has_the_pad()
    {
        _env.SeedTeasers(TeaserFactory.On(AppTime.Today.ToString("yyyy-MM-dd"), answer: "a"));
        Services.AddSingleton(_env.NewTeaserStore());
        Services.AddSingleton(_env.NewRotationStore());
        Services.AddSingleton<DailySchedule>();
        Services.AddSingleton(_env.NewStatsStore());
        Services.AddScoped<CurrentTeaserContext>();
        Services.AddDataProtection();
        Services.AddScoped<ProtectedLocalStorage>();
        Services.AddMetrics(_env);
        Services.AddGrading(_env);

        var home = Render<Home>();

        Assert.Single(home.FindAll(".sp button.sp-launcher"));
    }

    [Fact]
    public void No_other_page_has_it()
    {
        // Not the layout, which every page shares, and not Twenty Four, the other puzzle.
        Services.AddReportDialog(_env);
        Services.AddMetrics(_env);
        Services.AddTwentyFour(_env);
        Services.AddGrading(_env);
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        Services.GetRequiredService<NavigationManager>().NavigateTo("yesterday");

        var layout = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(_ => { })));
        var twentyFour = Render<TwentyFour>();

        Assert.Empty(layout.FindAll(".sp"));
        Assert.Empty(twentyFour.FindAll(".sp"));
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
