using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneADay.Components;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The sign-up dialog. Two rules carry the whole feature, and both are invisible in the
/// markup: **the same screen for every outcome**, so nobody can test whether an address
/// is on the list, and **nothing queued unless the sign-up is new**, so the confirmation
/// can't be aimed at a stranger's inbox.
/// </summary>
public class SubscribeDialogTests : BunitContext
{
    private static readonly DateTime T0 = new(2026, 9, 11, 16, 0, 0, DateTimeKind.Utc);
    private const string Inbox = "Check your inbox";

    private readonly TestEnvironment _env = new();
    private readonly SubscriberStore _store;
    private readonly ConfirmationQueue _queue;

    public SubscribeDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _store = _env.NewSubscriberStore();
        _queue = new ConfirmationQueue(Options.Create(new EmailOptions
        {
            Enabled = true, To = "a@x.com", From = "a@x.com", Host = "h", AppPassword = "p",
        }));
        Services.AddSingleton(_store);
        Services.AddSingleton(_queue);
        Services.AddSingleton(Options.Create(new SiteOptions { BaseUrl = "https://site" }));
    }

    /// <summary>How many confirmations are waiting to be sent.</summary>
    private int Queued()
    {
        var count = 0;
        while (_queue.Reader.TryRead(out _))
        {
            count++;
        }
        return count;
    }

    /// <summary>Opens the dialog and backdates its clock past the compose floor.</summary>
    private IRenderedComponent<SubscribeDialog> Open()
    {
        var dialog = Render<SubscribeDialog>();
        dialog.Find("button.sd-open").Click();

        var field = typeof(SubscribeDialog).GetField("_openedAt", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "'_openedAt' not found on SubscribeDialog — was it renamed? These tests " +
                "depend on it to skip the compose floor.");
        field.SetValue(dialog.Instance, DateTime.UtcNow.AddMinutes(-2));

        return dialog;
    }

    private static void SignUp(IRenderedComponent<SubscribeDialog> dialog, string address, string? honeypot = null)
    {
        dialog.Find("#sd-email").Input(address);
        if (honeypot is not null)
        {
            dialog.Find("#sd-website").Input(honeypot);
        }
        dialog.Find("button.oad-btn-green").Click();
    }

    [Fact]
    public void A_new_address_is_stored_and_queues_exactly_one_confirmation()
    {
        // The control case. Without it, every test below would pass with the dialog
        // wired to nothing at all.
        var dialog = Open();

        SignUp(dialog, "reader@example.com");

        Assert.Equal(1, Queued());
        Assert.Equal((0, 1), _store.Counts);   // pending, not confirmed
        Assert.Contains(Inbox, dialog.Markup);
        Assert.Contains("spam folder", dialog.Markup);
    }

    [Fact]
    public void An_address_already_subscribed_sees_the_same_screen_and_queues_nothing()
    {
        // The anti-enumeration rule: "you're already subscribed" would let anyone test
        // whether an address reads this site.
        var token = _store.Request("reader@example.com", T0).Token!;
        _store.Confirm(token, T0);

        var dialog = Open();
        SignUp(dialog, "reader@example.com");

        Assert.Equal(0, Queued());
        Assert.Contains(Inbox, dialog.Markup);
    }

    [Fact]
    public void Signing_up_again_while_pending_sees_the_same_screen_and_queues_nothing()
    {
        _store.Request("reader@example.com", DateTime.UtcNow);

        var dialog = Open();
        SignUp(dialog, "reader@example.com");

        Assert.Equal(0, Queued());
        Assert.Contains(Inbox, dialog.Markup);
    }

    [Fact]
    public void A_honeypot_hit_sees_the_same_screen_and_nothing_is_stored_or_queued()
    {
        var dialog = Open();

        SignUp(dialog, "bot@example.com", honeypot: "http://spam.example");

        Assert.Equal(0, Queued());
        Assert.Equal((0, 0), _store.Counts);
        Assert.Contains(Inbox, dialog.Markup);   // never tell a script which check caught it
    }

    [Fact]
    public void A_submission_faster_than_the_floor_stores_and_queues_nothing()
    {
        var dialog = Render<SubscribeDialog>();
        dialog.Find("button.sd-open").Click();   // no backdating: the clock starts now

        SignUp(dialog, "bot@example.com");

        Assert.Equal(0, Queued());
        Assert.Equal((0, 0), _store.Counts);
        Assert.Contains(Inbox, dialog.Markup);
    }

    [Fact]
    public void An_unusable_address_is_the_one_message_allowed_to_differ()
    {
        var dialog = Open();

        SignUp(dialog, "not-an-address");

        Assert.Equal(0, Queued());
        Assert.Equal((0, 0), _store.Counts);
        Assert.DoesNotContain(Inbox, dialog.Markup);
        Assert.Contains("doesn't look like an email address", dialog.Markup);
    }

    [Fact]
    public void The_confirmation_carries_an_absolute_link_with_the_subscribers_token()
    {
        // Relative links are meaningless in an inbox, and the token has to be the one
        // stored for that address or the link confirms nothing.
        var dialog = Open();
        SignUp(dialog, "reader@example.com");

        Assert.True(_queue.Reader.TryRead(out var mail));
        Assert.Equal("reader@example.com", mail!.To);
        var token = _store.FindByToken(ExtractToken(mail.Body));
        Assert.NotNull(token);
        Assert.Equal("reader@example.com", token!.Email);
        Assert.Contains($"https://site/subscribe/confirm?token=", mail.Body);
    }

    private static string ExtractToken(string body)
    {
        var marker = "token=";
        var start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = body.IndexOfAny([' ', '\r', '\n'], start);
        return end < 0 ? body[start..] : body[start..end];
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
