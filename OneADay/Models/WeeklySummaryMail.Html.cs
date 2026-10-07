using System.Text;
using static OneADay.Models.EmailLayout;

namespace OneADay.Models;

/// <summary>
/// The Monday summary's styled version: the subscriber email's look (<see cref="EmailLayout"/>),
/// laid out as headline tiles, a bar per day, then a card per puzzle.
/// </summary>
/// <remarks>
/// <para><b>Bars are table cells and sized divs</b>, which Gmail and Apple Mail draw. Outlook for
/// Windows may square them off, so every bar also prints its number: nothing depends on a bar
/// rendering.</para>
///
/// <para>Every value that isn't a fixed string goes through <see cref="EmailLayout.E"/>. The
/// questions are the author's own, but an inbox is the wrong place to find a stray &lt;.</para>
/// </remarks>
public static partial class WeeklySummaryMail
{
    private const int DayBarHeight = 70;
    private const int AttemptBarHeight = 36;

    private static string Html(MetricsReport report, string subject, string siteUrl)
    {
        var body =
            Masthead($"{Long(report.From)} to {Long(report.To)}", "The week in numbers") +
            Card(Headlines(report) + Spacer(26) + VisitorsByDay(report)) +
            Spacer(16) +
            Card(Facts(report)) +
            Spacer(34) +
            SectionHeading("Each puzzle, most recent first") +
            PuzzleCards(report);

        var footer =
            FooterLine("Visitors are counted once per browser per day, so a phone and a laptop are two.") +
            FooterLine($"Figures from fewer than {Share.TooFewBelow} browsers are tagged &ldquo;too few to judge&rdquo;: " +
                       "at that size, one browser moves them by five points.") +
            FooterLine("Sent every Monday from 7am Pacific. For the full breakdown, run " +
                       $"""<code style="font-family:Menlo,Consolas,monospace;font-size:13px;">sh deploy/pull-live-data.sh</code>""" +
                       " and open admin.", margin: "0");

        return Page(subject, Preheader(report), siteUrl, body, footer);
    }

    /// <summary>The inbox's preview line: counts only, so no percentage goes without its count.</summary>
    private static string Preheader(MetricsReport report)
    {
        var puzzles = report.Funnel.Count + Unopened(report).Count;
        var parts = new List<string>
        {
            $"{report.VisitorDays.ToString("N0", C)} {Wording.Plural(report.VisitorDays, "visitor")}",
            $"{puzzles} {Wording.Plural(puzzles, "puzzle")}",
        };
        if (report.Subscribers is { } list)
        {
            parts.Add($"{list.Confirmed} on the list");
        }
        return string.Join(" · ", parts);
    }

    // ---- the headline card ---------------------------------------------------------

    /// <summary>
    /// Three tiles. The labels sit in a row of their own, aligned to the bottom, so a label
    /// that wraps can't push its number out of line with the others.
    /// </summary>
    private static string Headlines(MetricsReport report)
    {
        Tile[] tiles =
        [
            new("Visitors", report.VisitorDays.ToString("N0", C), "daily, added up", TooFew: false),
            Tile.Of("Back the next day", report.NextDayReturn),
            Tile.Of("Had visited in the week before", report.WithinWeek),
        ];

        return $$"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
            <tr>{{string.Concat(tiles.Select(TileLabel))}}</tr>
            <tr>{{string.Concat(tiles.Select(TileFigure))}}</tr>
            </table>
            """;
    }

    private sealed record Tile(string Label, string Big, string Count, bool TooFew)
    {
        public static Tile Of(string label, Share share) =>
            share.Percent is { } percent
                ? new(label, $"{percent.ToString("0", C)}%", $"{share.Part} of {share.Whole}", IsTooFew(share.Whole))
                : new(label, "—", "none yet", TooFew: false);
    }

    private static string TileLabel(Tile tile) => $$"""
        <td width="33%" valign="bottom" align="center" style="width:33%;padding:0 4px;text-align:center;font-family:{{Sans}};font-size:13px;line-height:18px;color:{{InkSoft}};">{{E(tile.Label)}}</td>
        """;

    private static string TileFigure(Tile tile) => $$"""
        <td width="33%" valign="top" align="center" style="width:33%;padding:0 4px;text-align:center;">
        <div style="padding:4px 0 2px;font-family:{{Serif}};font-size:34px;line-height:40px;font-weight:700;color:{{Ink}};">{{E(tile.Big)}}</div>
        <div style="font-family:{{Sans}};font-size:13px;line-height:18px;color:{{InkSoft}};">{{E(tile.Count)}}</div>
        {{(tile.TooFew ? $"""<div style="padding-top:5px;">{FewTag}</div>""" : "")}}
        </td>
        """;

    private static string VisitorsByDay(MetricsReport report)
    {
        var byDay = report.Habit.ToDictionary(h => h.Day, h => h.Visitors.Total);
        var days = new List<(DateOnly Day, int Visitors)>();
        for (var day = report.From; day <= report.To; day = day.AddDays(1))
        {
            days.Add((day, byDay.GetValueOrDefault(day)));
        }
        var most = Math.Max(1, days.Max(d => d.Visitors));

        var bars = string.Concat(days.Select(d => $$"""
            <td width="14%" valign="bottom" align="center" style="width:14%;padding:0 4px;">
            <div style="padding-bottom:3px;font-family:{{Sans}};font-size:13px;line-height:18px;font-weight:700;color:{{Ink}};">{{d.Visitors}}</div>
            {{Bar(d.Visitors, most, DayBarHeight, Gold)}}
            </td>
            """));
        var labels = string.Concat(days.Select(d => $$"""
            <td align="center" style="padding:6px 0 0;border-top:1px solid {{Line}};font-family:{{Sans}};font-size:12px;line-height:16px;color:{{InkSoft}};">{{d.Day.ToString("ddd", C)}}</td>
            """));

        return $$"""
            <p style="margin:0 0 10px;font-family:{{Sans}};font-size:13px;line-height:18px;color:{{InkSoft}};">Visitors by day</p>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
            <tr>{{bars}}</tr>
            <tr>{{labels}}</tr>
            </table>
            """;
    }

    // ---- the facts card ------------------------------------------------------------

    private static string Facts(MetricsReport report)
    {
        string[] week = [$"{report.Confirmations} confirmed this week", $"{report.Unsubscribes} unsubscribed"];
        var list = report.Subscribers is { } size
            ? Fact("Daily email", $"{size.Confirmed} confirmed", [$"{size.Pending} waiting, as of {Short(size.Day)}", .. week])
            : Fact("Daily email", "—", ["size not recorded yet", .. week]);

        return $$"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>
            {{Fact("Rotation", $"{report.RecycledDays.Part} of {report.RecycledDays.Whole}", "days recycled")}}
            {{list}}
            {{Fact("Twenty Four", $"{report.TwentyFourPlayers} players", "per day, added up",
                   $"{report.HandsSolved} hands solved", $"{report.HandsPassed} passed")}}
            </tr></table>
            """;
    }

    private static string Fact(string label, string value, params string[] notes) => $$"""
        <td width="33%" valign="top" style="width:33%;padding:0 10px 0 0;">
        <div style="font-family:{{Sans}};font-size:13px;line-height:18px;color:{{InkSoft}};">{{E(label)}}</div>
        <div style="padding:3px 0 3px;font-family:{{Sans}};font-size:19px;line-height:25px;font-weight:700;color:{{Ink}};">{{E(value)}}</div>
        {{string.Concat(notes.Select(note => $$"""<div style="font-family:{{Sans}};font-size:13px;line-height:18px;color:{{InkSoft}};">{{E(note)}}</div>"""))}}
        </td>
        """;

    // ---- a card per puzzle ---------------------------------------------------------

    private static string PuzzleCards(MetricsReport report)
    {
        var ran = report.Fairness.ToDictionary(r => r.TeaserId);
        var cards = new List<string>();

        foreach (var row in report.Funnel)
        {
            cards.Add(Card(
                PuzzleHead(ran.GetValueOrDefault(row.TeaserId)) +
                Lead(Flat(row.Question, 160), margin: "6px 0 16px") +
                Funnel(row) +
                SolveRate(row) +
                Attempts(row)));
        }
        foreach (var row in Unopened(report))
        {
            cards.Add(Card(
                PuzzleHead(row) +
                Lead(Flat(row.Question, 160), margin: "6px 0 0") +
                Soft("Nobody opened it.")));
        }

        return cards.Count == 0
            ? Card(Soft("No puzzle ran this week.", margin: "0"))
            : string.Join(Spacer(14), cards);
    }

    /// <summary>When it ran and its share of viewers, with a pill if it was a recycled draw.</summary>
    private static string PuzzleHead(FairnessRow? rotation)
    {
        if (rotation is not { DaysShown: > 0, LastShown: { } last })
        {
            return "";
        }

        var when = rotation.DaysShown == 1
            ? last.ToString("ddd d MMM", C)
            : $"{rotation.DaysShown} days, last {last.ToString("ddd d MMM", C)}";
        var viewers = rotation.OfViewers.Percent is { } percent
            ? $" · viewers {percent.ToString("0", C)}% · {rotation.OfViewers.Part} of {rotation.OfViewers.Whole}"
            : "";
        var recycled = rotation.RecycledDays == 0 ? ""
            : rotation.RecycledDays == rotation.DaysShown ? Pill("Recycled")
            : Pill($"{rotation.RecycledDays} of {rotation.DaysShown} recycled");

        return $$"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>
            <td style="font-family:{{Sans}};font-size:13px;line-height:20px;color:{{InkSoft}};">{{E(when + viewers)}}{{Few(IsTooFew(rotation.OfViewers.Whole) && rotation.OfViewers.Percent is not null)}}</td>
            <td align="right" style="text-align:right;">{{recycled}}</td>
            </tr></table>
            """;
    }

    /// <summary>Saw → tried → solved as three bars, each scaled to the widest.</summary>
    private static string Funnel(FunnelRow row)
    {
        var most = Math.Max(1, Math.Max(row.Saw, Math.Max(row.Tried, row.Solved)));
        return $$"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
            {{FunnelStep("Saw", row.Saw, most, BlueDeep)}}
            {{FunnelStep("Tried", row.Tried, most, Gold)}}
            {{FunnelStep("Solved", row.Solved, most, Green)}}
            </table>
            """;
    }

    private static string FunnelStep(string label, int count, int most, string color)
    {
        var width = (int)Math.Round(100.0 * count / most);
        var fill = width == 0 ? "" : $$"""
            <td width="{{width}}%" height="12" bgcolor="{{color}}" style="width:{{width}}%;height:12px;background-color:{{color}};border-radius:6px;font-size:0;line-height:0;">&nbsp;</td>
            """;
        var rest = width >= 100 ? "" : """<td style="font-size:0;line-height:0;">&nbsp;</td>""";

        return $$"""
            <tr>
            <td width="56" style="width:56px;padding:4px 0;font-family:{{Sans}};font-size:14px;line-height:18px;color:{{InkSoft}};">{{label}}</td>
            <td style="padding:4px 10px 4px 0;"><table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>{{fill}}{{rest}}</tr></table></td>
            <td width="36" align="right" style="width:36px;padding:4px 0;text-align:right;font-family:{{Sans}};font-size:14px;line-height:18px;font-weight:700;color:{{Ink}};">{{count}}</td>
            </tr>
            """;
    }

    private static string SolveRate(FunnelRow row)
    {
        var rate = row.SolveRate.Percent is { } percent
            ? $"""<strong style="color:{Ink};">{percent.ToString("0", C)}%</strong> · {row.SolveRate.Part} of {row.SolveRate.Whole}{Few(IsTooFew(row.SolveRate.Whole))}"""
            : "— nobody tried it";
        return $$"""<p style="margin:12px 0 0;font-family:{{Sans}};font-size:15px;line-height:24px;color:{{InkSoft}};">Solve rate {{rate}}</p>""";
    }

    /// <summary>Solves by attempt count as five small bars, and the average beside them.</summary>
    private static string Attempts(FunnelRow row)
    {
        if (row.AverageAttempts is not { } average)
        {
            return "";
        }

        var labels = new[] { "1", "2", "3", "4", "5+" };
        var most = Math.Max(1, row.SolvedIn.Max());
        var bars = string.Concat(row.SolvedIn.Select(count => $$"""
            <td width="34" valign="bottom" align="center" style="width:34px;padding:0 3px;">
            <div style="padding-bottom:2px;font-family:{{Sans}};font-size:12px;line-height:16px;color:{{Ink}};">{{count}}</div>
            {{Bar(count, most, AttemptBarHeight, Green)}}
            </td>
            """));
        var axis = string.Concat(labels.Select(label => $$"""
            <td align="center" style="padding:4px 0 0;border-top:1px solid {{Line}};font-family:{{Sans}};font-size:12px;line-height:16px;color:{{InkSoft}};">{{label}}</td>
            """));

        return $$"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-top:14px;"><tr>
            <td valign="bottom">
            <div style="padding-bottom:6px;font-family:{{Sans}};font-size:13px;line-height:18px;color:{{InkSoft}};">Attempts to solve</div>
            <table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr>{{bars}}</tr><tr>{{axis}}</tr></table>
            </td>
            <td valign="bottom" align="right" style="text-align:right;">
            <div style="font-family:{{Sans}};font-size:13px;line-height:18px;color:{{InkSoft}};">average</div>
            <div style="font-family:{{Serif}};font-size:26px;line-height:32px;font-weight:700;color:{{Ink}};">{{average.ToString("0.0", C)}}</div>
            {{(IsTooFew(row.Solved) ? $"""<div style="padding-top:4px;">{FewTag}</div>""" : "")}}
            </td>
            </tr></table>
            """;
    }

    // ---- small pieces --------------------------------------------------------------

    private static string SectionHeading(string text) =>
        $$"""<h2 style="margin:0 0 14px;font-family:{{Serif}};font-size:22px;line-height:28px;font-weight:700;color:{{Ink}};">{{E(text)}}</h2>""";

    /// <summary>A bar scaled to the tallest; a hairline for zero, so the column still shows.</summary>
    private static string Bar(int count, int most, int maxHeight, string color)
    {
        var height = count == 0 ? 2 : Math.Max(4, (int)Math.Round((double)maxHeight * count / most));
        return $$"""<div style="height:{{height}}px;background-color:{{(count == 0 ? Line : color)}};border-radius:3px 3px 0 0;font-size:0;line-height:0;">&nbsp;</div>""";
    }

    /// <summary>Requirement 17's mark, as a small gold tag rather than words in the sentence.</summary>
    private static string FewTag =>
        $$"""<span style="display:inline-block;padding:1px 8px;border-radius:999px;background-color:{{GoldTint}};color:{{Amber}};font-family:{{Sans}};font-size:12px;line-height:18px;white-space:nowrap;">too few to judge</span>""";

    private static string Few(bool tooFew) => tooFew ? "&nbsp;" + FewTag : "";

    private static string Pill(string text) =>
        $$"""<span style="display:inline-block;padding:1px 9px;border-radius:999px;border:1px solid {{Line}};background-color:{{Paper}};color:{{InkSoft}};font-family:{{Sans}};font-size:12px;line-height:18px;white-space:nowrap;">{{E(text)}}</span>""";
}
