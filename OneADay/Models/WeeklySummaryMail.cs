using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OneADay.Services;

namespace OneADay.Models;

/// <summary>
/// The Monday summary's wording (PRD 16): last week's headline numbers, then each puzzle.
/// </summary>
/// <remarks>
/// <para>Built from a <see cref="MetricsReport"/> alone, which holds daily totals and puzzle
/// questions — no answers, no addresses, nothing about any one visitor — so none of those
/// can reach the email.</para>
///
/// <para>Unlike the author's other notifications it goes out styled, like the subscriber
/// email, because it's a page of numbers and a wall of plain text buries them
/// <i>(author's decision, 2026-10-07)</i>. The plain text below is its twin, for clients that
/// won't show HTML and for spam filters, which distrust HTML-only mail. The styled version is
/// in WeeklySummaryMail.Html.cs.</para>
/// </remarks>
public static partial class WeeklySummaryMail
{
    private static readonly CultureInfo C = CultureInfo.InvariantCulture;

    /// <param name="siteUrl">Where the wordmark links.</param>
    public static MailContent Build(MetricsReport report, string siteUrl)
    {
        var subject = $"Stumpty — the week in numbers, {Short(report.From)} – {Short(report.To)}";
        return new MailContent(subject, PlainText(report), Html(report, subject, siteUrl));
    }

    private static string PlainText(MetricsReport report)
    {
        var body = new StringBuilder();
        body.AppendLine($"Stumpty's week, {Long(report.From)} to {Long(report.To)}.");
        body.AppendLine();

        body.AppendLine("VISITORS");
        if (report.VisitorDays == 0)
        {
            body.AppendLine("  No visitors were counted this week.");
        }
        else
        {
            body.AppendLine($"  Daily visitors, added up: {report.VisitorDays.ToString("N0", C)}");
            body.AppendLine($"  By day: {ByDay(report)}");
            body.AppendLine($"  Back the next day: {ShareOf(report.NextDayReturn)}");
            body.AppendLine($"  Had visited in the week before: {ShareOf(report.WithinWeek)}");
        }
        body.AppendLine();

        body.AppendLine("ROTATION");
        body.AppendLine($"  Recycled days: {report.RecycledDays.Part} of {report.RecycledDays.Whole}");
        body.AppendLine();

        body.AppendLine("DAILY EMAIL");
        body.AppendLine(report.Subscribers is { } list
            ? $"  Confirmed: {list.Confirmed} · waiting to confirm: {list.Pending} · as of {Short(list.Day)}"
            : "  The list's size hasn't been recorded yet.");
        body.AppendLine($"  Confirmed this week: {report.Confirmations} · unsubscribed: {report.Unsubscribes}");
        body.AppendLine();

        body.AppendLine("TWENTY FOUR");
        body.AppendLine($"  Players, per day, added up: {report.TwentyFourPlayers}");
        body.AppendLine($"  Hands solved: {report.HandsSolved} · passed: {report.HandsPassed}");
        body.AppendLine();

        body.AppendLine("EACH PUZZLE, MOST RECENT FIRST");
        AppendPuzzles(body, report);

        body.AppendLine($"""
            Visitors are counted once per browser per day, so a phone and a laptop are two.
            Percentages from fewer than {Share.TooFewBelow} browsers are marked "too few to judge":
            at that size, one browser moves them by five points.

            This comes every Monday from 7am Pacific. For the full breakdown, run
            sh deploy/pull-live-data.sh and open admin.
            """);

        return body.ToString();
    }

    private static void AppendPuzzles(StringBuilder body, MetricsReport report)
    {
        var ran = report.Fairness.ToDictionary(r => r.TeaserId);

        foreach (var row in report.Funnel)
        {
            body.AppendLine();
            body.AppendLine($"  {Quoted(row.Question)}");
            if (ran.TryGetValue(row.TeaserId, out var rotation) && rotation.DaysShown > 0)
            {
                body.AppendLine($"    {Ran(rotation)} · share of viewers {ShareOf(rotation.OfViewers)}");
            }
            body.AppendLine($"    Saw {row.Saw} → tried {row.Tried} → solved {row.Solved}");
            body.AppendLine($"    Solve rate: {ShareOf(row.SolveRate)}");
            if (row.AverageAttempts is { } average)
            {
                body.AppendLine($"    Attempts: {Spread(row.SolvedIn)} · average {average.ToString("0.0", C)}" +
                                TooFew(row.Solved));
            }
        }

        var unopened = Unopened(report);
        foreach (var row in unopened)
        {
            body.AppendLine();
            body.AppendLine($"  {Quoted(row.Question)}");
            body.AppendLine($"    {Ran(row)} · nobody opened it");
        }

        if (report.Funnel.Count == 0 && unopened.Count == 0)
        {
            body.AppendLine("  No puzzle ran this week.");
        }
        body.AppendLine();
    }

    /// <summary>
    /// Puzzles that ran on a day nobody opened them. They have no counts, so they aren't in the
    /// funnel; listed anyway, because a week's puzzles shouldn't vanish from the summary.
    /// </summary>
    private static List<FairnessRow> Unopened(MetricsReport report)
    {
        var counted = report.Funnel.Select(f => f.TeaserId).ToHashSet();
        return report.Fairness.Where(r => r.DaysShown > 0 && !counted.Contains(r.TeaserId)).ToList();
    }

    /// <summary>"58% · 7 of 12", marked when the count is too small to mean much (PRD 16, requirement 17).</summary>
    public static string ShareOf(Share share) =>
        share.Percent is { } percent
            ? $"{percent.ToString("0", C)}% · {share.Part} of {share.Whole}{TooFew(share.Whole)}"
            : "—";

    /// <summary>The one rule both versions use: below 20 browsers, a figure is too few to judge.</summary>
    private static bool IsTooFew(int browsers) => browsers < Share.TooFewBelow;

    private static string TooFew(int browsers) => IsTooFew(browsers) ? " (too few to judge)" : "";

    private static string Ran(FairnessRow row) =>
        $"Ran {row.DaysShown} {Wording.Plural(row.DaysShown, "day")}" +
        (row.RecycledDays > 0 ? $", {row.RecycledDays} recycled" : "");

    /// <summary>Every day of the week in order, with a zero where nothing was counted.</summary>
    private static string ByDay(MetricsReport report)
    {
        var byDay = report.Habit.ToDictionary(h => h.Day, h => h.Visitors.Total);
        var days = new List<string>();
        for (var day = report.From; day <= report.To; day = day.AddDays(1))
        {
            days.Add($"{day.ToString("ddd", C)} {byDay.GetValueOrDefault(day)}");
        }
        return string.Join(" · ", days);
    }

    private static string Spread(IReadOnlyList<int> solvedIn)
    {
        var words = new[] { "one", "two", "three", "four", "five or more" };
        return string.Join(", ", solvedIn.Select((count, i) => $"{count} in {words[i]}"));
    }

    /// <summary>One line, and short enough to scan, like admin's tables.</summary>
    private static string Quoted(string question) => $"\"{Flat(question, 70)}\"";

    /// <summary>The question on one line, cut at <paramref name="max"/> characters.</summary>
    private static string Flat(string question, int max)
    {
        var line = Whitespace().Replace(question, " ").Trim();
        return line.Length <= max ? line : line[..max] + "…";
    }

    private static string Short(DateOnly day) => day.ToString("d MMM", C);

    private static string Long(DateOnly day) => day.ToString("dddd d MMMM", C);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
