using System.Threading.Channels;
using Microsoft.Extensions.Options;
using OneADay.Models;

namespace OneADay.Services;

/// <summary>
/// Queue for confirmation emails. The sign-up form runs on the SignalR circuit, so it
/// enqueues and returns at once; a mail failure must never cost a visitor their sign-up
/// or stall the page.
/// </summary>
public sealed class ConfirmationQueue
{
    public const int Capacity = 100;

    private readonly Channel<OutgoingMail> _queue;
    private readonly EmailOptions _options;

    /// <param name="subscribers">Told about each confirmation pushed out of a full queue, so
    /// that sign-up can ask again (<see cref="SubscriberStore.ConfirmationNotSent"/>).</param>
    public ConfirmationQueue(IOptions<EmailOptions> options, SubscriberStore? subscribers = null)
    {
        _options = options.Value;
        _queue = Channel.CreateBounded<OutgoingMail>(
            new BoundedChannelOptions(Capacity)
            {
                // Newest kept: under a burst, the most recent sign-ups are the ones a real
                // person is sitting waiting on.
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            },
            itemDropped: mail => subscribers?.ConfirmationNotSent(mail.To));
    }

    public ChannelReader<OutgoingMail> Reader => _queue.Reader;

    /// <summary>Never blocks, never throws. A no-op when email isn't configured.</summary>
    public void Enqueue(OutgoingMail mail)
    {
        if (!_options.IsConfigured)
        {
            return;
        }
        _queue.Writer.TryWrite(mail);
    }
}

/// <summary>
/// Sends confirmation emails, under a global daily ceiling.
/// </summary>
/// <remarks>
/// The ceiling is the thing that matters here. Double opt-in means nothing is ever
/// <i>broadcast</i> to an address that didn't confirm — but the confirmation itself still
/// goes out, so a script submitting a thousand strangers' addresses would otherwise make
/// the site send a thousand unsolicited emails from the author's Gmail. That is exactly
/// the pattern that gets an account suspended, and the same account carries the author
/// notifications and the daily digest. A fixed cap bounds the damage whatever the source,
/// which a per-IP limit can't: IPs rotate, and on a circuit the IP is often unknown.
/// (<see cref="SignUpLimit"/> adds one anyway, so a single script can't spend the whole cap.)
/// </remarks>
public sealed class ConfirmationSender(
    ConfirmationQueue queue,
    SmtpMailer mailer,
    SubscriberStore subscribers,
    IOptions<SubscriptionOptions> options,
    ILogger<ConfirmationSender> log) : BackgroundService
{
    private readonly DailySendBudget _budget = new(options.Value.MaxConfirmationsPerDay);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!mailer.IsConfigured)
        {
            return;
        }

        await foreach (var mail in queue.Reader.ReadAllAsync(stoppingToken))
        {
            await SendOneAsync(mail, stoppingToken);
        }
    }

    /// <summary>Sends one confirmation, or drops it over the day's cap. Public for tests.</summary>
    public async Task SendOneAsync(OutgoingMail mail, CancellationToken token)
    {
        var today = AppTime.Today;
        if (!_budget.HasRoom(today))
        {
            // Dropped, not deferred — and the sign-up can ask again straight away, rather than
            // wait 24 hours for an email that never went out.
            log.LogWarning("Daily confirmation cap ({Cap}) reached; this confirmation " +
                           "was dropped. The sign-up stays pending and can be requested " +
                           "again at once.", _budget.MaxPerDay);
            subscribers.ConfirmationNotSent(mail.To);
            return;
        }

        if (await mailer.SendAsync(mail, token))
        {
            _budget.RecordSent(today);
        }
        else
        {
            subscribers.ConfirmationNotSent(mail.To);
        }
    }
}
