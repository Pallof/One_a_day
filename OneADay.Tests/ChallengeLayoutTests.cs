using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneADay.Components;
using OneADay.Components.Layout;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// Today's challenge on a laptop (PRD 09, "Fitting on one screen"): two columns, the question and
/// its picture beside the answer box, and a long question's picture giving way so the page still
/// fits the window. The widths and heights themselves are CSS, checked by measurement; these pin
/// what the CSS relies on.
/// </summary>
public class ChallengeLayoutTests : BunitContext
{
    private readonly TestEnvironment _env = new();

    public ChallengeLayoutTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddReportDialog(_env);         // the layout carries it on every page
        Services.AddSingleton(_env.NewStatsStore());
        Services.AddDataProtection();
        Services.AddScoped<ProtectedLocalStorage>();
        Services.AddMetrics(_env);
        Services.AddScoped<CurrentTeaserContext>();
        // The challenge page's footer offers the email sign-up, which needs these.
        Services.AddSingleton(_env.NewSubscriberStore());
        Services.AddSingleton(new ConfirmationQueue(Options.Create(new EmailOptions())));
        Services.AddSingleton(new SignUpLimit(Options.Create(new SubscriptionOptions())));
        Services.AddSingleton<IHttpContextAccessor>(FixedHttpContextAccessor.From("203.0.113.7"));
        Services.AddSingleton(Options.Create(new SiteOptions { BaseUrl = "https://site" }));
        // Last: it reads the services, and bUnit locks them on the first read.
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private IRenderedComponent<ChallengeView> Challenge(string question) =>
        Render<ChallengeView>(p => p
            .Add(c => c.Teaser, TeaserFactory.On("2026-08-12", question, hint: "Think it through."))
            .Add(c => c.IsDaily, true));

    /// <summary>
    /// The picture's height is the window less the question's lines, and the CSS reads those from
    /// --q-lines. 50 characters a line never undercounts a question in the bank, so the picture
    /// is never left too tall for the window.
    /// </summary>
    [Theory]
    [InlineData(35, 1)]
    [InlineData(50, 1)]
    [InlineData(51, 2)]
    [InlineData(478, 10)]   // the bank's longest, which runs to 9 lines
    public void A_question_tells_its_picture_how_many_lines_it_takes(int length, int lines)
    {
        var challenge = Challenge(new string('a', length));

        Assert.Contains($"--q-lines: {lines}", challenge.Find(".cv-question").GetAttribute("style"));
    }

    /// <summary>
    /// The grid puts the question on the left and everything else on the right, so everything
    /// else has to be one box: the hint, the answer box and the way to yesterday's solution.
    /// </summary>
    [Fact]
    public void Everything_after_the_question_is_one_column()
    {
        var challenge = Challenge("How many legs does a duck have?");

        var columns = challenge.Find(".cv").Children;
        Assert.Equal(["cv-question", "cv-side"], columns.Select(c => c.ClassList.Last()));
        var side = columns[1];
        Assert.NotNull(side.QuerySelector(".cv-hint-section"));
        Assert.NotNull(side.QuerySelector("textarea.cv-input"));
        Assert.NotNull(side.QuerySelector("a.cv-yesterday"));
    }

    /// <summary>Only today's challenge widens for its two columns; every other page keeps the reading column.</summary>
    [Theory]
    [InlineData("", true)]
    [InlineData("yesterday", false)]
    [InlineData("about", false)]
    [InlineData("contact", false)]
    public void Only_todays_challenge_widens_for_two_columns(string path, bool widens)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(path);
        var layout = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(_ => { })));

        Assert.Equal(widens, layout.Find("main").ClassList.Contains("oad-measure-split"));
    }
}
