using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneADay.Services;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OneADay.Tests;

/// <summary>
/// The traffic alert: counting what reaches the server, and emailing the author once when a
/// day passes the line.
///
/// <para>The stakes: the host bills for data sent and has no billing alerts or spending caps
/// of its own (PRD 11), so this email is the only warning the author gets that something is
/// running up the bill.</para>
/// </summary>
public class TrafficAlertTests
{
    private static readonly DateTime Noon = new(2026, 9, 24, 12, 0, 0);   // Pacific, a Thursday

    private static TrafficMonitor Monitor(int threshold) =>
        new(Options.Create(new TrafficAlertOptions { DailyThreshold = threshold }));

    private static void Record(TrafficMonitor monitor, int served, int turnedAway = 0)
    {
        for (var i = 0; i < served; i++)
        {
            monitor.RecordServed();
        }
        for (var i = 0; i < turnedAway; i++)
        {
            monitor.RecordTurnedAway();
        }
    }

    // ---- counting and the line ----------------------------------------------------

    [Fact]
    public void Below_the_line_nothing_is_reported()
    {
        var monitor = Monitor(threshold: 10);
        Record(monitor, served: 9);

        Assert.Null(monitor.Check(Noon));
    }

    [Fact]
    public void Passing_the_line_reports_once_however_long_the_flood_lasts()
    {
        var monitor = Monitor(threshold: 10);
        Record(monitor, served: 10);

        var report = monitor.Check(Noon);
        Assert.NotNull(report);
        Assert.Equal(10, report.Total);

        // One email a day, not one a minute for as long as the flood lasts.
        Record(monitor, served: 1_000);
        Assert.Null(monitor.Check(Noon.AddMinutes(1)));
        Assert.Null(monitor.Check(Noon.AddHours(11)));
    }

    [Fact]
    public void Turned_away_requests_count_toward_the_line_and_are_reported_apart()
    {
        // A flood at the server's own address is all rejections. It costs little, but it must
        // still trip the alert, and the email must be able to say that's what it is.
        var monitor = Monitor(threshold: 10);
        Record(monitor, served: 3, turnedAway: 7);

        var report = monitor.Check(Noon);

        Assert.NotNull(report);
        Assert.Equal(3, report.Served);
        Assert.Equal(7, report.TurnedAway);
    }

    [Fact]
    public void A_new_Pacific_day_starts_from_zero_and_can_alert_again()
    {
        var monitor = Monitor(threshold: 10);
        Record(monitor, served: 10);
        Assert.NotNull(monitor.Check(Noon));

        var tomorrow = Noon.Date.AddDays(1).AddMinutes(1);
        Assert.Null(monitor.Check(tomorrow));   // yesterday's count doesn't carry over

        Record(monitor, served: 10);
        Assert.NotNull(monitor.Check(tomorrow.AddHours(3)));
    }

    [Fact]
    public void Yesterday_is_reported_only_when_the_whole_day_was_counted()
    {
        // A number for "yesterday" is there to show what a normal day looks like. A day the
        // server only saw part of would show something smaller than normal and mislead.
        var monitor = Monitor(threshold: 10);
        monitor.Check(Noon);                    // the server starts part-way through day 1
        Record(monitor, served: 4);

        var day2 = Noon.Date.AddDays(1);
        monitor.Check(day2.AddMinutes(1));
        Record(monitor, served: 10);
        var second = monitor.Check(day2.AddHours(9));
        Assert.NotNull(second);
        Assert.Null(second.Yesterday);

        var day3 = day2.AddDays(1);
        monitor.Check(day3.AddMinutes(1));
        Record(monitor, served: 10);
        var third = monitor.Check(day3.AddHours(9));
        Assert.NotNull(third);
        Assert.Equal(10, third.Yesterday);
    }

    [Fact]
    public void A_threshold_of_zero_switches_the_alert_off()
    {
        var monitor = Monitor(threshold: 0);
        Record(monitor, served: 1_000);

        Assert.False(monitor.IsOn);
        Assert.Null(monitor.Check(Noon));
    }

    // ---- counting real requests ---------------------------------------------------

    [Fact]
    public async Task Every_request_is_counted_including_the_ones_the_lock_turns_away()
    {
        const string secret = "a-test-secret-that-is-long-enough-0123456789";
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.Configure<CloudflareLockOptions>(o =>
        {
            o.Enabled = true;
            o.Secret = secret;
        });
        builder.Services.Configure<TrafficAlertOptions>(o => o.DailyThreshold = 5);
        builder.Services.AddSingleton<TrafficMonitor>();

        await using var app = builder.Build();
        app.UseCloudflareLock();
        app.UseTrafficCount();
        app.Run(context => context.Response.WriteAsync("page"));
        await app.StartAsync();

        var address = new Uri(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        using var throughCloudflare = new HttpClient { BaseAddress = address };
        throughCloudflare.DefaultRequestHeaders.TryAddWithoutValidation(CloudflareLock.HeaderName, secret);
        using var aroundIt = new HttpClient { BaseAddress = address };

        for (var i = 0; i < 2; i++)
        {
            await throughCloudflare.GetStringAsync("/");
        }
        for (var i = 0; i < 3; i++)
        {
            (await aroundIt.GetAsync("/")).Dispose();
        }

        var report = app.Services.GetRequiredService<TrafficMonitor>().Check(Noon);
        await app.StopAsync();

        Assert.NotNull(report);
        Assert.Equal(2, report.Served);
        Assert.Equal(3, report.TurnedAway);
    }

    [Fact]
    public void Requests_are_counted_straight_after_the_lock()
    {
        // Proven against the real Program.cs, like the lock's own order check. Below the
        // static files, a flood of downloads — the costliest kind — would never be counted.
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "OneADay", "Program.cs"));
        var pipeline = Regex.Matches(program, @"^\s*app\.(?:Use|Map)\w*\(", RegexOptions.Multiline)
            .Select(m => m.Value.Trim())
            .ToList();

        Assert.Equal(new[] { "app.UseCloudflareLock(", "app.UseTrafficCount(" }, pipeline.Take(2));
    }

    // ---- the email ------------------------------------------------------------------

    [Fact]
    public async Task The_author_gets_one_email_when_the_line_is_crossed()
    {
        var monitor = Monitor(threshold: 10);
        var mailer = new RecordingMailer();
        var service = new TrafficAlertService(monitor, mailer,
            Options.Create(new EmailOptions { To = "author@x.com" }),
            NullLogger<TrafficAlertService>.Instance);

        Record(monitor, served: 9);
        Assert.False(await service.RunOnceAsync(Noon, CancellationToken.None));

        Record(monitor, served: 1);
        Assert.True(await service.RunOnceAsync(Noon.AddMinutes(1), CancellationToken.None));

        Record(monitor, served: 5_000);
        Assert.False(await service.RunOnceAsync(Noon.AddMinutes(2), CancellationToken.None));

        var mail = Assert.Single(mailer.Sent);
        Assert.Equal("author@x.com", mail.To);
        Assert.Equal("Stumpty — unusual traffic: 10 requests today", mail.Subject);
    }

    [Fact]
    public async Task Without_email_set_up_the_alert_is_still_logged()
    {
        // The host's logs are the fallback. An unconfigured mailer mustn't make the alert vanish,
        // and mustn't throw.
        var monitor = Monitor(threshold: 1);
        var log = new CapturingLogger<TrafficAlertService>();
        var service = new TrafficAlertService(monitor,
            new SmtpMailer(Options.Create(new EmailOptions()), NullLogger<SmtpMailer>.Instance),
            Options.Create(new EmailOptions()), log);

        Record(monitor, served: 1);

        Assert.False(await service.RunOnceAsync(Noon, CancellationToken.None));
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Unusual traffic"));
    }

    [Fact]
    public void The_email_says_what_happened_and_what_to_do()
    {
        var mail = TrafficAlertMail.Build(new TrafficReport(
            new DateOnly(2026, 9, 24), Noon.AddMinutes(5),
            Served: 48_120, TurnedAway: 1_880, Threshold: 50_000, Yesterday: 9_812));

        Assert.Equal("Stumpty — unusual traffic: 50,000 requests today", mail.Subject);
        Assert.Contains("48,120", mail.Body);
        Assert.Contains("1,880", mail.Body);
        Assert.Contains("Yesterday's total was 9,812.", mail.Body);
        Assert.Contains("12:05pm Pacific on Thursday 24 September", mail.Body);
        Assert.Contains("Most of it was served", mail.Body);
        Assert.Contains("stop the machine", mail.Body);
        Assert.Contains("at most once a day", mail.Body);
    }

    [Fact]
    public void A_flood_at_the_servers_own_address_is_called_one()
    {
        var mail = TrafficAlertMail.Build(new TrafficReport(
            new DateOnly(2026, 9, 24), Noon,
            Served: 100, TurnedAway: 60_000, Threshold: 50_000, Yesterday: null));

        Assert.Contains("Most of it was turned away by the Cloudflare lock", mail.Body);
        Assert.Contains("isn't known", mail.Body);
    }

    [Fact]
    public void Production_settings_switch_the_alert_on()
    {
        using var doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(FindRepoRoot(), "OneADay", "appsettings.json")));
        var threshold = doc.RootElement.GetProperty(TrafficAlertOptions.Section)
            .GetProperty("DailyThreshold").GetInt32();

        Assert.True(threshold > 0);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, ".gitignore")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Couldn't find the repo root.");
    }
}

/// <summary>A logger that keeps what it's given, for checking what was logged.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                            Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}
