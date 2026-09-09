using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace OneADay.Services;

/// <summary>One queued notification. Plain text; no user content ever reaches a header.</summary>
public sealed record Notification(string Subject, string Body);

/// <summary>
/// Hands notifications to the background sender.
///
/// <para><b>The contract that matters:</b> enqueuing must never block, never throw, and
/// never affect whether the thing being reported was saved. A suggestion or issue is
/// written to disk first and is the valuable artefact; the email is a convenience. If
/// mail is misconfigured, the SMTP host is down, or the queue is full, the visitor
/// still gets their confirmation and the author still finds the item in admin.</para>
///
/// <para>This matters more than it looks: <c>Submit()</c> runs on the SignalR circuit,
/// so anything slow or throwing here would stall — or fail — a real person's
/// submission.</para>
/// </summary>
public sealed class EmailNotifier
{
    private readonly Channel<Notification> _queue;
    private readonly EmailOptions _options;
    private readonly ILogger<EmailNotifier> _log;

    /// <summary>How many notifications may be pending before the oldest are discarded.</summary>
    public const int Capacity = 100;

    public EmailNotifier(IOptions<EmailOptions> options, ILogger<EmailNotifier> log)
    {
        _options = options.Value;
        _log = log;

        // Bounded, and full means drop. Issue reports are uncapped by design, so an
        // unbounded queue would be a memory leak with a hostile trigger. Dropping is
        // safe because nothing is lost — the item is already on disk.
        //
        // DropOldest, not DropWrite. Under a burst, DropWrite keeps the *first* 100 and
        // silently discards everything after, so the author is told about the start of
        // a flood and never learns it continued. Keeping the newest is the more useful
        // half when the point of the feature is knowing what just happened.
        _queue = Channel.CreateBounded<Notification>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
    }

    /// <summary>
    /// The queue to drain. <b>Exactly one consumer</b> — <see cref="EmailSenderService"/>
    /// — may read this; tests drain it directly, but only ever one at a time.
    /// </summary>
    /// <remarks>
    /// The channel is created with <c>SingleReader = true</c>, which lets it skip
    /// synchronisation it would otherwise need. Nothing enforces that at the type
    /// level, so a second concurrent consumer would be undefined behaviour rather than
    /// a compile error. If a second reader is ever wanted, drop that flag first.
    /// </remarks>
    public ChannelReader<Notification> Reader => _queue.Reader;

    /// <summary>
    /// Queues a notification. Returns immediately; failure is swallowed on purpose.
    /// </summary>
    public void Enqueue(string subject, string body)
    {
        if (!_options.IsConfigured)
        {
            return;   // not set up — do nothing at all, quietly
        }

        try
        {
            // Strip newlines: user-supplied text must never reach a mail header, where
            // a CR/LF would let it inject headers of its own.
            var safeSubject = subject.Replace("\r", " ").Replace("\n", " ").Trim();

            // Check for a full queue *before* writing. Under every dropping FullMode,
            // TryWrite returns true even as it discards — so branching on its result
            // produces a warning that can never fire, which is what this code did
            // before. A full queue means the sender is stalled or badly behind, and
            // that is worth knowing about precisely because the drop is otherwise
            // invisible: the item is on disk, but nobody is told it arrived.
            if (_queue.Reader.CanCount && _queue.Reader.Count >= Capacity)
            {
                _log.LogWarning(
                    "Notification queue is at capacity ({Capacity}); discarding the oldest " +
                    "pending notification. Items are still saved — only the email is lost. " +
                    "Newest subject: {Subject}", Capacity, safeSubject);
            }

            _queue.Writer.TryWrite(new Notification(safeSubject, body));
        }
        catch (Exception ex)
        {
            // Nothing about a notification is worth surfacing to a visitor.
            _log.LogError(ex, "Failed to queue a notification");
        }
    }
}
