using System.Globalization;
using Microsoft.Extensions.Options;

namespace OneADay.Services;

/// <summary>
/// Emails the author when a day's traffic passes the alert line — at most once a day.
/// </summary>
/// <remarks>
/// <para><b>Polls once a minute</b>, like <see cref="DailyDigestService"/>, so counting stays
/// a single increment per request and the alert arrives within a minute of the line being
/// crossed.</para>
///
/// <para><b>Sent directly, not through <see cref="EmailNotifier"/>.</b> That queue's 25-a-day
/// cap is shared with issue reports, which are uncapped by design (PRD 06), so a bot filing
/// reports could spend the day's allowance first — and the one email that matters during an
/// attack would be the one dropped. At most once a day, this can't flood the inbox itself.</para>
///
/// <para>Logged as a warning whether or not email is set up, so the host's logs carry it
/// either way. The once-a-day record lives in memory, so a restart can send one more.</para>
/// </remarks>
public sealed class TrafficAlertService(
    TrafficMonitor monitor,
    SmtpMailer mailer,
    IOptions<EmailOptions> email,
    ILogger<TrafficAlertService> log) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!monitor.IsOn)
        {
            log.LogInformation("Traffic alerts are off ({Section}:DailyThreshold is zero).",
                TrafficAlertOptions.Section);
            return;
        }

        log.LogInformation("Traffic alerts on; the author hears when a day passes {Threshold:N0} requests.",
            monitor.Threshold);

        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await RunOnceAsync(AppTime.NowPacific, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // One bad tick must not end the watch for good. Log it and look again next minute.
                log.LogError(ex, "Traffic alert tick failed; will retry.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// One pass: if today has just passed the alert line, log it and email the author.
    /// Public so the rules can be tested without waiting a minute.
    /// </summary>
    /// <returns>Whether an email went out.</returns>
    public async Task<bool> RunOnceAsync(DateTime nowPacific, CancellationToken token)
    {
        if (monitor.Check(nowPacific) is not { } report)
        {
            return false;
        }

        log.LogWarning(
            "Unusual traffic: {Total:N0} requests today — {Served:N0} served, {TurnedAway:N0} " +
            "turned away by the Cloudflare lock. The alert line is {Threshold:N0}.",
            report.Total, report.Served, report.TurnedAway, report.Threshold);

        if (!mailer.IsConfigured)
        {
            return false;
        }

        var mail = TrafficAlertMail.Build(report);
        return await mailer.SendAsync(new OutgoingMail(email.Value.To, mail.Subject, mail.Body), token);
    }
}

/// <summary>The alert's wording. Plain text, like the author's other notifications.</summary>
public static class TrafficAlertMail
{
    public static Notification Build(TrafficReport report)
    {
        var c = CultureInfo.InvariantCulture;
        var subject = string.Create(c, $"Stumpty — unusual traffic: {report.Total:N0} requests today");

        var yesterday = report.Yesterday is { } total
            ? string.Create(c, $"Yesterday's total was {total:N0}.")
            : "Yesterday's total isn't known, because the server restarted since.";

        var meaning = report.TurnedAway > report.Served
            ? "Most of it was turned away by the Cloudflare lock, so someone is using the server's own " +
              "Fly address. The lock answers each request with 99 bytes, so even nonstop that costs " +
              "about $2 a month. A Cloudflare Tunnel would remove that address altogether."
            : "Most of it was served, so it came through Cloudflare. If it's a bot, Cloudflare's " +
              "Security > Events page shows which addresses; block them there, or switch on " +
              "\"I'm Under Attack\" mode. If it's real visitors, nothing is wrong: raise the alert " +
              "line (TrafficAlert__DailyThreshold) if days like this become normal.";

        var body = string.Create(c, $"""
            Stumpty's server has had {report.Total:N0} requests today, passing the alert line of {report.Threshold:N0}.
            {yesterday}

              Served:                              {report.Served,12:N0}
              Turned away by the Cloudflare lock:  {report.TurnedAway,12:N0}

            As of {report.AtPacific.ToString("h:mmtt", c).ToLowerInvariant()} Pacific on {report.AtPacific.ToString("dddd d MMMM", c)}.

            {meaning}

            To stop all charges at once, stop the machine from the Fly dashboard. The site stays
            offline until you start it again. For scale: at the server's limit, a flood through
            Cloudflare costs about $4 a day.

            You'll get this at most once a day.
            """);

        return new Notification(subject, body);
    }
}
