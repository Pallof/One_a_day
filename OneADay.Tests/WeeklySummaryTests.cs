using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The Monday summary email (PRD 16): when it goes, that it goes once, and what it says.
///
/// <para>The stakes: it lands in a real inbox from the live site. A resend loop would bury that
/// inbox and could get the Gmail account throttled; an answer or an address in it would leave
/// the server in plain text.</para>
/// </summary>
public class WeeklySummaryTests : IDisposable
{
    // Pacific. Monday 5 October 2026; the week summarised is Monday 28 September to Sunday 4 October.
    private static readonly DateTime MondaySeven = new(2026, 10, 5, 7, 0, 0);
    private static readonly DateOnly WeekFrom = new(2026, 9, 28);
    private static readonly DateOnly WeekTo = new(2026, 10, 4);

    private readonly TestEnvironment _env = new();
    private readonly MetricsData _data = new();
    private readonly List<BrainTeaser> _bank = [];
    private readonly List<DailyRun> _history = [];
    private readonly RecordingMailer _mailer = new();

    public void Dispose() => _env.Dispose();

    // ---- when it goes ---------------------------------------------------------------

    [Fact]
    public async Task Nothing_goes_before_seven_on_Monday_and_the_summary_goes_at_seven()
    {
        var service = Service();

        Assert.False(await service.RunOnceAsync(MondaySeven.AddMinutes(-1), default));
        Assert.Empty(_mailer.Sent);

        Assert.True(await service.RunOnceAsync(MondaySeven, default));
        var mail = Assert.Single(_mailer.Sent);
        Assert.Equal("author@x.com", mail.To);
        Assert.Equal("Stumpty — the week in numbers, 28 Sep – 4 Oct", mail.Subject);
    }

    [Fact]
    public async Task It_goes_once_a_week_however_often_it_checks()
    {
        var service = Service();
        Assert.True(await service.RunOnceAsync(MondaySeven, default));

        var sundayNight = MondaySeven.AddDays(6).AddHours(16).AddMinutes(59);
        foreach (var later in new[] { MondaySeven.AddMinutes(1), MondaySeven.AddDays(1), sundayNight })
        {
            Assert.False(await service.RunOnceAsync(later, default));
        }

        Assert.True(await service.RunOnceAsync(MondaySeven.AddDays(7), default));
        Assert.Collection(_mailer.Sent,
            first => Assert.EndsWith("28 Sep – 4 Oct", first.Subject),
            second => Assert.EndsWith("5 Oct – 11 Oct", second.Subject));
    }

    [Fact]
    public async Task A_restart_or_redeploy_doesnt_send_the_week_again()
    {
        Assert.True(await Service().RunOnceAsync(MondaySeven, default));

        // A new service over the same App_Data is what a restart or a redeploy leaves behind.
        Assert.False(await Service().RunOnceAsync(MondaySeven.AddHours(1), default));
        Assert.Single(_mailer.Sent);
    }

    [Fact]
    public async Task A_Monday_the_server_missed_goes_out_when_it_is_back_that_week()
    {
        var wednesday = MondaySeven.AddDays(2).AddHours(3);

        Assert.True(await Service().RunOnceAsync(wednesday, default));
        Assert.EndsWith("28 Sep – 4 Oct", Assert.Single(_mailer.Sent).Subject);
    }

    [Fact]
    public async Task A_failed_send_is_tried_again_an_hour_later_not_every_minute()
    {
        var service = Service();
        _mailer.FailFor.Add("author@x.com");
        Assert.False(await service.RunOnceAsync(MondaySeven, default));

        _mailer.FailFor.Clear();
        Assert.False(await service.RunOnceAsync(MondaySeven.AddMinutes(59), default));
        Assert.True(await service.RunOnceAsync(MondaySeven.AddHours(1), default));
        Assert.Single(_mailer.Sent);
    }

    [Fact]
    public async Task Without_email_set_up_nothing_is_sent_or_recorded()
    {
        var unconfigured = new RecordingMailer(new EmailOptions());

        Assert.False(await Service(unconfigured).RunOnceAsync(MondaySeven, default));
        Assert.Empty(unconfigured.Sent);

        // Nothing was marked sent, so setting email up later still delivers the week.
        Assert.True(await Service().RunOnceAsync(MondaySeven.AddHours(1), default));
    }

    [Fact]
    public async Task An_unreadable_record_counts_as_not_sent_and_is_replaced()
    {
        _env.WriteDataFile("weekly-summary.json", "{ not json");

        Assert.True(await Service().RunOnceAsync(MondaySeven, default));
        Assert.Contains("\"2026-09-28\"", _env.ReadDataFile("weekly-summary.json"));
    }

    [Fact]
    public async Task A_record_that_cant_be_saved_still_stops_a_resend()
    {
        // A folder where the file belongs makes every save fail, like a full disk. Without the
        // week remembered in memory, this would mail the author every minute until a restart.
        Directory.CreateDirectory(Path.Combine(_env.ContentRootPath, "App_Data", "weekly-summary.json"));
        var service = Service();

        Assert.True(await service.RunOnceAsync(MondaySeven, default));
        Assert.False(await service.RunOnceAsync(MondaySeven.AddMinutes(1), default));
        Assert.Single(_mailer.Sent);
    }

    // ---- as a background service ---------------------------------------------------

    [Theory]
    [InlineData("Development", true)]   // the author's Mac: admin shows these numbers there
    [InlineData("Production", false)]   // a server with no email set up
    public async Task Off_on_the_authors_Mac_and_without_email_it_stops_at_startup(string environment, bool emailSetUp)
    {
        _env.EnvironmentName = environment;
        var mailer = emailSetUp ? _mailer : new RecordingMailer(new EmailOptions());
        var service = Service(mailer, clock: () => MondaySeven);

        await service.StartAsync(default);
        try
        {
            var stopped = await Task.WhenAny(service.ExecuteTask!, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(service.ExecuteTask, stopped);
        }
        finally
        {
            await service.StopAsync(default);
        }
        Assert.Empty(mailer.Sent);
    }

    [Fact]
    public async Task On_the_live_site_it_sends_and_keeps_watching()
    {
        // The control for the test above: the same service, with email set up outside
        // Development, does send and doesn't stop.
        var service = Service(clock: () => MondaySeven);

        await service.StartAsync(default);
        try
        {
            await Until(() => _mailer.Sent.Count > 0);
            Assert.Single(_mailer.Sent);
            Assert.False(service.ExecuteTask!.IsCompleted);
        }
        finally
        {
            await service.StopAsync(default);
        }
    }

    [Fact]
    public async Task Whatever_goes_wrong_in_a_tick_the_service_and_the_site_keep_running()
    {
        // An exception escaping a background service stops the whole app, the site with it.
        var mailer = new ThrowingMailer();
        var log = new CapturingLogger<WeeklySummaryService>();
        var service = Service(mailer, log, clock: () => MondaySeven);

        await service.StartAsync(default);
        try
        {
            await Until(() => service.ExecuteTask!.IsCompleted
                              || log.Entries.ToArray().Any(e => e.Level == LogLevel.Error));
            Assert.Equal(1, mailer.Calls);
            Assert.False(service.ExecuteTask!.IsCompleted);
        }
        finally
        {
            await service.StopAsync(default);
        }
    }

    [Fact]
    public void The_live_site_runs_it()
    {
        // Every test above passes on a service nothing starts. This line is what starts it.
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "OneADay", "Program.cs"));

        Assert.Matches(@"(?m)^builder\.Services\.AddHostedService<WeeklySummaryService>\(\);", program);
    }

    // ---- what it says -----------------------------------------------------------------

    [Fact]
    public async Task The_week_is_the_Monday_to_Sunday_just_ended()
    {
        On(WeekFrom.AddDays(-1)).Visitors.New = 1000;   // the Sunday before: last week's email
        On(WeekFrom).Visitors.New = 3;
        On(WeekTo).Visitors.New = 4;
        On(WeekTo.AddDays(1)).Visitors.New = 500;       // this morning, still under way

        var lines = Lines(await SentBody());

        Assert.Contains("Stumpty's week, Monday 28 September to Sunday 4 October.", lines);
        Assert.Contains("  Daily visitors, added up: 7", lines);
        Assert.Contains("  By day: Mon 3 · Tue 0 · Wed 0 · Thu 0 · Fri 0 · Sat 0 · Sun 4", lines);
    }

    [Fact]
    public async Task Headline_numbers_come_first_then_each_puzzle()
    {
        var puzzle = Teaser("Which way does the wind blow?");
        Ran(puzzle, WeekTo);
        On(WeekTo).Visitors.New = 5;
        On(WeekTo).Puzzles[puzzle.Id] = new PuzzleCounts { Saw = 5 };

        var body = await SentBody();

        var order = new[] { "VISITORS", "ROTATION", "DAILY EMAIL", "TWENTY FOUR", "\"Which way does the wind blow?\"" }
            .Select(heading => body.IndexOf(heading, StringComparison.Ordinal))
            .ToList();
        Assert.DoesNotContain(-1, order);
        Assert.Equal(order.Order(), order);
    }

    [Fact]
    public async Task Each_puzzle_shows_how_it_ran_and_how_it_played()
    {
        var twice = Teaser("Which way does the wind blow?");
        var other = Teaser("Another puzzle?");
        Ran(twice, WeekFrom);
        Ran(other, WeekFrom.AddDays(1));
        Ran(twice, WeekTo, recycled: true);
        On(WeekFrom).Puzzles[twice.Id] = new PuzzleCounts
        {
            Saw = 10, Tried = 8, Solved = 4, SolvedIn = [2, 1, 0, 0, 1], SolveAttempts = 2 + 2 + 9,
        };
        On(WeekFrom.AddDays(1)).Puzzles[other.Id] = new PuzzleCounts { Saw = 5 };
        On(WeekTo).Puzzles[twice.Id] = new PuzzleCounts
        {
            Saw = 5, Tried = 4, Solved = 2, SolvedIn = [0, 2, 0, 0, 0], SolveAttempts = 4,
        };

        var lines = Lines(await SentBody());

        Assert.Contains("  Recycled days: 1 of 3", lines);
        var at = lines.IndexOf("  \"Which way does the wind blow?\"");
        Assert.Equal(
        [
            "    Ran 2 days, 1 recycled · share of viewers 75% · 15 of 20",
            "    Saw 15 → tried 12 → solved 6",
            "    Solve rate: 50% · 6 of 12 (too few to judge)",
            "    Attempts: 2 in one, 3 in two, 0 in three, 0 in four, 1 in five or more · average 2.8 (too few to judge)",
        ], lines.Skip(at + 1).Take(4));
    }

    [Fact]
    public async Task Every_percentage_shows_its_count()
    {
        var puzzle = Teaser("A puzzle?");
        Ran(puzzle, WeekFrom);
        On(WeekFrom).Visitors.New = 40;
        On(WeekFrom.AddDays(1)).Visitors.BackFromYesterday = 10;
        On(WeekFrom).Puzzles[puzzle.Id] = new PuzzleCounts
        {
            Saw = 40, Tried = 12, Solved = 7, SolvedIn = [7, 0, 0, 0, 0], SolveAttempts = 7,
        };

        var body = await SentBody();

        Assert.Contains("Solve rate: 58% · 7 of 12", body);
        var percentages = Regex.Matches(body, @"\d+%");
        Assert.True(percentages.Count >= 4, $"Expected at least four percentages, found {percentages.Count}.");
        Assert.All(percentages, m => Assert.Matches(@"^\d+% · \d+ of \d+", body[m.Index..]));
    }

    [Fact]
    public async Task Fewer_than_twenty_browsers_is_too_few_to_judge_and_twenty_is_enough()
    {
        var small = Teaser("Small?");
        var middle = Teaser("Middle?");
        var big = Teaser("Big?");
        On(WeekFrom).Puzzles[small.Id] = new PuzzleCounts
        {
            Saw = 12, Tried = 12, Solved = 7, SolvedIn = [7, 0, 0, 0, 0], SolveAttempts = 7,
        };
        On(WeekFrom.AddDays(1)).Puzzles[middle.Id] = new PuzzleCounts
        {
            Saw = 30, Tried = 24, Solved = 12, SolvedIn = [6, 6, 0, 0, 0], SolveAttempts = 18,
        };
        On(WeekTo).Puzzles[big.Id] = new PuzzleCounts
        {
            Saw = 30, Tried = 20, Solved = 20, SolvedIn = [10, 10, 0, 0, 0], SolveAttempts = 30,
        };

        var lines = Lines(await SentBody());

        Assert.Contains("    Solve rate: 58% · 7 of 12 (too few to judge)", lines);
        Assert.Contains("    Attempts: 7 in one, 0 in two, 0 in three, 0 in four, 0 in five or more · average 1.0 (too few to judge)", lines);
        // 24 tried is enough for the rate; the average comes from the 12 who solved, which isn't.
        Assert.Contains("    Solve rate: 50% · 12 of 24", lines);
        Assert.Contains("    Attempts: 6 in one, 6 in two, 0 in three, 0 in four, 0 in five or more · average 1.5 (too few to judge)", lines);
        Assert.Contains("    Solve rate: 100% · 20 of 20", lines);
        Assert.Contains("    Attempts: 10 in one, 10 in two, 0 in three, 0 in four, 0 in five or more · average 1.5", lines);
    }

    [Fact]
    public async Task Questions_go_in_but_never_an_answer_hint_or_solution()
    {
        var opened = Teaser("What has keys but opens no locks?",
            answer: "zq-answer-one", hint: "zq-hint-one", solution: "zq-solution-one");
        var unopened = Teaser("What runs but never walks?",
            answer: "zq-answer-two", hint: "zq-hint-two", solution: "zq-solution-two");
        Ran(opened, WeekFrom);
        Ran(unopened, WeekTo);
        On(WeekFrom).Puzzles[opened.Id] = new PuzzleCounts
        {
            Saw = 4, Tried = 2, Solved = 1, SolvedIn = [1, 0, 0, 0, 0], SolveAttempts = 1,
        };

        var body = await SentBody();
        var html = _mailer.Sent[0].Html!;

        Assert.DoesNotContain("zq-", body);
        Assert.DoesNotContain("zq-", html);
        Assert.DoesNotContain("zq-", _mailer.Sent[0].Subject);
        Assert.Contains("\"What has keys but opens no locks?\"", body);
        Assert.Contains("\"What runs but never walks?\"", body);
        Assert.Contains("What has keys but opens no locks?", Visible(html));
        Assert.Contains("What runs but never walks?", Visible(html));
    }

    [Fact]
    public async Task A_puzzle_that_ran_with_nobody_opening_it_is_still_listed()
    {
        var quiet = Teaser("A quiet day's puzzle?");
        Ran(quiet, WeekFrom.AddDays(2));

        var lines = Lines(await SentBody());

        var at = lines.IndexOf("  \"A quiet day's puzzle?\"");
        Assert.True(at >= 0, "The puzzle that ran isn't listed.");
        Assert.Equal("    Ran 1 day · nobody opened it", lines[at + 1]);
    }

    [Fact]
    public async Task A_week_with_nothing_counted_still_sends_and_says_so()
    {
        // Silence would look the same as the summary breaking. An empty week is news too.
        var lines = Lines(await SentBody());

        Assert.Contains("  No visitors were counted this week.", lines);
        Assert.Contains("  No puzzle ran this week.", lines);
        Assert.Contains("  The list's size hasn't been recorded yet.", lines);
    }

    [Fact]
    public async Task The_list_goes_in_as_totals_and_no_address_ever_does()
    {
        // Real addresses sit in the same App_Data on the live site. Built through the container
        // with the subscriber store in it, as Program.cs builds it.
        var subscribers = _env.NewSubscriberStore();
        var at = new DateTime(2026, 9, 29, 17, 0, 0, DateTimeKind.Utc);
        foreach (var address in new[] { "first.reader@example.com", "second.reader@example.com" })
        {
            subscribers.Confirm(subscribers.Request(address, at).Token, at);
        }
        On(WeekTo).Subscribers = new SubscriberCounts { Confirmed = 2, Pending = 1, Confirmations = 2, Unsubscribes = 1 };
        var (metrics, rotation, teasers) = Stores();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWebHostEnvironment>(_env);
        services.AddSingleton(subscribers);
        services.AddSingleton(metrics);
        services.AddSingleton(rotation);
        services.AddSingleton(teasers);
        services.AddSingleton<SmtpMailer>(_mailer);
        services.AddSingleton(Options.Create(new EmailOptions { To = "author@x.com" }));
        services.AddSingleton<WeeklySummaryService>();
        using var provider = services.BuildServiceProvider();

        Assert.True(await provider.GetRequiredService<WeeklySummaryService>().RunOnceAsync(MondaySeven, default));

        var mail = Assert.Single(_mailer.Sent);
        var lines = Lines(mail.Body);
        Assert.Contains("  Confirmed: 2 · waiting to confirm: 1 · as of 4 Oct", lines);
        Assert.Contains("  Confirmed this week: 2 · unsubscribed: 1", lines);
        Assert.Contains("2 confirmed 1 waiting, as of 4 Oct 2 confirmed this week 1 unsubscribed", Visible(mail.Html!));
        Assert.DoesNotContain("@", mail.Body);
        Assert.DoesNotContain("@", mail.Subject);
        Assert.DoesNotContain("reader@example.com", mail.Html);   // "@" alone would match the stylesheet's @media
    }

    // ---- the styled version -------------------------------------------------------------

    [Fact]
    public async Task It_goes_styled_with_the_plain_text_beside_it()
    {
        var puzzle = Teaser("Which way does the wind blow?");
        Ran(puzzle, WeekTo);
        On(WeekTo).Puzzles[puzzle.Id] = new PuzzleCounts { Saw = 5 };

        Assert.True(await Service().RunOnceAsync(MondaySeven, default));

        var mail = Assert.Single(_mailer.Sent);
        Assert.NotNull(mail.Html);
        Assert.Contains("href=\"https://stumpty.test/\"", mail.Html);
        Assert.Contains("Which way does the wind blow?", Visible(mail.Html));
        Assert.Contains("\"Which way does the wind blow?\"", mail.Body);
    }

    [Fact]
    public async Task The_styled_version_encodes_what_it_shows()
    {
        var puzzle = Teaser("Is 3 < 5 & 5 > 3? Say <b>yes</b>.");
        Ran(puzzle, WeekTo);
        On(WeekTo).Puzzles[puzzle.Id] = new PuzzleCounts { Saw = 5 };

        var html = await SentHtml();

        Assert.DoesNotContain("<b>yes</b>", html);
        Assert.Contains("Is 3 &lt; 5 &amp; 5 &gt; 3? Say &lt;b&gt;yes&lt;/b&gt;.", html);
    }

    [Fact]
    public async Task The_styled_version_shows_every_percentage_with_its_count()
    {
        var puzzle = Teaser("A puzzle?");
        Ran(puzzle, WeekFrom);
        On(WeekFrom).Visitors.New = 40;
        On(WeekFrom.AddDays(1)).Visitors.BackFromYesterday = 10;
        On(WeekFrom).Puzzles[puzzle.Id] = new PuzzleCounts
        {
            Saw = 40, Tried = 12, Solved = 7, SolvedIn = [7, 0, 0, 0, 0], SolveAttempts = 7,
        };

        var text = Visible(await SentHtml());

        Assert.Contains("Solve rate 58% · 7 of 12", text);
        var percentages = Regex.Matches(text, @"\d+%");
        Assert.True(percentages.Count >= 4, $"Expected at least four percentages, found {percentages.Count}.");
        Assert.All(percentages, m => Assert.Matches(@"^\d+%\s*(?:·\s*)?\d+ of \d+", text[m.Index..]));
    }

    [Theory]
    [InlineData(19, 19, 3)]   // rate and average both from 19: two tags, plus the footer's explanation
    [InlineData(24, 12, 2)]   // 24 tried is enough for the rate; the average comes from 12 solvers
    [InlineData(20, 20, 1)]   // 20 is enough for both, so only the footer mentions it
    public async Task The_styled_version_tags_figures_from_fewer_than_twenty_browsers(int tried, int solved, int mentions)
    {
        var puzzle = Teaser("A puzzle?");
        On(WeekTo).Puzzles[puzzle.Id] = new PuzzleCounts
        {
            Saw = tried, Tried = tried, Solved = solved, SolvedIn = [solved, 0, 0, 0, 0], SolveAttempts = solved,
        };

        var text = Visible(await SentHtml());

        Assert.Equal(mentions, Regex.Matches(text, "too few to judge").Count);
    }

    [Fact]
    public async Task A_heavy_week_stays_clear_of_Gmails_clipping()
    {
        // Gmail hides everything past about 102 KB behind "View entire message": the puzzles at
        // the bottom. Nine is more than a week holds, even with a puzzle replaced mid-day.
        for (var i = 0; i < 9; i++)
        {
            var puzzle = Teaser($"Puzzle {i}: " + string.Join(' ', Enumerable.Repeat("a fairly long question", 10)));
            var day = WeekFrom.AddDays(i % 7);
            Ran(puzzle, day, recycled: i % 2 == 0);
            On(day).Visitors.New += 300;
            On(day).Puzzles[puzzle.Id] = new PuzzleCounts
            {
                Saw = 400, Tried = 300, Solved = 200, SolvedIn = [80, 60, 30, 20, 10], SolveAttempts = 500,
            };
        }
        On(WeekTo).Subscribers = new SubscriberCounts { Confirmed = 1234, Pending = 56 };

        var html = await SentHtml();

        Assert.Equal(9, Regex.Matches(html, "Attempts to solve").Count);   // every card is in it
        var bytes = Encoding.UTF8.GetByteCount(html);
        Assert.True(bytes < 92_000, $"The styled email is {bytes:N0} bytes; Gmail clips at about 102 KB.");
    }

    // ---- helpers ------------------------------------------------------------------------

    private BrainTeaser Teaser(string question, string answer = "an answer", string? hint = null,
                               string? solution = null)
    {
        var teaser = TeaserFactory.On("2026-09-01", question, answer, hint, solution);
        _bank.Add(teaser);
        return teaser;
    }

    private void Ran(BrainTeaser teaser, DateOnly day, bool recycled = false) =>
        _history.Add(new DailyRun { Date = day, TeaserId = teaser.Id, Recycled = recycled });

    private DayMetrics On(DateOnly day)
    {
        var key = MetricsData.Key(day);
        if (!_data.Days.TryGetValue(key, out var metrics))
        {
            metrics = new DayMetrics();
            _data.Days[key] = metrics;
        }
        return metrics;
    }

    /// <summary>The stores, loaded from files written from what each test set up.</summary>
    private (MetricsStore, RotationStore, TeaserStore) Stores()
    {
        _env.SeedTeasers([.. _bank]);
        _env.WriteDataFile("metrics.json", JsonSerializer.Serialize(_data));
        _env.WriteDataFile("rotation.json", JsonSerializer.Serialize(new RotationState { History = _history }));
        return (_env.NewMetricsStore(), _env.NewRotationStore(), _env.NewTeaserStore());
    }

    private WeeklySummaryService Service(SmtpMailer? mailer = null, ILogger<WeeklySummaryService>? log = null,
                                         Func<DateTime>? clock = null)
    {
        var (metrics, rotation, teasers) = Stores();
        return new WeeklySummaryService(metrics, rotation, teasers, mailer ?? _mailer,
            Options.Create(new EmailOptions { To = "author@x.com" }),
            Options.Create(new SiteOptions { BaseUrl = "https://stumpty.test" }), _env,
            log ?? NullLogger<WeeklySummaryService>.Instance, clock);
    }

    /// <summary>Monday's email, sent from everything set up so far.</summary>
    private async Task<string> SentBody()
    {
        Assert.True(await Service().RunOnceAsync(MondaySeven, default));
        return Assert.Single(_mailer.Sent).Body;
    }

    /// <summary>Monday's styled email, sent from everything set up so far.</summary>
    private async Task<string> SentHtml()
    {
        Assert.True(await Service().RunOnceAsync(MondaySeven, default));
        return Assert.Single(_mailer.Sent).Html!;
    }

    private static List<string> Lines(string body) => body.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

    /// <summary>The styled version's text as a reader sees it: tags stripped, then decoded.</summary>
    private static string Visible(string html)
    {
        var body = Regex.Replace(html, "<head>.*?</head>", " ", RegexOptions.Singleline);
        var text = WebUtility.HtmlDecode(Regex.Replace(body, "<[^>]+>", " "));
        return Regex.Replace(text, @"\s+", " ");
    }

    /// <summary>Waits for something the background service does, failing after five seconds.</summary>
    private static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the background service.");
            await Task.Delay(10);
        }
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

    /// <summary>A mailer that throws, standing in for anything going wrong mid-tick.</summary>
    private sealed class ThrowingMailer() : SmtpMailer(
        Options.Create(new EmailOptions
        {
            Enabled = true, To = "a@x.com", From = "a@x.com", Host = "h", AppPassword = "p",
        }),
        NullLogger<SmtpMailer>.Instance)
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public override Task<bool> SendAsync(OutgoingMail mail, CancellationToken token)
        {
            Interlocked.Increment(ref _calls);
            throw new InvalidOperationException("The mail server fell over.");
        }
    }
}
