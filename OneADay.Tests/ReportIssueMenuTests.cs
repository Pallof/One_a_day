using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneADay.Components;
using OneADay.Components.Layout;
using OneADay.Components.Pages;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// "Report an issue" in the menu, on every page (PRD 06): an issue can turn up anywhere, and on a
/// phone the floating button sits under the thumb. The floating button stays on the challenge.
/// </summary>
public class ReportIssueMenuTests : BunitContext
{
    private readonly TestEnvironment _env = new();
    private readonly IssueStore _issues;

    /// <summary>Queues for real, so the author's email can be read; nothing here sends it.</summary>
    private readonly EmailNotifier _notifier = new(
        Options.Create(new EmailOptions
        {
            Enabled = true, To = "author@example.com", From = "author@example.com",
            Host = "smtp.example.com", AppPassword = "app-password",
        }),
        NullLogger<EmailNotifier>.Instance);

    public ReportIssueMenuTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _issues = Services.AddReportDialog(_env, _notifier);
        Services.AddMetrics(_env);   // the Twenty Four page counts its players

        // The challenge page's footer also offers the email sign-up, which needs these.
        Services.AddSingleton(_env.NewSubscriberStore());
        Services.AddSingleton(new ConfirmationQueue(Options.Create(new EmailOptions())));
        Services.AddSingleton(new SignUpLimit(Options.Create(new SubscriptionOptions())));
        Services.AddSingleton<IHttpContextAccessor>(FixedHttpContextAccessor.From("203.0.113.7"));
        Services.AddSingleton(Options.Create(new SiteOptions { BaseUrl = "https://site" }));

        // Last: it reads the services, and bUnit locks them on the first read.
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private NavigationManager Nav => Services.GetRequiredService<NavigationManager>();

    private static readonly RenderFragment EmptyPage = _ => { };

    private static readonly RenderFragment TwentyFourPage = builder =>
    {
        builder.OpenComponent<TwentyFour>(0);
        builder.CloseComponent();
    };

    /// <summary>The layout at this address, around an empty page unless given one.</summary>
    private IRenderedComponent<MainLayout> Layout(string path, RenderFragment? page = null)
    {
        Nav.NavigateTo(path);
        return Render<MainLayout>(p => p.Add(l => l.Body, page ?? EmptyPage));
    }

    private static void OpenFromMenu(IRenderedComponent<MainLayout> layout) =>
        layout.Find(".oad-menu-panel li button.oad-menu-action").Click();

    /// <summary>Fills in and sends the open dialog, past the bot check's five-second floor.</summary>
    private static void Send(IRenderedComponent<MainLayout> layout, string details)
    {
        var dialog = layout.FindComponent<ReportIssue>().Instance;
        typeof(ReportIssue).GetField("_dialogShownAt", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(dialog, DateTime.UtcNow.AddMinutes(-2));
        layout.Find("#ri-details").Input(details);
        layout.Find("div.ri-actions button.oad-btn-green").Click();
    }

    private static string HandOnScreen(IRenderedComponent<MainLayout> layout) =>
        string.Join(" ", layout.FindAll(".tf-card .tf-pip").Select(p => p.TextContent.Trim()));

    [Theory]
    [InlineData("")]
    [InlineData("yesterday")]
    [InlineData("twentyfour")]
    [InlineData("about")]
    [InlineData("contact")]
    public void Every_pages_menu_opens_the_report_dialog(string path)
    {
        var layout = Layout(path);

        var entry = layout.Find(".oad-menu-panel li button.oad-menu-action");
        Assert.Equal("⚠️ Report an issue", entry.TextContent.Trim());
        Assert.Empty(layout.FindAll(".ri-modal"));

        entry.Click();

        Assert.Single(layout.FindAll(".ri-modal"));
    }

    [Fact]
    public void Opening_the_dialog_moves_the_focus_into_it_which_closes_the_menu()
    {
        // The menu is open for as long as anything inside it has the focus, and a tapped
        // button keeps it. Left there, the menu would sit open behind the dialog.
        var layout = Layout("about");

        OpenFromMenu(layout);

        var focused = (ElementReference)JSInterop.VerifyFocusAsyncInvoke().Arguments[0]!;
        focused.ShouldBeElementReferenceTo(layout.Find(".ri-modal"));
    }

    [Theory]
    [InlineData("", "at every width")]
    [InlineData("yesterday", "on wide screens")]
    [InlineData("twentyfour", "on wide screens")]
    [InlineData("contact", "on wide screens")]
    [InlineData("about", "nowhere")]
    public void The_floating_button_shows_by_page(string path, string where)
    {
        // Wide screens only is the stylesheet's doing, below the menu's 900px breakpoint;
        // what the page decides is whether the button carries that class.
        var buttons = Layout(path).FindAll("button.ri-fab");

        var shown = buttons.Count switch
        {
            0 => "nowhere",
            1 when buttons[0].ClassList.Contains("ri-fab-wide-only") => "on wide screens",
            1 => "at every width",
            _ => "more than once",
        };
        Assert.Equal(where, shown);
    }

    [Fact]
    public void A_report_from_another_page_is_filed_with_that_pages_address()
    {
        var layout = Layout("contact");
        OpenFromMenu(layout);

        Send(layout, "the form wouldn't send");

        var report = Assert.Single(_issues.GetAll());
        Assert.Equal("/contact", report.PageUrl);
        Assert.Null(report.TeaserQuestion);
        Assert.Null(report.TwentyFourHand);
    }

    // ---- the Twenty Four hand ---------------------------------------------------------

    [Fact]
    public void A_report_from_twenty_four_names_the_hand_on_screen()
    {
        // After a Pass, so the hand named is the one on screen, not the first one dealt.
        var layout = Layout("twentyfour", TwentyFourPage);
        layout.Find("button.tf-pass").Click();
        var hand = HandOnScreen(layout);

        OpenFromMenu(layout);
        Assert.Contains($"the Twenty Four hand {hand}", layout.Find(".ri-context").TextContent);
        Send(layout, "my answer was marked wrong");

        Assert.Equal(hand, string.Join(" ", Assert.Single(_issues.GetAll()).TwentyFourHand!));
        Assert.True(_notifier.Reader.TryRead(out var mail));
        Assert.Contains($"24 hand   : {hand}", mail!.Body);
    }

    [Fact]
    public void Leaving_the_game_stops_naming_its_hand()
    {
        var layout = Layout("twentyfour", TwentyFourPage);

        layout.InvokeAsync(() => Nav.NavigateTo("contact"));
        layout.Render(p => p.Add(l => l.Body, EmptyPage));   // the game's page goes
        OpenFromMenu(layout);

        Assert.Empty(layout.FindAll(".ri-context"));
        Send(layout, "the form wouldn't send");
        Assert.Null(Assert.Single(_issues.GetAll()).TwentyFourHand);
    }

    [Fact]
    public void Leaving_the_page_closes_the_dialog()
    {
        // It lives in the layout, so it would otherwise ride along, open, onto the next page —
        // still holding the last page's question.
        var layout = Layout("about");
        OpenFromMenu(layout);

        layout.InvokeAsync(() => Nav.NavigateTo("contact"));

        Assert.Empty(layout.FindAll(".ri-modal"));
    }

    [Fact]
    public void A_page_that_clears_up_late_leaves_the_next_pages_hand_alone()
    {
        // Leaving one page and arriving at the next happen in one step, in no set order:
        // the arriving page may already have named its hand when the leaving one clears up.
        var onScreen = new CurrentTeaserContext();
        int[] leaving = [1, 2, 3, 4], arriving = [5, 6, 7, 8];

        onScreen.SetHand(leaving);
        onScreen.SetHand(arriving);
        onScreen.ClearHand(leaving);

        Assert.Same(arriving, onScreen.Hand);
    }

    // ---- the form's categories --------------------------------------------------------

    [Fact]
    public void The_categories_are_three_whole_choices_with_the_first_picked()
    {
        // A list rather than a dropdown, which can't wrap and cut the longest one off on a
        // phone. Each label carries its category whole; the first is picked, as the form's
        // own default is.
        var layout = Layout("about");
        OpenFromMenu(layout);

        var choices = layout.FindAll(".ri-categories label.ri-choice");
        Assert.Equal(IssueCategories.All, choices.Select(c => c.TextContent.Trim()));
        Assert.Equal([true, false, false],
            choices.Select(c => c.QuerySelector("input[type=radio]")!.HasAttribute("checked")));
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
