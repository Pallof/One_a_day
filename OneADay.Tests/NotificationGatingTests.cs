using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneADay.Components;
using OneADay.Components.Pages;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// That the anti-abuse measures gate the <b>email</b>, not just the save.
///
/// <para>This is the property that makes the guards worth having on this path. If a
/// blocked submission still notified, the honeypot would be worse than useless: it
/// would look like protection while a script quietly filled the author's inbox — and
/// unlike the JSON inbox, an email inbox has no admin page to clear it from.</para>
///
/// <para>The ordering in both components is <c>guard → save → notify</c>, with the
/// guard returning early. That is one <c>return</c> away from being wrong, and nothing
/// else in the suite would notice if it moved, so it is pinned here.</para>
/// </summary>
public class NotificationGatingTests : BunitContext
{
    private readonly TestEnvironment _env = new();

    /// <summary>A notifier that would really queue, so an unwanted email is visible.</summary>
    private static EmailNotifier LiveNotifier() => new(
        Options.Create(new EmailOptions
        {
            Enabled = true,
            To = "author@example.com",
            From = "author@example.com",
            Host = "smtp.example.com",
            AppPassword = "app-password",
        }),
        NullLogger<EmailNotifier>.Instance);

    private static int Queued(EmailNotifier notifier)
    {
        var n = 0;
        while (notifier.Reader.TryRead(out _))
        {
            n++;
        }
        return n;
    }

    /// <summary>
    /// Backdates the component's "form shown at" stamp so the 5-second compose floor is
    /// already satisfied.
    /// </summary>
    /// <remarks>
    /// Reflection on a private field is a smell, and it is the lesser one here. The
    /// alternative is a real 5-second sleep in every test that needs to get *past* the
    /// timing guard in order to isolate the honeypot — three of them, on a suite that
    /// currently runs in under half a second. If the field is ever renamed this fails
    /// loudly, which is the acceptable failure mode.
    /// </remarks>
    private static void SatisfyComposeFloor(object component, string field)
    {
        var f = component.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"'{field}' not found on {component.GetType().Name} — was it renamed? " +
                    "These tests depend on it to skip the compose floor.");
        f.SetValue(component, DateTime.UtcNow.AddMinutes(-2));
    }

    /// <summary>
    /// A fixed HttpContext, so the per-IP cap is reachable in tests.
    /// </summary>
    /// <remarks>
    /// The real <see cref="HttpContextAccessor"/> is backed by an AsyncLocal that is
    /// empty under bUnit, which makes <c>GetIpHash()</c> return null and silently
    /// disables the per-IP gate. A test that can't reach the gate it claims to test is
    /// worse than no test — it passes for the wrong reason.
    /// </remarks>
    private sealed class FixedHttpContextAccessor(HttpContext? context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get => context; set { } }
    }

    private EmailNotifier RegisterForContact()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var notifier = LiveNotifier();
        Services.AddSingleton(_env.NewSuggestionStore());
        Services.AddSingleton(notifier);

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        Services.AddSingleton<IHttpContextAccessor>(new FixedHttpContextAccessor(context));

        Services.AddDataProtection();
        Services.AddScoped<ProtectedLocalStorage>();
        return notifier;
    }

    private EmailNotifier RegisterForReportIssue()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var notifier = LiveNotifier();
        Services.AddSingleton(_env.NewIssueStore());
        Services.AddSingleton(notifier);
        Services.AddScoped<CurrentTeaserContext>();
        return notifier;
    }

    // ---- suggestions -----------------------------------------------------------

    [Fact]
    public void A_genuine_suggestion_queues_exactly_one_email()
    {
        // The control case. Without this, the tests below would pass even if the
        // notifier were never called at all.
        var notifier = RegisterForContact();
        var cut = Render<Contact>();
        SatisfyComposeFloor(cut.Instance, "_formShownAt");

        cut.Find("#teaser").Input("a genuine riddle");
        cut.Find("#solution").Input("the answer");
        cut.Find("button.oad-btn-green").Click();

        Assert.Equal(1, Queued(notifier));
    }

    [Fact]
    public void A_honeypot_hit_saves_nothing_and_sends_nothing()
    {
        var notifier = RegisterForContact();
        var store = Services.GetRequiredService<SuggestionStore>();
        var cut = Render<Contact>();
        SatisfyComposeFloor(cut.Instance, "_formShownAt");   // only the decoy can fire

        cut.Find("#teaser").Input("spam");
        cut.Find("#solution").Input("spam");
        cut.Find("#website").Input("http://spam.example");   // the decoy
        cut.Find("button.oad-btn-green").Click();

        Assert.Equal(0, Queued(notifier));
        Assert.Empty(store.GetAll());
        Assert.Equal(1, store.BlockedCount);
    }

    [Fact]
    public void Submitting_faster_than_the_compose_floor_sends_nothing()
    {
        // No backdating: the form was "shown" microseconds ago, so the timing guard
        // fires even though the decoy is untouched.
        var notifier = RegisterForContact();
        var store = Services.GetRequiredService<SuggestionStore>();
        var cut = Render<Contact>();

        cut.Find("#teaser").Input("typed impossibly fast");
        cut.Find("#solution").Input("also fast");
        cut.Find("button.oad-btn-green").Click();

        Assert.Equal(0, Queued(notifier));
        Assert.Empty(store.GetAll());
        Assert.Equal(1, store.BlockedCount);
    }

    [Fact]
    public void A_rate_limited_suggestion_sends_nothing()
    {
        // The daily caps are a separate gate from SubmissionGuard, further down
        // Submit(). One that got past the guard but not the cap must not mail either —
        // otherwise the caps limit the inbox on disk while leaving email wide open.
        //
        // Each render mints a fresh visitor id (protected storage is empty in tests),
        // so it is the per-IP cap of 3 that bites here, on the fourth attempt.
        var notifier = RegisterForContact();
        var store = Services.GetRequiredService<SuggestionStore>();

        void SubmitOne(string text)
        {
            var cut = Render<Contact>();
            SatisfyComposeFloor(cut.Instance, "_formShownAt");
            cut.Find("#teaser").Input(text);
            cut.Find("#solution").Input("answer");
            cut.Find("button.oad-btn-green").Click();
        }

        for (var i = 1; i <= SuggestionStore.MaxPerIpPerDay; i++)
        {
            SubmitOne($"allowed {i}");
        }

        Assert.Equal(SuggestionStore.MaxPerIpPerDay, Queued(notifier));   // all allowed mailed
        Assert.Equal(SuggestionStore.MaxPerIpPerDay, store.GetAll().Count);

        // Over the cap the form isn't even rendered — the first of two gates.
        var over = Render<Contact>();
        Assert.Empty(over.FindAll("#teaser"));
        Assert.Contains("already sent us a suggestion", over.Markup);

        // The second gate is server-side. Drive Submit() directly, the way a client
        // that ignored the UI would, and confirm it still refuses — and still doesn't
        // mail. Hiding the form must not be the only thing protecting the inbox.
        //
        // The fields must be populated by hand: there is no form to type into, and an
        // empty _question returns at the blank-field guard long before TryAdd is
        // reached. An earlier version of this test skipped that, so it exercised the
        // blank-field path and proved nothing about the cap — a mutation that moved
        // Enqueue outside `if (accepted)` passed the whole suite.
        SetField(over.Instance, "_question", "one over the cap");
        SetField(over.Instance, "_solutionAndHint", "answer");
        SatisfyComposeFloor(over.Instance, "_formShownAt");
        Submit(over.Instance);

        Assert.Equal(0, Queued(notifier));
        Assert.Equal(SuggestionStore.MaxPerIpPerDay, store.GetAll().Count);
    }

    /// <summary>Invokes a component's private Submit(), bypassing the UI entirely.</summary>
    private static void Submit(object component) =>
        component.GetType()
            .GetMethod("Submit", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(component, null);

    /// <summary>Sets a private field, for reaching state the UI won't render.</summary>
    private static void SetField(object component, string field, object? value)
    {
        var f = component.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"'{field}' not found on {component.GetType().Name} — was it renamed?");
        f.SetValue(component, value);
    }

    [Fact]
    public void An_empty_form_sends_nothing()
    {
        var notifier = RegisterForContact();
        var cut = Render<Contact>();
        SatisfyComposeFloor(cut.Instance, "_formShownAt");

        // Submit is disabled while either field is blank; invoking it anyway proves the
        // server-side path doesn't depend on the button's disabled state.
        Submit(cut.Instance);

        Assert.Equal(0, Queued(notifier));
    }

    // ---- untrusted field values -------------------------------------------------

    [Fact]
    public void A_forged_difficulty_is_normalised_before_it_is_stored_or_mailed()
    {
        // A <select> constrains the browser, not the server. A client driving the
        // circuit can bind anything to _difficulty, and unchecked it would be
        // persisted raw and interpolated into a mail subject.
        var notifier = RegisterForContact();
        var store = Services.GetRequiredService<SuggestionStore>();
        var cut = Render<Contact>();
        SatisfyComposeFloor(cut.Instance, "_formShownAt");

        SetField(cut.Instance, "_difficulty", new string('A', 40_000));
        cut.Find("#teaser").Input("a riddle");
        cut.Find("#solution").Input("an answer");
        cut.Find("button.oad-btn-green").Click();

        Assert.Equal("Medium", store.GetAll().Single().Difficulty);   // fell back, not stored raw

        Assert.True(notifier.Reader.TryRead(out var mail));
        Assert.DoesNotContain("AAAA", mail!.Subject);
        Assert.True(mail.Subject.Length < 200, $"subject was {mail.Subject.Length} chars");
    }

    [Theory]
    [InlineData("Easy", "Easy")]
    [InlineData("Medium", "Medium")]
    [InlineData("Hard", "Hard")]
    [InlineData("hard", "Hard")]          // case is tolerated, not rejected
    [InlineData("Impossible", "Medium")]  // unknown value falls back
    [InlineData("", "Medium")]
    public void Difficulty_round_trips_when_genuine_and_falls_back_when_not(
        string submitted, string expected)
    {
        var notifier = RegisterForContact();
        var store = Services.GetRequiredService<SuggestionStore>();
        var cut = Render<Contact>();
        SatisfyComposeFloor(cut.Instance, "_formShownAt");

        SetField(cut.Instance, "_difficulty", submitted);
        cut.Find("#teaser").Input("a riddle");
        cut.Find("#solution").Input("an answer");
        cut.Find("button.oad-btn-green").Click();

        Assert.Equal(expected, store.GetAll().Single().Difficulty);
    }

    // ---- issue reports ---------------------------------------------------------

    [Fact]
    public void A_genuine_report_queues_exactly_one_email()
    {
        var notifier = RegisterForReportIssue();
        var cut = Render<ReportIssue>();
        cut.Find("button.ri-fab").Click();                      // opens, starts the clock
        SatisfyComposeFloor(cut.Instance, "_dialogShownAt");

        cut.Find("#ri-details").Input("the hint gives it away");
        cut.Find("div.ri-actions button.oad-btn-green").Click();

        Assert.Equal(1, Queued(notifier));
    }

    [Fact]
    public void A_honeypot_hit_on_a_report_saves_nothing_and_sends_nothing()
    {
        var notifier = RegisterForReportIssue();
        var store = Services.GetRequiredService<IssueStore>();
        var cut = Render<ReportIssue>();
        cut.Find("button.ri-fab").Click();
        SatisfyComposeFloor(cut.Instance, "_dialogShownAt");

        cut.Find("#ri-details").Input("spam");
        cut.Find("#ri-website").Input("http://spam.example");   // the decoy
        cut.Find("div.ri-actions button.oad-btn-green").Click();

        Assert.Equal(0, Queued(notifier));
        Assert.Empty(store.GetAll());
        Assert.Equal(1, store.BlockedCount);
    }

    [Fact]
    public void A_report_submitted_too_fast_sends_nothing()
    {
        var notifier = RegisterForReportIssue();
        var store = Services.GetRequiredService<IssueStore>();
        var cut = Render<ReportIssue>();
        cut.Find("button.ri-fab").Click();       // no backdating — instant submit

        cut.Find("#ri-details").Input("typed impossibly fast");
        cut.Find("div.ri-actions button.oad-btn-green").Click();

        Assert.Equal(0, Queued(notifier));
        Assert.Empty(store.GetAll());
        // Without this the test can't tell "the guard fired" from "nothing happened
        // at all" — a broken submit button would pass it just as happily.
        Assert.Equal(1, store.BlockedCount);
    }

    // ---- the shape of the guarantee -------------------------------------------

    [Fact]
    public void Nothing_is_ever_mailed_that_was_not_also_saved()
    {
        // The invariant behind all of the above: the email is a notification *about*
        // something stored. A mail with no matching row would mean the ordering in
        // Submit() had been inverted.
        var notifier = RegisterForContact();
        var store = Services.GetRequiredService<SuggestionStore>();

        var blocked = Render<Contact>();
        SatisfyComposeFloor(blocked.Instance, "_formShownAt");
        blocked.Find("#teaser").Input("spam");
        blocked.Find("#solution").Input("spam");
        blocked.Find("#website").Input("bot");
        blocked.Find("button.oad-btn-green").Click();

        var good = Render<Contact>();
        SatisfyComposeFloor(good.Instance, "_formShownAt");
        good.Find("#teaser").Input("a real one");
        good.Find("#solution").Input("answer");
        good.Find("button.oad-btn-green").Click();

        Assert.Equal(store.GetAll().Count, Queued(notifier));   // 1 stored, 1 mailed
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
