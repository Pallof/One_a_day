using Microsoft.Extensions.Options;
using OneADay.Models;

namespace OneADay.Services;

/// <summary>
/// Sends each confirmed subscriber the day's challenge, once, after the send hour.
/// </summary>
/// <remarks>
/// <para><b>Polls once a minute rather than sleeping until 7am.</b> A computed sleep has
/// to be recomputed across DST changes, gets stretched when a laptop sleeps, and has to
/// be re-derived after every restart. A once-a-minute check of "is it past the send hour,
/// and is anyone still owed today's email?" gets all of those right for free, and costs
/// nothing.</para>
///
/// <para>That shape also makes it <b>self-healing</b>. Delivery is recorded per subscriber,
/// so a crash halfway down the list resumes with the rest on the next tick; and a server
/// that was down at 7am sends when it comes back, the same day. A day the server misses
/// entirely is skipped rather than sent late the next morning — yesterday's puzzle is no
/// longer the one on the site.</para>
///
/// <para>Today's teaser comes from <see cref="DailySchedule.ForDay"/>, the same path the
/// home page uses, so subscribers get exactly what the site is showing. If nobody has
/// visited yet, this call is what settles the day.</para>
/// </remarks>
public sealed class DailyDigestService(
    IServiceProvider services,
    SmtpMailer mailer,
    IOptions<SubscriptionOptions> options,
    IOptions<SiteOptions> site,
    ILogger<DailyDigestService> log) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly SubscriptionOptions _options = options.Value;
    private readonly SiteOptions _site = site.Value;
    private readonly DailySendBudget _budget = new(options.Value.MaxDigestsPerDay);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!mailer.IsConfigured)
        {
            log.LogInformation("Daily digest is off (email not configured).");
            return;
        }

        log.LogInformation("Daily digest on; sends from {Hour}:00 Pacific.", _options.SendHourPacific);

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
                // One bad tick must not end the service for good — it would silently
                // stop every future digest. Log it and try again next minute.
                log.LogError(ex, "Daily digest tick failed; will retry.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// One pass: if it's past the send hour, mail everyone still owed today's challenge.
    /// Public so the scheduling rules can be tested without waiting for 7am.
    /// </summary>
    /// <returns>How many digests were sent on this pass.</returns>
    public async Task<int> RunOnceAsync(DateTime nowPacific, CancellationToken token)
    {
        if (nowPacific.Hour < _options.SendHourPacific)
        {
            return 0;
        }

        // Scoped rather than injected: DailySchedule and the stores are singletons today,
        // but a hosted service outlives every scope, so resolving per pass is the safe
        // habit if any of them ever becomes scoped.
        using var scope = services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<SubscriberStore>();
        var schedule = scope.ServiceProvider.GetRequiredService<DailySchedule>();

        var today = DateOnly.FromDateTime(nowPacific);
        store.PruneExpired(DateTime.UtcNow);

        // Only people confirmed before today's send are owed today's. Anyone confirming
        // later has just seen today's challenge (the sign-up link sits under it), and at
        // 11:50pm "send it now" is exactly the midnight email the 7am send exists to
        // avoid. Their first digest is the next morning's.
        var sendTime = today.ToDateTime(new TimeOnly(_options.SendHourPacific, 0));
        var due = store.DueForDigest(today)
            .Where(s => s.ConfirmedAt is { } confirmed && AppTime.ToPacific(confirmed) < sendTime)
            .ToList();
        if (due.Count == 0)
        {
            return 0;
        }

        var teaser = schedule.ForDay(today);
        if (teaser is null)
        {
            log.LogWarning("No challenge for {Day}; digest skipped.", today);
            return 0;
        }

        var sent = 0;
        foreach (var subscriber in due)
        {
            if (!_budget.HasRoom(today))
            {
                log.LogWarning("Daily digest cap ({Cap}) reached with {Left} subscribers still " +
                               "owed. The account is near its sending ceiling — see PRD 15.",
                               _budget.MaxPerDay, due.Count - sent);
                break;
            }

            var mail = BuildMail(subscriber, teaser, today);
            if (await mailer.SendAsync(mail, token))
            {
                _budget.RecordSent(today);
                store.MarkDigestSent(subscriber.Id, today);
                sent++;
            }
            // A failure leaves LastDigestOn untouched, so the next tick retries this
            // subscriber rather than skipping them for the day.
        }

        if (sent > 0)
        {
            log.LogInformation("Sent {Count} {Noun} for {Day}.",
                sent, Wording.Plural(sent, "digest"), today.ToString("yyyy-MM-dd"));
        }
        return sent;
    }

    private OutgoingMail BuildMail(Subscriber subscriber, BrainTeaser teaser, DateOnly today)
    {
        var links = new DigestLinks(
            Solve: _site.Link(""),
            Unsubscribe: _site.Link($"unsubscribe?token={subscriber.Token}"));

        var content = SubscriptionMail.Digest(
            today,
            teaser.Difficulty,
            teaser.Question,
            hasImage: !string.IsNullOrWhiteSpace(teaser.ImageFileName),
            links);

        return new OutgoingMail(subscriber.Email, content.Subject, content.Body,
            Headers: new Dictionary<string, string>
            {
                // Lets Gmail, Outlook and Apple Mail show their own "Unsubscribe" control
                // next to the sender, and is expected of anyone sending mail in bulk. The
                // one-click header tells the client it may POST to the URL directly — see
                // SubscriptionEndpoints.
                ["List-Unsubscribe"] = $"<{links.Unsubscribe}>",
                ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
            },
            Html: content.Html);
    }
}
