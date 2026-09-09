using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using OneADay.Models;

namespace OneADay.Services;

/// <summary>
/// Drains the notification queue and sends over SMTP, off the request path entirely.
///
/// <para>Uses the built-in <see cref="SmtpClient"/> rather than MailKit so the project
/// keeps its zero-package dependency list. That is adequate for low-volume mail to a
/// single inbox and nothing more — see the scale note in PRD 14.</para>
/// </summary>
public sealed class EmailSenderService(
    EmailNotifier notifier,
    IOptions<EmailOptions> options,
    ILogger<EmailSenderService> log) : BackgroundService
{
    private const int MaxAttempts = 3;

    /// <summary>
    /// How long to wait on one SMTP attempt.
    /// </summary>
    /// <remarks>
    /// <see cref="SmtpClient"/> defaults to <b>100 seconds</b>. Because the drain loop
    /// awaits each send in turn, one message to an unreachable host would otherwise
    /// block the queue for 3 × 100s plus backoff — over five minutes — during which
    /// every new notification is silently discarded once the queue fills. Gmail
    /// normally answers in under two seconds, so fifteen is generous and caps the
    /// worst case at roughly fifty seconds.
    /// </remarks>
    private const int SendTimeoutMs = 15_000;

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
            if (await SendWithRetryAsync(item, stoppingToken))
            {
                _budget.RecordSent(today);
            }
        }
    }

    /// <summary>Sends one notification. Returns true only if it actually went out.</summary>
    private async Task<bool> SendWithRetryAsync(Notification item, CancellationToken token)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var client = new SmtpClient(_options.Host, _options.Port)
                {
                    EnableSsl = true,   // STARTTLS on 587
                    Timeout = SendTimeoutMs,
                    Credentials = new NetworkCredential(_options.From, _options.AppPassword),
                };

                using var message = new MailMessage(_options.From, _options.To)
                {
                    Subject = item.Subject,
                    Body = item.Body,
                    IsBodyHtml = false,   // plain text: nothing to escape, renders anywhere
                };

                await client.SendMailAsync(message, token);
                return true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Shutting down, not failing. Rethrow so the host stops the loop
                // cleanly rather than logging a spurious "retrying" on every restart.
                throw;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                // Transient more often than not — a blip, a throttle, a DNS hiccup.
                log.LogWarning(ex, "Email attempt {Attempt} failed; retrying", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), token);
            }
            catch (Exception ex)
            {
                // Give up on this one. The suggestion or report is already on disk and
                // visible in admin, so nothing is lost — the author just isn't pinged.
                log.LogError(ex, "Email failed after {Attempts} attempts: {Subject}",
                    MaxAttempts, item.Subject);
                return false;
            }
        }

        return false;
    }
}
