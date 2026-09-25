using Microsoft.Extensions.Options;

namespace OneADay.Services;

/// <summary>When to email the author about unusual traffic.</summary>
public sealed class TrafficAlertOptions
{
    public const string Section = "TrafficAlert";

    /// <summary>
    /// Requests reaching the server in one Pacific day that trigger the email. Zero or less
    /// switches the alert off.
    /// </summary>
    /// <remarks>
    /// With Cloudflare serving the scripts and styles from its cache, a page view reaches the
    /// server about four times: the page itself, then the three requests that open its live
    /// connection. A 1,000-visitor day is roughly 10,000 requests, so 50,000 sits well clear of
    /// a good day and far above launch traffic. Raise it as the site grows.
    /// </remarks>
    public int DailyThreshold { get; set; } = 50_000;
}

/// <summary>One day's traffic at the moment it crossed the alert line.</summary>
/// <param name="Yesterday">The previous day's total, or null when this server didn't count
/// all of it.</param>
public sealed record TrafficReport(
    DateOnly Day, DateTime AtPacific, long Served, long TurnedAway, int Threshold, long? Yesterday)
{
    public long Total => Served + TurnedAway;
}

/// <summary>
/// Counts the requests that reach the server each Pacific day, and says when a day passes
/// the alert line — once.
/// </summary>
/// <remarks>
/// <para>The host bills for data sent and has no billing alerts or spending caps of its own,
/// so the site watches for itself (PRD 11). Counting is one atomic increment per request;
/// everything else happens in <see cref="Check"/>, which <see cref="TrafficAlertService"/>
/// calls once a minute, off the request path.</para>
///
/// <para>Requests the Cloudflare lock turns away are counted separately. Each costs almost
/// nothing — 99 bytes — but a stream of them means someone has found the server's own
/// address, which is worth knowing.</para>
/// </remarks>
public sealed class TrafficMonitor(IOptions<TrafficAlertOptions> options)
{
    private readonly int _threshold = options.Value.DailyThreshold;
    private readonly object _gate = new();

    private long _served;
    private long _turnedAway;

    private DateOnly? _day;        // the Pacific day the counters belong to
    private bool _dayIsWhole;      // counted from its start, not from a restart part-way through
    private long? _yesterday;      // the previous day's total, when it was counted whole
    private DateOnly? _alertedOn;

    public bool IsOn => _threshold > 0;

    public int Threshold => _threshold;

    public void RecordServed() => Interlocked.Increment(ref _served);

    public void RecordTurnedAway() => Interlocked.Increment(ref _turnedAway);

    /// <summary>
    /// Rolls the counts over at midnight Pacific, and returns a report the first time a day
    /// passes the alert line. Null every other time, so a day alerts once however long the
    /// flood lasts.
    /// </summary>
    /// <remarks>
    /// Called once a minute, so a request in the minute after midnight can land in the day
    /// before. That's noise at this scale.
    /// </remarks>
    public TrafficReport? Check(DateTime nowPacific)
    {
        var today = DateOnly.FromDateTime(nowPacific);

        lock (_gate)
        {
            if (_day is null)
            {
                _day = today;   // the first check: everything counted since the start is today's
            }
            else if (_day != today)
            {
                var total = Interlocked.Exchange(ref _served, 0) + Interlocked.Exchange(ref _turnedAway, 0);

                // Only a whole day that really was yesterday says what a normal day looks
                // like. A restart part-way through, or a server that was off for a day, would
                // put a meaningless number in the email.
                _yesterday = _dayIsWhole && _day == today.AddDays(-1) ? total : null;
                _day = today;
                _dayIsWhole = true;
            }

            var served = Interlocked.Read(ref _served);
            var turnedAway = Interlocked.Read(ref _turnedAway);

            if (!IsOn || served + turnedAway < _threshold || _alertedOn == today)
            {
                return null;
            }

            _alertedOn = today;
            return new TrafficReport(today, nowPacific, served, turnedAway, _threshold, _yesterday);
        }
    }
}

/// <summary>Wires <see cref="TrafficMonitor"/> into the request pipeline.</summary>
public static class TrafficCount
{
    /// <summary>Counts every request the Cloudflare lock lets in.</summary>
    /// <remarks>
    /// <b>Register this straight after <see cref="CloudflareLock.UseCloudflareLock"/></b>,
    /// before anything that can answer a request. Below the static files, a flood of file
    /// downloads — the costliest kind — would never be counted. TrafficAlertTests pins the
    /// order against Program.cs.
    /// </remarks>
    public static WebApplication UseTrafficCount(this WebApplication app)
    {
        var monitor = app.Services.GetRequiredService<TrafficMonitor>();
        app.Use((context, next) =>
        {
            monitor.RecordServed();
            return next(context);
        });
        return app;
    }
}
