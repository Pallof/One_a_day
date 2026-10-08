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
    public void It_starts_closed_behind_its_whiteboard_button_with_the_pen_picked()
    {
        var pad = Render<ScratchPad>();

        var launcher = pad.Find("button.sp-launcher");
        Assert.Equal("Scratch pad", launcher.GetAttribute("aria-label"));
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

        var home = Render<Home>();

        Assert.Single(home.FindAll(".sp button.sp-launcher"));
    }

    [Fact]
    public void No_other_page_has_it()
    {
        // Not the layout, which every page shares, and not Twenty Four, the other puzzle.
        Services.AddReportDialog(_env);
        Services.AddMetrics(_env);
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
