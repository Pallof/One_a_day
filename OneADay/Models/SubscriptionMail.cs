using System.Globalization;
using static OneADay.Models.EmailLayout;

namespace OneADay.Models;

/// <summary>Subject and bodies of one subscription email, before it's addressed.</summary>
/// <param name="Body">The plain-text version — sent alongside, never instead.</param>
/// <param name="Html">The styled version, drawn by <see cref="EmailLayout"/>.</param>
public sealed record MailContent(string Subject, string Body, string Html);

/// <summary>Where the digest's links point, already absolute.</summary>
public sealed record DigestLinks(string Solve, string Unsubscribe);

/// <summary>
/// The wording of every email a subscriber receives, kept pure so it can be tested
/// without SMTP.
///
/// <para>Each email goes out as HTML styled like the site, with a plain-text twin for
/// clients that won't render it. Both say the same thing, and neither ever includes an
/// answer or a hint: the digest sends the question and a link, and solving happens on the
/// site, where the spoiler rules live.</para>
/// </summary>
public static class SubscriptionMail
{
    public static MailContent Confirmation(string confirmUrl, string siteUrl)
    {
        const string subject = "Stumpty — please confirm your email";

        var text = $"""
            Thanks for signing up for Stumpty — one brain teaser, every morning.

            Please confirm this is your address by opening this link:

            {confirmUrl}

            Nothing will be sent until you do. If you didn't sign up, ignore this email and
            you won't hear from us again.
            """;

        var body =
            Masthead(dateline: null, "Confirm your email") +
            Card(
                Lead("Thanks for signing up for Stumpty — one brain teaser, every morning.") +
                Soft("Confirm this is your address and your first challenge arrives at 7am Pacific. " +
                     "Nothing is sent until you do.") +
                Spacer(24) +
                Button(confirmUrl, "Confirm my email") +
                Spacer(22) +
                FooterLine($"Button not working? Paste this into your browser:<br>{Link(confirmUrl, confirmUrl, labelIsUrl: true)}", margin: "0"));

        var footer =
            FooterLine("Didn&#39;t sign up? Ignore this email and you won&#39;t hear from us again.") +
            Tagline;

        return new MailContent(subject, text, Page(subject,
            preheader: "One click and the daily challenge starts arriving at 7am Pacific.",
            siteUrl, body, footer));
    }

    /// <summary>
    /// The daily challenge.
    /// </summary>
    /// <param name="hasImage">
    /// Neither version carries the teaser's picture, so the reader is told there is one
    /// rather than being handed a question that makes no sense without it.
    /// </param>
    public static MailContent Digest(
        DateOnly day, Difficulty difficulty, string question, bool hasImage, DigestLinks links)
    {
        var date = day.ToString("dddd, MMMM d", CultureInfo.InvariantCulture);
        var subject = $"Stumpty — {date} · {difficulty}";
        const string pictureNote = "This one comes with a picture — open it on the site to see it.";

        var text = $"""
            Challenge of the day · {date} · {difficulty}

            {question}
            {(hasImage ? $"\n({pictureNote})\n" : "")}
            Solve today's challenge: {links.Solve}

            —
            You're receiving this because you subscribed to Stumpty.
            Unsubscribe in one click: {links.Unsubscribe}
            """;

        var body =
            Masthead(date, "Challenge of the day") +
            Card(
                QuestionHead(difficulty) +
                Lead(question, margin: "14px 0 0") +
                (hasImage ? Callout("🖼️ " + pictureNote) : "")) +
            Spacer(26) +
            Button(links.Solve, "Solve today's challenge");

        var footer =
            Tagline +
            FooterLine($"You&#39;re getting this because you subscribed. {FooterLink(links.Unsubscribe, "Unsubscribe")} in one click.",
                margin: "0");

        return new MailContent(subject, text, Page(subject,
            preheader: $"{difficulty} · {Preview(question)}",
            links.Solve, body, footer));
    }

    /// <summary>The start of the question, cut at a word, for the inbox preview line.</summary>
    private static string Preview(string question, int max = 110)
    {
        var flat = string.Join(' ', question.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (flat.Length <= max)
        {
            return flat;
        }
        var cut = flat.LastIndexOf(' ', max);
        return flat[..(cut > 0 ? cut : max)] + "…";
    }
}
