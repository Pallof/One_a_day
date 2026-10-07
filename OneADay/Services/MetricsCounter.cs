using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;
using OneADay.Models;

namespace OneADay.Services;

/// <summary>Where a browser's <see cref="BrowserNote"/> is kept.</summary>
public interface IBrowserNoteStorage
{
    ValueTask<BrowserNote?> ReadAsync();
    ValueTask WriteAsync(BrowserNote note);
}

/// <summary>The note in the browser's own storage, encrypted by this server.</summary>
public sealed class ProtectedBrowserNoteStorage(ProtectedLocalStorage storage) : IBrowserNoteStorage
{
    public const string Key = "oad-metrics-note";

    /// <summary>An unreadable note reads as none: a first visit, never an error.</summary>
    public ValueTask<BrowserNote?> ReadAsync() => storage.ReadOrDefaultAsync<BrowserNote>(Key);

    public ValueTask WriteAsync(BrowserNote note) => storage.SetAsync(Key, note);
}

/// <summary>
/// Counts one browser's activity for PRD 16, one per live connection.
/// </summary>
/// <remarks>
/// <para><b>Only a live browser counts.</b> Every count needs the browser's note written
/// first, and that takes the live connection: a page fetched as plain HTML — by a scanner, a
/// link preview, a bot — can't write it, so it counts nothing. That ordering is deliberate:
/// a note that can't be saved means the step isn't counted, rather than counted again on
/// every click.</para>
///
/// <para><b>Counting never breaks a page.</b> Any failure is logged and skipped, the same
/// rule as email (PRD 14).</para>
/// </remarks>
public sealed class MetricsCounter(
    IBrowserNoteStorage storage,
    MetricsStore store,
    ILogger<MetricsCounter> log,
    Func<DateOnly>? today = null)
{
    private readonly Func<DateOnly> _today = today ?? (() => AppTime.Today);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private BrowserNote? _note;

    /// <summary>The challenge page is open and live.</summary>
    public Task PuzzleSeenAsync(Guid puzzle) => Step((note, day) =>
    {
        var (next, visit) = note.Visit(day);
        (next, var seen) = next.SeePuzzle(day, puzzle);
        return (next, () =>
        {
            Visit(day, visit);
            if (seen)
            {
                store.RecordPuzzleSeen(day, puzzle);
            }
        });
    });

    /// <summary>An answer to the challenge, right or wrong.</summary>
    public Task AnswerAsync(Guid puzzle, bool correct) => Step((note, day) =>
    {
        var (next, visit) = note.Visit(day);
        (next, var counted) = next.Answer(day, puzzle, correct);
        return (next, () =>
        {
            Visit(day, visit);
            store.RecordAnswer(day, puzzle, counted);
        });
    });

    /// <summary>
    /// A Twenty Four answer was submitted. Only this makes a Twenty Four visitor count as a
    /// visitor — opening the page alone doesn't (author's decision, 2026-10-06).
    /// </summary>
    public Task TwentyFourAnswerAsync(bool solved) => Step((note, day) =>
    {
        var (next, visit) = note.Visit(day);
        (next, var firstToday) = next.PlayTwentyFour(day);
        return (next, () =>
        {
            Visit(day, visit);
            if (firstToday)
            {
                store.RecordTwentyFourPlayer(day);
            }
            if (solved)
            {
                store.RecordTwentyFourHand(day, solved: true);
            }
        });
    });

    /// <summary>A Twenty Four hand was passed. A hand, not a player: passing isn't submitting.</summary>
    public Task TwentyFourPassAsync() => Step((note, day) =>
        (note, () => store.RecordTwentyFourHand(day, solved: false)));

    private void Visit(DateOnly day, VisitKind? kind)
    {
        if (kind is { } counted)
        {
            store.RecordVisit(day, counted);
        }
    }

    /// <summary>
    /// Works out the next note, saves it to the browser, and only then counts. One step at a
    /// time, so a fast double click can't read the same note twice.
    /// </summary>
    private async Task Step(Func<BrowserNote, DateOnly, (BrowserNote Next, Action Count)> step)
    {
        await _gate.WaitAsync();
        try
        {
            _note ??= await storage.ReadAsync() ?? new BrowserNote();

            var (next, count) = step(_note, _today());
            if (next != _note)
            {
                await storage.WriteAsync(next);
                _note = next;
            }
            count();
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or InvalidOperationException)
        {
            // The browser went away mid-step, or this is a plain page render with no live
            // connection to write to. Either way there's nothing to count.
            log.LogDebug(ex, "Metrics step skipped.");
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Metrics step failed; the visitor's page is unaffected.");
        }
        finally
        {
            _gate.Release();
        }
    }
}

/// <summary>
/// Saves the metrics about once a minute, and records the email list's size as totals —
/// so the numbers can be downloaded without the addresses (PRD 16).
/// </summary>
public sealed class MetricsFlusher(MetricsStore metrics, SubscriberStore subscribers, ILogger<MetricsFlusher> log)
    : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            SaveNow();
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        SaveNow();
    }

    /// <summary>Records the list's size and saves. Public so tests needn't wait a minute.</summary>
    public void SaveNow()
    {
        try
        {
            var (confirmed, pending) = subscribers.Counts;
            metrics.SetSubscriberTotals(AppTime.Today, confirmed, pending);
            metrics.Flush();
        }
        catch (Exception ex)
        {
            // A failed save keeps the totals in memory for the next try.
            log.LogError(ex, "Saving metrics failed; will retry.");
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
