using System.Net.Mail;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The message as it goes over the wire. SmtpClient can write a message to a folder
/// instead of sending it, which gives the real MIME output with no server involved.
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

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
