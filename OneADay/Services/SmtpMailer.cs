using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Options;

namespace OneADay.Services;

/// <summary>One outgoing message.</summary>
/// <param name="Body">Plain text. Always sent — on its own, or as the fallback to Html.</param>
/// <param name="Headers">Recipient-specific headers, such as List-Unsubscribe.</param>
/// <param name="Html">An optional styled version. The author's own notifications leave it
/// null and stay plain text; subscriber mail sets it.</param>
public sealed record OutgoingMail(
    string To,
    string Subject,
    string Body,
    IReadOnlyDictionary<string, string>? Headers = null,
    string? Html = null);

/// <summary>
/// Sends one message over SMTP, with retry.
///
/// <para>Shared by the author notifier and the subscriber digest so both go through the
/// same timeout, retry and logging rules. It deliberately owns no queue and no budget:
/// those are policy, and the two callers need different ones — a handful of author
/// pings a day versus one message per subscriber.</para>
///
/// <para>Uses the built-in <see cref="SmtpClient"/> rather than MailKit, keeping the
/// project's zero-package dependency list. Adequate for low volume through one Gmail
/// account and nothing more — see PRD 14 and PRD 15.</para>
/// </summary>
public class SmtpMailer(IOptions<EmailOptions> options, ILogger<SmtpMailer> log)
{
    private const int MaxAttempts = 3;

    /// <summary>
    /// How long to wait on one SMTP attempt. <see cref="SmtpClient"/> defaults to 100s;
    /// callers send serially, so one unreachable host would otherwise stall everything
    /// behind it for over five minutes. Gmail normally answers in under two seconds.
    /// </summary>
    private const int SendTimeoutMs = 15_000;

    private readonly EmailOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    /// <summary>Sends one message. Returns true only if it actually went out.</summary>
    public virtual async Task<bool> SendAsync(OutgoingMail mail, CancellationToken token)
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

                using var message = BuildMessage(_options.From, mail);
                await client.SendMailAsync(message, token);
                return true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Shutting down, not failing — let the host stop the loop cleanly.
                throw;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                log.LogWarning(ex, "Email attempt {Attempt} failed; retrying", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), token);
            }
            catch (Exception ex)
            {
                // The recipient address is deliberately not logged: for the digest it is
                // a subscriber's personal data, and logs outlive the subscription.
                log.LogError(ex, "Email failed after {Attempts} attempts: {Subject}",
                    MaxAttempts, mail.Subject);
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// The MIME message for one mail. Separate from sending so its shape can be tested
    /// without a server.
    /// </summary>
    /// <remarks>
    /// With Html set this is <c>multipart/alternative</c>: plain text first, HTML last.
    /// Clients show the last part they can render, so HTML wins where it's supported and
    /// the text is what's left everywhere else — including to spam filters, which score
    /// HTML-only mail as suspicious.
    /// </remarks>
    public static MailMessage BuildMessage(string from, OutgoingMail mail)
    {
        var message = new MailMessage(from, mail.To)
        {
            Subject = mail.Subject,
            SubjectEncoding = Encoding.UTF8,   // the subjects carry — and ·
            Body = mail.Body,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
        };

        if (mail.Html is not null)
        {
            var html = AlternateView.CreateAlternateViewFromString(mail.Html, Encoding.UTF8, MediaTypeNames.Text.Html);
            // Pinned, not left to the default: the HTML's lines run far past SMTP's
            // 998-byte line limit, so it must never go out as raw 7bit/8bit.
            html.TransferEncoding = TransferEncoding.Base64;
            message.AlternateViews.Add(html);
        }

        if (mail.Headers is not null)
        {
            foreach (var (name, value) in mail.Headers)
            {
                message.Headers.Add(name, value);
            }
        }

        return message;
    }
}
