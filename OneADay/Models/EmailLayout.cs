using System.Net;

namespace OneADay.Models;

/// <summary>
/// The HTML every subscriber email shares, drawn to look like the site: paper background,
/// the gold-circle wordmark, a serif masthead over a gold rule, white cards, green buttons.
/// </summary>
/// <remarks>
/// <para><b>Email HTML is its own dialect.</b> Gmail ignores CSS variables and web fonts,
/// and Outlook for Windows renders with Word. So every style is inline, layout is tables,
/// and the colours are literal copies of the app.css tokens — <b>keep them in step</b>
/// when the site's palette changes. Fonts fall back to Georgia (for Lora) and the system
/// sans (for Atkinson Hyperlegible); the webfonts would only ever load in Apple Mail.</para>
///
/// <para>Every value that isn't a fixed string goes through <see cref="E"/>. Teasers are
/// written by the author, not visitors, but an inbox is the wrong place to discover that a
/// question contained a stray &lt;.</para>
///
/// <para>Each email also carries a plain-text twin (see <see cref="SubscriptionMail"/>),
/// for clients that won't render HTML and for spam filters, which distrust HTML-only mail.</para>
/// </remarks>
public static class EmailLayout
{
    // app.css tokens, as literals.
    private const string Paper = "#FCFBF6";
    private const string CardBg = "#FFFFFF";
    private const string Line = "#E4E1D6";
    private const string Ink = "#22334D";
    private const string InkSoft = "#54627A";
    private const string BlueDeep = "#1E5E9C";
    private const string Gold = "#F0A500";
    private const string GoldTint = "#FDF3DA";
    private const string Amber = "#9A6700";
    private const string Green = "#2F855A";
    private const string GreenDeep = "#256B49";
    private const string GreenTint = "#E7F3EC";
    private const string Red = "#B03A2E";
    private const string RedTint = "#F9E9E7";

    private const string Serif = "Georgia,'Times New Roman',serif";
    private const string Sans = "'Segoe UI',-apple-system,BlinkMacSystemFont,Helvetica,Arial,sans-serif";

    /// <summary>
    /// Invisible padding after the preview text. Without it, an inbox fills the rest of
    /// the preview line with whatever comes next in the body — here, the wordmark.
    /// </summary>
    private static readonly string PreheaderFill = string.Concat(Enumerable.Repeat("&#847;&zwnj;&nbsp;", 40));

    /// <summary>HTML-encodes text for use in content or an attribute.</summary>
    public static string E(string text) => WebUtility.HtmlEncode(text);

    /// <summary>The whole document: wordmark header, the body, and a footer.</summary>
    /// <param name="preheader">The preview line an inbox shows beside the subject.</param>
    public static string Page(string title, string preheader, string siteUrl, string body, string footer) => $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="x-apple-disable-message-reformatting">
        <meta name="color-scheme" content="light">
        <meta name="supported-color-schemes" content="light">
        <title>{{E(title)}}</title>
        <style>
          a[x-apple-data-detectors] { color: inherit !important; text-decoration: none !important; }
          @media (max-width: 520px) {
            .oad-pad { padding-left: 18px !important; padding-right: 18px !important; }
            .oad-card { padding: 20px 20px 22px !important; }
            .oad-title { font-size: 28px !important; line-height: 34px !important; }
          }
        </style>
        </head>
        <body style="margin:0;padding:0;background-color:{{Paper}};">
        <div style="display:none;max-height:0;overflow:hidden;mso-hide:all;font-size:1px;line-height:1px;color:{{Paper}};opacity:0;">{{E(preheader)}}{{PreheaderFill}}</div>
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="{{Paper}}" style="background-color:{{Paper}};">
        <tr><td align="center">
        <table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" style="width:100%;max-width:600px;">
        <tr><td class="oad-pad" style="padding:22px 28px 16px;border-bottom:1px solid {{Line}};">
        <table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr>
        <td width="34" height="34" align="center" valign="middle" bgcolor="{{Gold}}" style="width:34px;height:34px;border-radius:17px;background-color:{{Gold}};font-size:17px;line-height:34px;text-align:center;">&#129504;</td>
        <td style="padding-left:10px;font-family:{{Serif}};font-size:23px;line-height:34px;font-weight:700;color:{{Ink}};"><a href="{{E(siteUrl)}}" style="color:{{Ink}};text-decoration:none;">One a Day</a></td>
        </tr></table>
        </td></tr>
        <tr><td class="oad-pad" style="padding:30px 28px 34px;">
        {{body}}
        </td></tr>
        <tr><td class="oad-pad" style="padding:22px 28px 36px;border-top:1px solid {{Line}};text-align:center;font-family:{{Sans}};font-size:14px;line-height:22px;color:{{InkSoft}};">
        {{footer}}
        </td></tr>
        </table>
        </td></tr>
        </table>
        </body>
        </html>
        """;

    /// <summary>The site's masthead: an optional dateline, the serif title, the gold rule.</summary>
    public static string Masthead(string? dateline, string title)
    {
        var date = dateline is null
            ? ""
            : $$"""<p style="margin:0 0 6px;text-align:center;font-family:{{Sans}};font-size:16px;line-height:24px;color:{{InkSoft}};">{{E(dateline)}}</p>""";

        return $$"""
            {{date}}
            <h1 class="oad-title" style="margin:0;text-align:center;font-family:{{Serif}};font-size:32px;line-height:38px;font-weight:700;letter-spacing:-0.3px;color:{{Ink}};">{{E(title)}}</h1>
            <table role="presentation" align="center" cellpadding="0" cellspacing="0" border="0" style="margin:14px auto 26px;"><tr><td width="64" height="4" bgcolor="{{Gold}}" style="width:64px;height:4px;font-size:0;line-height:0;background-color:{{Gold}};border-radius:2px;">&nbsp;</td></tr></table>
            """;
    }

    /// <summary>A white card, like <c>.oad-box</c>.</summary>
    public static string Card(string inner) => $$"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="{{CardBg}}" style="background-color:{{CardBg}};border:1px solid {{Line}};border-radius:14px;box-shadow:0 1px 2px rgba(34,51,77,0.06),0 4px 14px rgba(34,51,77,0.05);">
        <tr><td class="oad-card" style="padding:24px 28px 26px;">
        {{inner}}
        </td></tr>
        </table>
        """;

    /// <summary>The question card's heading row: "Question:" with the difficulty pill.</summary>
    public static string QuestionHead(Difficulty level) => $$"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>
        <td style="font-family:{{Sans}};font-size:23px;line-height:30px;font-weight:700;color:{{Ink}};">Question:</td>
        <td align="right" style="text-align:right;">{{Badge(level)}}</td>
        </tr></table>
        """;

    /// <summary>The tinted difficulty pill from <c>DifficultyBadge.razor</c>.</summary>
    public static string Badge(Difficulty level)
    {
        var (tint, text, dot) = level switch
        {
            Difficulty.Easy => (GreenTint, GreenDeep, Green),
            Difficulty.Hard => (RedTint, Red, Red),
            _ => (GoldTint, Amber, Gold),
        };
        return $$"""<span style="display:inline-block;padding:4px 12px;border-radius:999px;background-color:{{tint}};color:{{text}};font-family:{{Sans}};font-size:14px;line-height:20px;font-weight:700;white-space:nowrap;"><span style="color:{{dot}};font-size:12px;">&#9679;</span>&nbsp;{{level}}</span>""";
    }

    /// <summary>Body text at the size the site sets a question in.</summary>
    public static string Lead(string text, string margin = "0") =>
        $$"""<p style="margin:{{margin}};font-family:{{Sans}};font-size:19px;line-height:30px;color:{{Ink}};">{{E(text)}}</p>""";

    /// <summary>Secondary text in the soft ink.</summary>
    public static string Soft(string text, string margin = "12px 0 0") =>
        $$"""<p style="margin:{{margin}};font-family:{{Sans}};font-size:16px;line-height:25px;color:{{InkSoft}};">{{E(text)}}</p>""";

    /// <summary>A gold-tint note, like the site's locked-hint strip.</summary>
    public static string Callout(string text) =>
        $$"""<p style="margin:16px 0 0;padding:11px 15px;background-color:{{GoldTint}};border-radius:10px;font-family:{{Sans}};font-size:15px;line-height:22px;color:{{Amber}};">{{E(text)}}</p>""";

    /// <summary>The green primary button, like <c>.oad-btn-green</c>.</summary>
    public static string Button(string href, string label) => $$"""
        <table role="presentation" align="center" cellpadding="0" cellspacing="0" border="0" style="margin:0 auto;"><tr>
        <td align="center" bgcolor="{{Green}}" style="border-radius:10px;background-color:{{Green}};">
        <a href="{{E(href)}}" style="display:inline-block;padding:14px 30px;font-family:{{Sans}};font-size:17px;line-height:22px;font-weight:700;color:#FFFFFF;text-decoration:none;border-radius:10px;">{{E(label)}}</a>
        </td></tr></table>
        """;

    /// <summary>A bare link in the site's link colour.</summary>
    /// <param name="labelIsUrl">
    /// The label is the raw URL, so let it break anywhere rather than widen the email on a
    /// phone. Only then: on words it splits "Unsubscribe" into "Unsubscri-be".
    /// </param>
    public static string Link(string href, string label, string color = BlueDeep, bool labelIsUrl = false) =>
        $$"""<a href="{{E(href)}}" style="color:{{color}};text-decoration:underline;{{(labelIsUrl ? "word-break:break-all;" : "")}}">{{E(label)}}</a>""";

    /// <summary>Vertical space that survives Outlook, which ignores margins on most things.</summary>
    public static string Spacer(int px) =>
        $$"""<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr><td height="{{px}}" style="height:{{px}}px;font-size:0;line-height:0;">&nbsp;</td></tr></table>""";

    /// <summary>A footer line; <paramref name="html"/> must already be encoded.</summary>
    public static string FooterLine(string html, string margin = "0 0 6px") =>
        $$"""<p style="margin:{{margin}};font-family:{{Sans}};font-size:14px;line-height:22px;color:{{InkSoft}};">{{html}}</p>""";

    /// <summary>The site's own footer tagline.</summary>
    public static string Tagline => FooterLine("One a Day &#129504; — a small daily workout for your mind.");

    /// <summary>The soft ink, for links that sit in the footer.</summary>
    public static string FooterLink(string href, string label) => Link(href, label, InkSoft);
}
