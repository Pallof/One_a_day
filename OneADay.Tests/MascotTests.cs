using System.Security.Cryptography;
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
/// close up, as the site's icon in a browser tab and beside the site in Google's results
/// (author's decision, 2026-10-14). Each picture must be a file the site actually serves, or
/// the visitor gets a broken-image box.
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
        Services.AddGrading(_env);

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

    /// <summary>
    /// The page's icon links, from App.razor: the page's head, which only renders inside the
    /// running site. Each address goes through Assets, or the href is null.
    /// </summary>
    private static List<(string Rel, string? Sizes, string? File)> IconLinks() =>
        Regex.Matches(RepoFiles.AppRazor(), """<link rel="(icon|apple-touch-icon)"[^>]*>""")
            .Select(link => (
                link.Groups[1].Value,
                Captured(link.Value, """sizes="([^"]+)"""),
                Captured(link.Value, """href="@Assets\["([^"]+)""")))
            .ToList();

    private static string? Captured(string tag, string pattern) =>
        Regex.Match(tag, pattern) is { Success: true } match ? match.Groups[1].Value : null;

    [Theory]
    [InlineData("icon", "favicon-48.png", 48)]
    [InlineData("icon", "favicon-96.png", 96)]
    [InlineData("icon", "favicon-192.png", 192)]
    [InlineData("apple-touch-icon", "apple-touch-icon.png", 180)]
    public void Each_icon_is_linked_at_the_size_the_file_really_is(string rel, string file, int size)
    {
        // A browser picks by the size the link declares, so a wrong one gets a blurry icon.
        // 180px is what an iPhone puts on its home screen.
        var link = Assert.Single(IconLinks(), l => l.Rel == rel && l.File == file);

        Assert.Equal($"{size}x{size}", link.Sizes);
        Assert.True(File.Exists(Path.Combine(RepoFiles.Wwwroot, file)),
            $"'{file}' isn't in wwwroot, so the page would link an icon nobody can fetch.");
        Assert.Equal((size, size), RepoFiles.PngSize(Path.Combine(RepoFiles.Wwwroot, file)));
    }

    [Fact]
    public void Google_is_offered_no_icon_smaller_than_48px()
    {
        // Google shows one icon beside the site in its results, picked from these links, and
        // asks for a square of 48px or more (its older rule: a multiple of 48). A smaller one
        // here is one it might pick and then not show. Browsers shrink the 48 for a tab.
        var icons = IconLinks().Where(l => l.Rel == "icon").ToList();

        Assert.NotEmpty(icons);
        foreach (var icon in icons)
        {
            Assert.NotNull(icon.File);
            var (width, height) = RepoFiles.PngSize(Path.Combine(RepoFiles.Wwwroot, icon.File));
            Assert.True(width == height && width >= 48 && width % 48 == 0,
                $"'{icon.File}' is {width} x {height}; Google wants a square, a multiple of 48px.");
        }
    }

    [Fact]
    public void Favicon_ico_holds_16_32_and_48px()
    {
        // Not linked from the page: it's for whatever asks for /favicon.ico without reading the
        // page first. Without the file that address is the not-found page, which isn't an icon.
        var ico = Path.Combine(RepoFiles.Wwwroot, "favicon.ico");

        Assert.True(File.Exists(ico), "There's no favicon.ico in wwwroot, so /favicon.ico is a 404.");
        Assert.Equal(new[] { 16, 32, 48 }, RepoFiles.IcoSizes(ico));
    }

    [Fact]
    public void Blazors_own_icon_is_nowhere_in_wwwroot()
    {
        // The template's purple swirl, which the tab showed before the duck and which Google
        // still had on 2026-10-28. Everything in wwwroot is served, under any name.
        const string BlazorIcon = "e265ac0f2dda1e5dfa65b1adf330722bb3ef7789115283604d8cd19f098f1f08";

        var copies = Directory.EnumerateFiles(RepoFiles.Wwwroot, "*", SearchOption.AllDirectories)
            .Where(f => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f))) == BlazorIcon)
            .Select(f => Path.GetRelativePath(RepoFiles.Wwwroot, f));

        Assert.Empty(copies);
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
