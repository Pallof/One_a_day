using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The notifier's job is to be harmless.
///
/// It sits on the submit path of a real visitor's suggestion or issue report, so the
/// properties worth pinning are all negative ones: it must not block, must not throw,
/// and must not care whether email is configured. If any of those break, a visitor
/// loses a submission over a mail problem — which is the wrong trade in every case.
/// </summary>
public class EmailNotifierTests
{
    private static EmailNotifier Build(EmailOptions options) =>
        new(Options.Create(options), NullLogger<EmailNotifier>.Instance);

    private static EmailOptions Configured() => new()
    {
        Enabled = true,
        To = "author@example.com",
        From = "author@example.com",
        Host = "smtp.example.com",
        AppPassword = "app-password",
    };

    // ---- when it is switched off or half-configured ----------------------------

    [Fact]
    public void Unconfigured_by_default()
    {
        // The default must be "off", so a fresh clone and CI never try to send.
        Assert.False(new EmailOptions().IsConfigured);
    }

    [Theory]
    [InlineData(false, "to@x.com", "from@x.com", "host", "pw")]   // switched off
    [InlineData(true, "", "from@x.com", "host", "pw")]            // no recipient
    [InlineData(true, "to@x.com", "", "host", "pw")]              // no sender
    [InlineData(true, "to@x.com", "from@x.com", "", "pw")]        // no host
    [InlineData(true, "to@x.com", "from@x.com", "host", "")]      // no app password
    public void Any_missing_piece_means_not_configured(
        bool enabled, string to, string from, string host, string password)
    {
        var options = new EmailOptions
        {
            Enabled = enabled, To = to, From = from, Host = host, AppPassword = password,
        };

        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void Enqueuing_while_unconfigured_does_nothing_and_does_not_throw()
    {
        var notifier = Build(new EmailOptions());   // off

        notifier.Enqueue("subject", "body");

        Assert.False(notifier.Reader.TryRead(out _));
    }

    // ---- when it is configured -------------------------------------------------

    [Fact]
    public void A_configured_notifier_queues_the_message()
    {
        var notifier = Build(Configured());

        notifier.Enqueue("Stumpty — new teaser suggestion (Hard)", "the body");

        Assert.True(notifier.Reader.TryRead(out var queued));
        Assert.Equal("Stumpty — new teaser suggestion (Hard)", queued!.Subject);
        Assert.Equal("the body", queued.Body);
    }

    [Fact]
    public void Enqueue_returns_without_waiting_for_anything()
    {
        // It runs on the SignalR circuit, so it has to be effectively instant even
        // with no reader draining the queue.
        //
        // Two things this deliberately does that the first version didn't. It writes
        // well past capacity, so the full-queue path is actually exercised rather than
        // skipped — that path is where a naive implementation would block waiting for
        // room. And the budget is tight: 500 calls in 250ms is 0.5ms each, where the
        // earlier "1 second for 50" allowed 20ms apiece and would have hidden a real
        // disk or network call on the circuit.
        const int calls = 500;   // 5x the queue bound
        var notifier = Build(Configured());

        var started = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < calls; i++)
        {
            notifier.Enqueue($"subject {i}", "body");
        }
        started.Stop();

        Assert.True(started.ElapsedMilliseconds < 250,
            $"{calls} enqueues took {started.ElapsedMilliseconds}ms — Enqueue is doing " +
            "real work on the circuit, or blocking when the queue is full.");
    }

    // ---- hostile and awkward input ---------------------------------------------

    [Theory]
    [InlineData("subject\r\nBcc: someone@evil.com")]
    [InlineData("subject\nX-Injected: yes")]
    [InlineData("line one\rline two")]
    public void Newlines_are_stripped_from_the_subject(string hostile)
    {
        // Visitor text reaches the subject line, and a bare CR/LF there is a header
        // injection. Strip rather than trust the mail library to notice.
        var notifier = Build(Configured());

        notifier.Enqueue(hostile, "body");

        Assert.True(notifier.Reader.TryRead(out var queued));
        Assert.DoesNotContain('\r', queued!.Subject);
        Assert.DoesNotContain('\n', queued.Subject);
    }

    [Fact]
    public void The_body_is_left_alone()
    {
        // Only headers are injectable; the body should arrive as written, newlines and
        // all, or a multi-line teaser would be mangled.
        var notifier = Build(Configured());
        const string body = "line one\nline two\n\nline four";

        notifier.Enqueue("subject", body);

        Assert.True(notifier.Reader.TryRead(out var queued));
        Assert.Equal(body, queued!.Body);
    }

    [Fact]
    public void A_flood_drops_messages_rather_than_growing_without_bound()
    {
        // Issue reports are uncapped by design, so an unbounded queue would be a
        // memory leak anyone could trigger. Dropping is safe: the report is already
        // saved to disk before this is ever called.
        const int flood = 5_000;
        var notifier = Build(Configured());

        for (var i = 0; i < flood; i++)
        {
            notifier.Enqueue($"subject {i}", "body");
        }

        var kept = new List<string>();
        while (notifier.Reader.TryRead(out var item))
        {
            kept.Add(item.Subject);
        }

        Assert.Equal(EmailNotifier.Capacity, kept.Count);   // bounded, never 5,000
    }

    [Fact]
    public void A_flood_keeps_the_newest_notifications_not_the_oldest()
    {
        // The direction matters and is easy to get backwards. DropWrite — the obvious
        // choice, and what this used first — keeps the *first* 100 and silently
        // discards everything after, so the author would be told about the start of a
        // burst and never learn it continued. The most recent activity is the half
        // worth keeping.
        const int flood = 5_000;
        var notifier = Build(Configured());

        for (var i = 0; i < flood; i++)
        {
            notifier.Enqueue($"subject {i}", "body");
        }

        var kept = new List<string>();
        while (notifier.Reader.TryRead(out var item))
        {
            kept.Add(item.Subject);
        }

        Assert.Equal($"subject {flood - EmailNotifier.Capacity}", kept[0]);   // 4900
        Assert.Equal($"subject {flood - 1}", kept[^1]);                       // 4999
        Assert.DoesNotContain("subject 0", kept);
    }

    // ---- the daily cap ---------------------------------------------------------

    [Fact]
    public void The_daily_cap_has_a_sane_default()
    {
        // Enough for a real day's feedback, low enough that a burst can't bury the
        // inbox. Whatever the number, it must not be zero or unlimited.
        var cap = new EmailOptions().MaxPerDay;
        Assert.InRange(cap, 1, 500);
    }
}
