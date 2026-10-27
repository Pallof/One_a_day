using System.Text.RegularExpressions;
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
/// The mascot (PRD 09): the whole duck on its stump wherever the page shows it, and its face,
/// close up, as the browser tab's icon (author's decision, 2026-10-14). Each picture must be a
/// file the site actually serves, or the visitor gets a broken-image box.
/// </summary>
public class MascotTests : BunitContext
{
    private readonly TestEnvironment _env = new();

    public MascotTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;   // confetti and storage are fire-and-forget here
        Services.AddReportDialog(_env);         // the layout carries it on every page
        Services.AddSingleton(_env.NewStatsStore());
        Services.AddDataProtection();
        Services.AddScoped<ProtectedLocalStorage>();
        Services.AddMetrics(_env);

        // Last: it reads the services, and bUnit locks them on the first read.
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    /// <summary>The layout on About, around an empty page — About has no sign-up to set up.</summary>
    private IRenderedComponent<MainLayout> Layout()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("about");
        return Render<MainLayout>(p => p.Add(l => l.Body, _ => { }));
    }

    /// <summary>
    /// The picture's address is the whole mascot, and the file is there to serve. Outside the
    /// running site, Assets hands the name back as written, so it can be looked up in wwwroot.
    /// </summary>
    private static void AssertMascot(string expected, string? src)
    {
        Assert.Equal(expected, src);
        Assert.True(File.Exists(Path.Combine(RepoFiles.Wwwroot, src!)),
            $"'{src}' isn't in wwwroot, so the page would show a broken image.");
    }

    [Fact]
    public void The_header_shows_the_whole_duck_beside_the_name()
    {
        var header = Layout().Find(".oad-header");

        var mark = header.QuerySelector(".oad-wordmark img.oad-mark");
        Assert.NotNull(mark);
        AssertMascot("img/stumpty-mark.png", mark.GetAttribute("src"));
        // Decorative: the name beside it already says what the link is, and a screen reader
        // reading "Stumpty, Stumpty" helps nobody.
        Assert.Equal("", mark.GetAttribute("alt"));
        Assert.Equal("Stumpty", header.QuerySelector(".oad-wordmark")!.TextContent.Trim());
    }

    [Fact]
    public void The_footer_tagline_carries_the_duck_where_the_brain_was()
    {
        var line = Layout().Find(".oad-footer-line");

        AssertMascot("img/stumpty-mark.png", line.QuerySelector("img.oad-footer-mark")?.GetAttribute("src"));
        Assert.Equal("Stumpty · one puzzle a day to sharpen how you think.",
            Regex.Replace(line.TextContent, @"\s+", " ").Trim());
    }

    [Fact]
    public void About_shows_the_duck_and_says_what_it_is()
    {
        var mascot = Render<About>().Find(".about-body img.about-mascot");

        AssertMascot("img/stumpty.png", mascot.GetAttribute("src"));
        // Here it's the subject, not decoration: someone who can't see it is told what it is.
        Assert.Contains("duck", mascot.GetAttribute("alt"));
    }

    [Fact]
    public void The_solve_dialog_shows_the_duck()
    {
        var page = Render<ChallengeView>(p => p
            .Add(c => c.Teaser, TeaserFactory.On("2026-08-12", answer: "a"))
            .Add(c => c.IsDaily, true));
        Assert.Empty(page.FindAll(".cv-solved"));

        page.Find("textarea.cv-input").Input("a");
        page.Find("button.oad-btn-green").Click();

        AssertMascot("img/stumpty.png", page.Find(".cv-solved img.cv-solved-mark").GetAttribute("src"));
    }

    [Theory]
    [InlineData("icon", "favicon.png", 32)]
    [InlineData("apple-touch-icon", "apple-touch-icon.png", 180)]
    public void The_tab_and_home_screen_icons_are_the_face_at_their_sizes(string rel, string file, int size)
    {
        // The page's <head> is App.razor, which only renders inside the running site. 32px is
        // a tab on a sharp screen; 180px is what an iPhone puts on its home screen.
        var app = RepoFiles.AppRazor();
        var link = Regex.Match(app, $"""<link rel="{rel}"[^>]*href="@Assets\["([^"]+)"\]""");

        Assert.True(link.Success, $"App.razor has no <link rel=\"{rel}\"> through Assets.");
        Assert.Equal(file, link.Groups[1].Value);
        Assert.Equal((size, size), RepoFiles.PngSize(Path.Combine(RepoFiles.Wwwroot, file)));
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
