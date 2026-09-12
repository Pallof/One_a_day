using Microsoft.Extensions.Options;
using OneADay.Models;

namespace OneADay.Services;

/// <summary>
/// Drains the author-notification queue, off the request path entirely.
///
/// <para>Sending itself lives in <see cref="SmtpMailer"/>, shared with the subscriber
/// digest; this service owns only the policy for author pings — one queue, one small
/// daily budget. The digest deliberately does <b>not</b> go through here: this queue
/// holds 100 and the budget allows 25 a day, so a broadcast would be silently truncated
/// and would starve these notifications while it did.</para>
/// </summary>
public sealed class EmailSenderService(
    EmailNotifier notifier,
    SmtpMailer mailer,
    IOptions<EmailOptions> options,
    ILogger<EmailSenderService> log) : BackgroundService
{
    private readonly EmailOptions _options = options.Value;

    /// <summary>Today's remaining allowance. Rolls at midnight Pacific, like every
    /// other day boundary in the app.</summary>
    private readonly DailySendBudget _budget = new(options.Value.MaxPerDay);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsConfigured)
        {
            log.LogInformation("Email notifications are off (not configured).");
            return;
        }

        log.LogInformation("Email notifications on; delivering to {To}", _options.To);

        await foreach (var item in notifier.Reader.ReadAllAsync(stoppingToken))
        {
            var today = AppTime.Today;

            if (!_budget.HasRoom(today))
            {
                log.LogWarning("Daily email cap ({Cap}) reached; further notifications " +
                               "are skipped today. Items are still saved.", _budget.MaxPerDay);
                continue;   // still on disk; just not mailed
            }

            // The slot is spent only on success, so an outage can't burn the day's
            // budget on mail that never arrived.
            if (await mailer.SendAsync(new OutgoingMail(_options.To, item.Subject, item.Body), stoppingToken))
            {
                _budget.RecordSent(today);
            }
        }
    }
}
