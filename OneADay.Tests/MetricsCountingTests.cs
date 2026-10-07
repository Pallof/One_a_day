using Microsoft.Extensions.Logging.Abstractions;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// PRD 16's counting rules: what a browser's note says is new, and what the counter adds to the
/// day's totals. A "reload" here is a fresh counter over the same stored note — exactly what a
/// new live connection from the same browser is.
/// </summary>
public class MetricsCountingTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 10, 6);
    private static readonly Guid Puzzle = Guid.NewGuid();

    private readonly TestEnvironment _env = new();
    private readonly MetricsStore _store;
    private readonly InMemoryNoteStorage _browser = new();

    public MetricsCountingTests() => _store = _env.NewMetricsStore();

    public void Dispose() => _env.Dispose();

    private MetricsCounter Visit(DateOnly day) =>
        new(_browser, _store, NullLogger<MetricsCounter>.Instance, () => day);

    private DayMetrics On(DateOnly day) => _store.Read().Days[MetricsData.Key(day)];

    // ---- the note's rules ---------------------------------------------------------

    [Theory]
    [InlineData(null, VisitKind.New)]
    [InlineData(1, VisitKind.BackFromYesterday)]
    [InlineData(2, VisitKind.BackWithinWeek)]
    [InlineData(7, VisitKind.BackWithinWeek)]
    [InlineData(8, VisitKind.BackAfterLonger)]
    public void A_visit_is_sorted_by_how_long_since_the_last(int? daysAgo, VisitKind expected)
    {
        var note = new BrowserNote { LastVisit = daysAgo is { } ago ? Day.AddDays(-ago) : null };

        var (next, counted) = note.Visit(Day);

        Assert.Equal(expected, counted);
        Assert.Equal(Day, next.LastVisit);
    }

    [Theory]
    [InlineData(0)]   // already counted today
    [InlineData(1)]   // the clock went back: counted already, on a day that hasn't come
    public void A_browser_already_counted_isnt_counted_again(int daysAhead)
    {
        var note = new BrowserNote { LastVisit = Day.AddDays(daysAhead) };

        Assert.Null(note.Visit(Day).Counted);
    }

    [Fact]
    public void Attempts_build_up_to_one_solve_and_nothing_counts_after_it()
    {
        var note = new BrowserNote();

        (note, var first) = note.Answer(Day, Puzzle, correct: false);
        (note, var second) = note.Answer(Day, Puzzle, correct: false);
        (note, var third) = note.Answer(Day, Puzzle, correct: true);
        (_, var after) = note.Answer(Day, Puzzle, correct: true);

        Assert.Equal(new AnswerCounts(Saw: true, Tried: true, SolvedIn: null), first);
        Assert.Equal(AnswerCounts.Nothing, second);
        Assert.Equal(new AnswerCounts(Saw: false, Tried: false, SolvedIn: 3), third);
        Assert.Equal(AnswerCounts.Nothing, after);
    }

    [Fact]
    public void A_new_day_or_a_replaced_puzzle_starts_afresh()
    {
        var (solved, _) = new BrowserNote().Answer(Day, Puzzle, correct: true);

        Assert.Equal(1, solved.Answer(Day.AddDays(1), Puzzle, correct: true).Counted.SolvedIn);
        Assert.Equal(1, solved.Answer(Day, Guid.NewGuid(), correct: true).Counted.SolvedIn);
    }

    // ---- the counter over a stored note ----------------------------------------------

    [Fact]
    public async Task Five_reloads_count_one_visitor_and_the_next_day_counts_them_as_back()
    {
        for (var i = 0; i < 5; i++)
        {
            await Visit(Day).PuzzleSeenAsync(Puzzle);
        }
        await Visit(Day.AddDays(1)).PuzzleSeenAsync(Puzzle);

        Assert.Equal(1, On(Day).Visitors.New);
        Assert.Equal(1, On(Day).Visitors.Total);
        Assert.Equal(1, On(Day).Puzzles[Puzzle].Saw);
        Assert.Equal(1, On(Day.AddDays(1)).Visitors.BackFromYesterday);
        Assert.Equal(1, On(Day.AddDays(1)).Visitors.Total);
    }

    [Fact]
    public async Task Attempts_before_and_after_a_reload_add_up_to_one_solve()
    {
        var before = Visit(Day);
        await before.PuzzleSeenAsync(Puzzle);
        await before.AnswerAsync(Puzzle, correct: false);
        await before.AnswerAsync(Puzzle, correct: false);

        var after = Visit(Day);   // reloaded the page
        await after.PuzzleSeenAsync(Puzzle);
        await after.AnswerAsync(Puzzle, correct: true);

        var counts = On(Day).Puzzles[Puzzle];
        Assert.Equal((1, 1, 1), (counts.Saw, counts.Tried, counts.Solved));
        Assert.Equal(new[] { 0, 0, 1, 0, 0 }, counts.SolvedIn);
        Assert.Equal(3, counts.SolveAttempts);
    }

    [Fact]
    public async Task Solving_again_after_coming_back_adds_nothing()
    {
        await Visit(Day).AnswerAsync(Puzzle, correct: true);

        var back = Visit(Day);
        await back.AnswerAsync(Puzzle, correct: false);
        await back.AnswerAsync(Puzzle, correct: true);

        var counts = On(Day).Puzzles[Puzzle];
        Assert.Equal((1, 1), (counts.Tried, counts.Solved));
        Assert.Equal(1, counts.SolveAttempts);
    }

    [Fact]
    public async Task Long_solves_share_the_last_bucket_but_the_average_stays_exact()
    {
        var counter = Visit(Day);
        for (var i = 0; i < 6; i++)
        {
            await counter.AnswerAsync(Puzzle, correct: false);
        }
        await counter.AnswerAsync(Puzzle, correct: true);

        var counts = On(Day).Puzzles[Puzzle];
        Assert.Equal(new[] { 0, 0, 0, 0, 1 }, counts.SolvedIn);
        Assert.Equal(7, counts.SolveAttempts);
    }

    [Fact]
    public async Task A_puzzle_replaced_mid_day_keeps_its_own_numbers()
    {
        var replacement = Guid.NewGuid();
        var counter = Visit(Day);
        await counter.PuzzleSeenAsync(Puzzle);
        await counter.PuzzleSeenAsync(replacement);
        await counter.AnswerAsync(replacement, correct: true);

        Assert.Equal((1, 0), (On(Day).Puzzles[Puzzle].Saw, On(Day).Puzzles[Puzzle].Tried));
        Assert.Equal((1, 1), (On(Day).Puzzles[replacement].Saw, On(Day).Puzzles[replacement].Solved));
        Assert.Equal(1, On(Day).Visitors.Total);
    }

    [Fact]
    public async Task A_page_with_no_live_connection_counts_nothing()
    {
        // The plain HTML render a bot fetches: the browser's storage can't be reached, which
        // is exactly how the framework fails when there's no live connection.
        _browser.FailWith = new InvalidOperationException("JavaScript interop calls cannot be issued at this time.");

        var counter = Visit(Day);
        await counter.PuzzleSeenAsync(Puzzle);
        await counter.AnswerAsync(Puzzle, correct: true);
        await counter.TwentyFourAnswerAsync(solved: true);

        Assert.Empty(_store.Read().Days);
    }

    [Fact]
    public async Task A_note_that_cant_be_saved_isnt_counted()
    {
        // Saved first, counted second. Counting first would count again on every click, since
        // the browser would never remember it had been counted.
        _browser.FailWritesWith = new IOException("storage full");

        var counter = Visit(Day);
        await counter.PuzzleSeenAsync(Puzzle);
        await counter.PuzzleSeenAsync(Puzzle);

        Assert.Empty(_store.Read().Days);
    }

    [Fact]
    public async Task A_failure_to_count_never_reaches_the_page()
    {
        _browser.FailWith = new IOException("storage full");

        var counter = Visit(Day);
        var counting = counter.AnswerAsync(Puzzle, correct: true);

        await counting;   // throws if a failure escaped
        Assert.True(counting.IsCompletedSuccessfully);
        Assert.Empty(_store.Read().Days);
    }

    [Fact]
    public async Task Twenty_four_counts_a_visitor_only_once_they_submit()
    {
        var counter = Visit(Day);
        await counter.TwentyFourPassAsync();

        Assert.Equal(0, On(Day).Visitors.Total);   // passing isn't submitting
        Assert.Equal(0, On(Day).TwentyFour.Players);
        Assert.Equal(1, On(Day).TwentyFour.HandsPassed);

        await counter.TwentyFourAnswerAsync(solved: false);
        await counter.TwentyFourAnswerAsync(solved: true);
        await Visit(Day).TwentyFourAnswerAsync(solved: true);   // reloaded, played again

        Assert.Equal(1, On(Day).Visitors.Total);
        Assert.Equal(1, On(Day).TwentyFour.Players);
        Assert.Equal(2, On(Day).TwentyFour.HandsSolved);
    }
}
