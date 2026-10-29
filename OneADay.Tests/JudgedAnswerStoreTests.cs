using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// Where Jev's verdicts are kept (PRD 02): a full store makes room instead of turning Jev off,
/// a verdict on an edited question is replaced, and answers are written as they were typed.
/// </summary>
public sealed class JudgedAnswerStoreTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    private static readonly Guid QuestionId = Guid.Parse("3b9c1f0e-5d7a-4e21-9c4b-8f2a6d1e7c30");

    private static JudgedAnswer Verdict(string typed, string against = "AAAAAAAAAAAAAAAA", int minute = 0) => new()
    {
        TeaserId = QuestionId,
        Submission = typed,
        Chance = 0.9,
        Against = against,
        JudgedAt = new DateTime(2026, 10, 28, 8, minute, 0),
    };

    [Fact]
    public void A_full_store_drops_its_oldest_verdict_to_make_room()
    {
        // It used to stop taking verdicts, which switched Jev off for good: nothing else ever
        // empties the file, and a flood could fill it in about a month.
        var store = new JudgedAnswerStore(_env, capacity: 3);
        foreach (var (typed, minute) in new[] { ("one", 1), ("two", 2), ("three", 3), ("four", 4) })
        {
            store.Add(Verdict(typed, minute: minute));
        }

        Assert.Equal(3, store.Count);
        Assert.Null(store.Find(QuestionId, "one"));
        Assert.NotNull(store.Find(QuestionId, "four"));
        Assert.Equal(["four", "three", "two"], new JudgedAnswerStore(_env, capacity: 3).GetAll().Select(v => v.Submission));
    }

    [Fact]
    public void A_verdict_on_the_question_before_an_edit_is_replaced_not_added()
    {
        var store = new JudgedAnswerStore(_env);
        store.Add(Verdict("near the front", against: "AAAAAAAAAAAAAAAA"));

        store.Add(Verdict("Near the front!", against: "BBBBBBBBBBBBBBBB"));
        store.Add(Verdict("near the front", against: "BBBBBBBBBBBBBBBB"));   // two visitors at once: the first stands

        Assert.Equal(1, store.Count);
        Assert.Equal("Near the front!", store.Find(QuestionId, "near the front")!.Submission);
    }

    [Fact]
    public void An_answer_with_no_letters_or_digits_is_never_kept()
    {
        var store = new JudgedAnswerStore(_env);

        store.Add(Verdict("⌨️"));

        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Answers_are_written_as_typed()
    {
        // Escaped as \uXXXX, a full file of other scripts was four times the size.
        new JudgedAnswerStore(_env).Add(Verdict("à côté du clavier"));

        Assert.Contains("à côté du clavier", File.ReadAllText(Path.Combine(_env.ContentRootPath, "App_Data", JudgedAnswerStore.FileName)));
    }

    public void Dispose() => _env.Dispose();
}
