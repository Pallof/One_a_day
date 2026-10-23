using System.Net.Mail;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The message as it goes over the wire, and what a retry sends. SmtpClient can write a
/// message to a folder instead of sending it, which gives the real MIME output with no
/// server involved.
/// </summary>
public sealed class SmtpMailerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("oad-mime-").FullName;

    private string WireFormat(OutgoingMail mail)
    {
        using var client = new SmtpClient
        {
            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
            PickupDirectoryLocation = _dir,
        };
        using var message = SmtpMailer.BuildMessage("sender@example.com", mail);
        client.Send(message);
        return File.ReadAllText(Directory.GetFiles(_dir).Single());
    }

    [Fact]
    public void A_styled_mail_goes_out_as_plain_text_first_then_html()
    {
        // Clients show the LAST part they can render, so the order is the whole point:
        // reversed, every HTML-capable client would show the plain text instead.
        var eml = WireFormat(new OutgoingMail("reader@example.com", "Subject", "plain words",
            Html: "<p>styled words</p>"));

        Assert.Contains("multipart/alternative", eml);
        var plain = eml.IndexOf("Content-Type: text/plain", StringComparison.Ordinal);
        var html = eml.IndexOf("Content-Type: text/html", StringComparison.Ordinal);
        Assert.True(plain >= 0 && html > plain, "expected text/plain before text/html");
    }

    [Fact]
    public void A_plain_mail_stays_a_single_plain_part()
    {
        // The author's own notifications set no Html and should look exactly as before.
        var eml = WireFormat(new OutgoingMail("author@example.com", "Subject", "plain words"));

        Assert.DoesNotContain("multipart", eml);
        Assert.DoesNotContain("text/html", eml);
    }

    [Fact]
    public void Unsubscribe_headers_survive_into_the_message()
    {
        var eml = WireFormat(new OutgoingMail("reader@example.com", "Subject", "plain",
            Headers: new Dictionary<string, string>
            {
                ["List-Unsubscribe"] = "<https://site/unsubscribe?token=abc>",
                ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
            },
            Html: "<p>styled</p>"));

        Assert.Contains("List-Unsubscribe: <https://site/unsubscribe?token=abc>", eml);
        Assert.Contains("List-Unsubscribe-Post: List-Unsubscribe=One-Click", eml);
    }

    [Fact]
    public void No_line_of_the_message_breaks_smtps_length_limit()
    {
        // The styled HTML has single lines thousands of characters long. Sent raw, a relay
        // may cut them mid-tag. The encoding has to fold them.
        var longLine = "<p style=\"" + new string('x', 5000) + "\">styled</p>";
        var eml = WireFormat(new OutgoingMail("reader@example.com", "Subject", "plain", Html: longLine));

        Assert.All(eml.Split("\r\n"), line => Assert.True(line.Length <= 998, $"line of {line.Length}"));
    }

    [Fact]
    public void The_message_key_becomes_the_message_id_on_the_senders_domain()
    {
        var eml = WireFormat(new OutgoingMail("reader@example.com", "Subject", "plain",
            MessageKey: "digest.2026-10-21.abc"));

        Assert.Contains("Message-ID: <digest.2026-10-21.abc@example.com>", eml);
    }

    [Fact]
    public async Task A_retry_after_a_timeout_sends_the_same_message_not_a_new_one()
    {
        // 2026-10-21: Gmail took the digest, its "OK" came after the timeout, and the retry
        // landed as a second copy. A retry with the same Message-ID is one Gmail shows once.
        var mailer = new StallsOnceMailer();

        var sent = await mailer.SendAsync(new OutgoingMail("reader@example.com", "Subject", "plain"), default);

        Assert.True(sent);
        Assert.Equal(2, mailer.MessageIds.Count);
        Assert.NotNull(mailer.MessageIds[0]);
        Assert.Equal(mailer.MessageIds[0], mailer.MessageIds[1]);
    }

    /// <summary>A server whose first reply never comes in time, as on 2026-10-21.</summary>
    private sealed class StallsOnceMailer() : SmtpMailer(
        Options.Create(new EmailOptions
        {
            Enabled = true, To = "a@x.com", From = "sender@example.com", Host = "h", AppPassword = "p",
        }),
        NullLogger<SmtpMailer>.Instance)
    {
        public List<string?> MessageIds { get; } = [];

        protected override Task DeliverAsync(MailMessage message, CancellationToken token)
        {
            MessageIds.Add(message.Headers["Message-ID"]);
            return MessageIds.Count == 1
                ? Task.FromException(new SmtpException("The operation has timed out."))
                : Task.CompletedTask;
        }
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
