using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Text.Json;
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

    private EmailNotifier RegisterForContact()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var notifier = LiveNotifier();
        Services.AddSingleton(_env.NewSuggestionStore());
        Services.AddSingleton(notifier);

        // Without a fixed address the per-IP cap is unreachable here: see FixedHttpContextAccessor.
        Services.AddSingleton<IHttpContextAccessor>(FixedHttpContextAccessor.From("203.0.113.7"));

        Services.AddDataProtection();
        Services.AddScoped<ProtectedLocalStorage>();

        // The page hashes the address above before storing it. A fixed key keeps the hash
        // stable across the run, so the per-IP cap stays reachable here — the same reason
        // FixedHttpContextAccessor exists.
        Services.AddSingleton(IpHasher.WithKey("notification-gating-tests-fixed-key-00"));
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
        // empty _question returns at the blank-field guard long before Add is
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

    [Fact]
    public void A_caught_bot_spends_none_of_its_addresses_allowance()
    {
        // The guard runs before the store, so a script sharing an address with a real
        // person can't use up their three a day on its way to being rejected. Moved after
        // the store, the three bot hits below would fill the address's allowance (and the
        // inbox) and the real suggestion would be refused.
        RegisterForContact();
        var store = Services.GetRequiredService<SuggestionStore>();

        for (var i = 0; i < SuggestionStore.MaxPerIpPerDay; i++)
        {
            var bot = Render<Contact>();
            SatisfyComposeFloor(bot.Instance, "_formShownAt");
            bot.Find("#teaser").Input($"spam {i}");
            bot.Find("#solution").Input("spam");
            bot.Find("#website").Input("http://spam.example");   // the decoy
            bot.Find("button.oad-btn-green").Click();
        }

        var person = Render<Contact>();
        SatisfyComposeFloor(person.Instance, "_formShownAt");
        person.Find("#teaser").Input("a real riddle");
        person.Find("#solution").Input("its answer");
        person.Find("button.oad-btn-green").Click();

        Assert.Equal("a real riddle", Assert.Single(store.GetAll()).Question);
        Assert.Equal(SuggestionStore.MaxPerIpPerDay, store.BlockedCount);
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

    [Fact]
    public void A_forged_overlong_suggestion_is_cut_to_the_forms_limit_before_it_is_stored_or_mailed()
    {
        // maxlength binds the browser only. bUnit's Input skips it just as a client driving
        // the live connection does, so 40,000 characters arrive — and unchecked, all of them
        // were stored, and every save rewrote them all (security review, 2026-09-28).
        var notifier = RegisterForContact();
        var store = Services.GetRequiredService<SuggestionStore>();
        var cut = Render<Contact>();
        SatisfyComposeFloor(cut.Instance, "_formShownAt");

        cut.Find("#teaser").Input(new string('Q', 40_000));
        cut.Find("#solution").Input(new string('S', 40_000));
        cut.Find("button.oad-btn-green").Click();

        var saved = store.GetAll().Single();
        Assert.Equal(new string('Q', TeaserSuggestion.MaxFieldLength), saved.Question);
        Assert.Equal(new string('S', TeaserSuggestion.MaxFieldLength), saved.SolutionAndHint);

        Assert.True(notifier.Reader.TryRead(out var mail));
        Assert.DoesNotContain(new string('Q', TeaserSuggestion.MaxFieldLength + 1), mail!.Body);
    }

    [Fact]
    public void A_forged_overlong_report_is_cut_to_the_dialogs_limits_before_it_is_stored_or_mailed()
    {
        var notifier = RegisterForReportIssue();
        var store = Services.GetRequiredService<IssueStore>();

        // The page address comes from the client too, and can be as long as its message.
        Services.GetRequiredService<NavigationManager>().NavigateTo("/" + new string('p', 40_000));

        var cut = Render<ReportIssue>();
        cut.Find("button.ri-fab").Click();
        SatisfyComposeFloor(cut.Instance, "_dialogShownAt");

        SetField(cut.Instance, "_category", new string('C', 40_000));   // a category, forged
        cut.Find("#ri-details").Input(new string('D', 40_000));
        cut.Find("div.ri-actions button.oad-btn-green").Click();

        var saved = store.GetAll().Single();
        Assert.Equal(new string('D', IssueReport.MaxDetailsLength), saved.Details);
        Assert.Equal(IssueCategories.Other, saved.Category);
        Assert.Equal(IssueReport.MaxPageUrlLength, saved.PageUrl!.Length);

        Assert.True(notifier.Reader.TryRead(out var mail));
        Assert.DoesNotContain("CCCC", mail!.Subject + mail.Body);
        Assert.DoesNotContain(new string('D', IssueReport.MaxDetailsLength + 1), mail.Body);
    }

    [Fact]
    public void Each_page_shows_the_same_limit_the_server_keeps()
    {
        // A page allowing more than the server keeps would cut real text without a word; one
        // allowing less would hide the server's limit. Both read the one constant.
        RegisterForContact();
        RegisterForReportIssue();
        var contact = Render<Contact>();
        var report = Render<ReportIssue>();
        report.Find("button.ri-fab").Click();

        var limit = TeaserSuggestion.MaxFieldLength.ToString();
        Assert.Equal(limit, contact.Find("#teaser").GetAttribute("maxlength"));
        Assert.Equal(limit, contact.Find("#solution").GetAttribute("maxlength"));
        Assert.Equal(IssueReport.MaxDetailsLength.ToString(),
            report.Find("#ri-details").GetAttribute("maxlength"));
    }

    [Fact]
    public void A_genuine_report_keeps_its_category()
    {
        // The control for the test above: normalising must not flatten real choices to Other.
        var notifier = RegisterForReportIssue();
        var store = Services.GetRequiredService<IssueStore>();
        var cut = Render<ReportIssue>();
        cut.Find("button.ri-fab").Click();
        SatisfyComposeFloor(cut.Instance, "_dialogShownAt");

        cut.Find($"input[name='ri-category'][value='{IssueCategories.Evaluation}']").Change(true);
        cut.Find("#ri-details").Input("my answer was marked wrong");
        cut.Find("div.ri-actions button.oad-btn-green").Click();

        Assert.Equal(IssueCategories.Evaluation, store.GetAll().Single().Category);
    }

    // ---- a full inbox ------------------------------------------------------------

    [Fact]
    public void A_full_suggestion_box_saves_nothing_mails_nothing_and_says_so()
    {
        // The ceiling protects the server's memory. Whoever meets it may be a real person, so
        // they're told rather than thanked for something that was thrown away.
        _env.WriteDataFile("suggestions.json", JsonSerializer.Serialize(
            Enumerable.Range(0, SuggestionStore.MaxStored)
                .Select(i => new TeaserSuggestion { Question = $"waiting {i}" })));
        var notifier = RegisterForContact();
        var store = Services.GetRequiredService<SuggestionStore>();
        var cut = Render<Contact>();
        SatisfyComposeFloor(cut.Instance, "_formShownAt");

        cut.Find("#teaser").Input("one too many");
        cut.Find("#solution").Input("answer");
        cut.Find("button.oad-btn-green").Click();

        Assert.Equal(0, Queued(notifier));
        Assert.Equal(SuggestionStore.MaxStored, store.GetAll().Count);
        Assert.Contains("suggestion box is full", cut.Markup);
        Assert.DoesNotContain("Thanks for your suggestion", cut.Markup);
    }

    [Fact]
    public void A_full_report_inbox_saves_nothing_mails_nothing_and_says_so()
    {
        _env.WriteDataFile("issues.json", JsonSerializer.Serialize(
            Enumerable.Range(0, IssueStore.MaxOpenReports)
                .Select(i => new IssueReport { Details = $"open {i}" })));
        var notifier = RegisterForReportIssue();
        var store = Services.GetRequiredService<IssueStore>();
        var cut = Render<ReportIssue>();
        cut.Find("button.ri-fab").Click();
        SatisfyComposeFloor(cut.Instance, "_dialogShownAt");

        cut.Find("#ri-details").Input("one too many");
        cut.Find("div.ri-actions button.oad-btn-green").Click();

        Assert.Equal(0, Queued(notifier));
        Assert.Equal(IssueStore.MaxOpenReports, store.GetAll().Count);
        Assert.Contains("report inbox is full", cut.Markup);
        Assert.DoesNotContain("Thanks for letting us know", cut.Markup);
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
