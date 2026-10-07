using System.Text.Json;
using Microsoft.Extensions.Options;
using OneADay.Models;

namespace OneADay.Services;

/// <summary>
/// Emails the author last week's headline numbers every Monday from 7am Pacific, so they can
/// see them without downloading anything (PRD 16).
/// </summary>
/// <remarks>
/// <para><b>Polls once a minute</b>, like <see cref="DailyDigestService"/>, and for the same
/// reasons: no sleep to recompute across DST or restarts. The week is the Monday to Sunday just
/// ended. If the server was down on Monday morning, it goes out when the server is back, any
/// day that week — last week's numbers don't go stale the way a puzzle does.</para>
///
/// <para><b>At most once a week, across restarts and redeploys.</b> The last week sent is kept
/// in App_Data/weekly-summary.json, on the volume that survives a deploy (PRD 11). A failed
/// send records nothing and is tried again an hour later, not every minute.</para>
///
/// <para><b>Sent directly, not through <see cref="EmailNotifier"/></b>, like the traffic
/// alert: that queue's 25-a-day cap is shared with uncapped issue reports (PRD 14).</para>
///
/// <para><b>Off on the author's Mac.</b> Development is where admin shows these numbers, and
/// the Mac holds the real Gmail password: left on, it would mail its own copy of the week.</para>
/// </remarks>
public sealed class WeeklySummaryService(
    MetricsStore metrics,
    RotationStore rotation,
    TeaserStore teasers,
    SmtpMailer mailer,
    IOptions<EmailOptions> email,
    IOptions<SiteOptions> site,
    IWebHostEnvironment env,
    ILogger<WeeklySummaryService> log,
    Func<DateTime>? nowPacific = null) : BackgroundService
{
    public const int SendHourPacific = 7;
    public const string FileName = "weekly-summary.json";

    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromHours(1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Func<DateTime> _now = nowPacific ?? (() => AppTime.NowPacific);
    private readonly string _filePath = Path.Combine(env.ContentRootPath, "App_Data", FileName);
    private DateOnly? _lastWeekSent;
    private bool _loaded;
    private DateTime? _retryAfter;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (env.IsDevelopment())
        {
            log.LogInformation("Weekly summary is off on this machine; admin shows the numbers here.");
            return;
        }
        if (!mailer.IsConfigured)
        {
            log.LogInformation("Weekly summary is off (email not configured).");
            return;
        }

        log.LogInformation("Weekly summary on; sends Mondays from {Hour}:00 Pacific.", SendHourPacific);

        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await RunOnceAsync(_now(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // One bad tick must not end the service, or take the site down with it — an
                // exception escaping a background service stops the whole app.
                log.LogError(ex, "Weekly summary tick failed; will retry.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// One pass: if last week's summary is due and hasn't gone out, build it and send it.
    /// Public so the schedule can be tested without waiting for Monday.
    /// </summary>
    /// <returns>Whether an email went out.</returns>
    public async Task<bool> RunOnceAsync(DateTime nowPacific, CancellationToken token)
    {
        if (!mailer.IsConfigured)
        {
            return false;
        }

        var today = DateOnly.FromDateTime(nowPacific);
        var thisMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        if (today == thisMonday && nowPacific.Hour < SendHourPacific)
        {
            return false;   // last week's isn't due until 7, and the week before's chance has passed
        }

        var weekFrom = thisMonday.AddDays(-7);
        if (LastWeekSent() >= weekFrom || nowPacific < _retryAfter)
        {
            return false;
        }

        var report = MetricsReport.Build(
            metrics.Read(),
            rotation.Snapshot(take: int.MaxValue).Recent,
            teasers.GetAll(),
            today: weekFrom.AddDays(6),
            MetricsRange.LastWeek);
        var mail = WeeklySummaryMail.Build(report, site.Value.Link(""));

        if (!await mailer.SendAsync(new OutgoingMail(email.Value.To, mail.Subject, mail.Body, Html: mail.Html), token))
        {
            _retryAfter = nowPacific + RetryAfterFailure;
            log.LogWarning("Weekly summary for the week from {From} wasn't sent; trying again after {Retry:HH:mm}.",
                weekFrom, _retryAfter);
            return false;
        }

        _retryAfter = null;
        RecordSent(weekFrom);
        log.LogInformation("Weekly summary sent for the week from {From}.", weekFrom);
        return true;
    }

    private DateOnly? LastWeekSent()
    {
        if (_loaded)
        {
            return _lastWeekSent;
        }
        _loaded = true;
        try
        {
            if (File.Exists(_filePath))
            {
                _lastWeekSent = JsonSerializer.Deserialize<SentRecord>(File.ReadAllText(_filePath), JsonOptions)?.LastWeekSent;
            }
        }
        catch (Exception ex)
        {
            // Unreadable reads as never sent: at worst one repeat, rather than no summary
            // ever again until someone fixes the file by hand.
            log.LogWarning(ex, "{File} couldn't be read; treating last week as not sent.", FileName);
        }
        return _lastWeekSent;
    }

    private void RecordSent(DateOnly weekFrom)
    {
        // Memory first: if the file can't be written, this server still won't send the week
        // again every minute — only a restart could repeat it.
        _lastWeekSent = weekFrom;
        try
        {
            AtomicFile.WriteAllText(_filePath, JsonSerializer.Serialize(new SentRecord(weekFrom), JsonOptions));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Couldn't save {File}; a restart may send this week's summary again.", FileName);
        }
    }

    /// <param name="LastWeekSent">The Monday that starts the last week summarised.</param>
    private sealed record SentRecord(DateOnly LastWeekSent);
}
