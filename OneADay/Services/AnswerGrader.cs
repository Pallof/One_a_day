using Microsoft.Extensions.Options;
using OneADay.Models;

namespace OneADay.Services;

/// <summary>
/// Whether a typed answer is right (PRD 02). The checker decides first. Only an answer it turns
/// down can go to Jev for a second opinion, and only when the question allows one.
/// </summary>
public sealed class AnswerGrader(
    IAnswerJudge judge, JudgedAnswerStore judged, IOptions<JevOptions> options, ILogger<AnswerGrader> log)
{
    private readonly object _budgetLock = new();
    private readonly DailySendBudget _budget = new(options.Value.MaxPerDay);
    private readonly DailyAddressAllowance _perAddress = new(options.Value.MaxPerAddressPerDay);

    /// <summary>The days the caps were last reached and logged, so each warning comes once a day.</summary>
    private DateOnly _warnedFullOn;
    private DateOnly _warnedAddressOn;

    /// <param name="visitor">The visitor's address, from <see cref="VisitorAddress.Key"/>, for
    /// their share of today's questions to Jev. Null when unknown: every unknown visitor
    /// shares one share.</param>
    public async Task<bool> IsCorrectAsync(
        BrainTeaser teaser, string submission, string? visitor = null, CancellationToken cancellationToken = default)
    {
        if (teaser.AcceptsAnswer(submission))
        {
            return true;
        }

        // The author's ruling beats any verdict of Jev's, earlier or not.
        if (teaser.RulesOut(submission))
        {
            return false;
        }

        // Switched off, Jev's earlier verdicts go too: the checker alone decides. And the rules
        // on what may be asked apply to an earlier verdict as much as to a new question, so a
        // verdict kept for one answer can never be reused for one Jev couldn't be asked about.
        var settings = options.Value;
        if (!settings.IsOn || !teaser.AllowsSecondOpinion(submission))
        {
            return false;
        }

        if (judged.Find(teaser.Id, submission) is { } earlier && earlier.StillStandsFor(teaser))
        {
            // Judged against today's bar, not the one in force when Jev was asked.
            return settings.Passes(earlier.Chance);
        }

        if (!TakeTodaysSlot(visitor))
        {
            return false;
        }

        var chance = await judge.ChanceSameAnswerAsync(teaser.Question, teaser.AcceptedAnswers(), submission, cancellationToken);
        if (chance is not { } verdict)
        {
            // Not remembered: the same answer is asked about again next time.
            return false;
        }

        try
        {
            judged.Add(new JudgedAnswer
            {
                TeaserId = teaser.Id,
                Submission = submission.Trim(),
                Chance = verdict,
                Against = teaser.Fingerprint(),
                JudgedAt = AppTime.NowPacific,
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A full disk, say. Paid for and answered, so the visitor still gets the verdict: a
            // second opinion must never break the page (PR 74 review). It's held in memory, so
            // it's reused until the next restart. Not which answer: the log is no place for it.
            log.LogWarning("Couldn't save a verdict of Jev's ({Error}). It's kept until the next restart.", e.GetType().Name);
        }
        return settings.Passes(verdict);
    }

    /// <summary>
    /// Spends one of today's questions to Jev, from the site's budget and the visitor's own
    /// share of it at once. Every question sent counts, answered or not: a timeout may still be
    /// billed. The counts are in memory, so a restart starts the day afresh.
    /// </summary>
    private bool TakeTodaysSlot(string? visitor)
    {
        lock (_budgetLock)
        {
            var today = AppTime.Today;
            if (!_budget.HasRoom(today))
            {
                if (_warnedFullOn != today)
                {
                    _warnedFullOn = today;
                    log.LogWarning("Jev has been asked {Max} times today, the daily cap. The checker alone " +
                                   "decides until midnight Pacific.", _budget.MaxPerDay);
                }
                return false;
            }
            if (!_perAddress.HasRoom(visitor, today))
            {
                if (_warnedAddressOn != today)
                {
                    // Not which address: the log is no place to keep one.
                    _warnedAddressOn = today;
                    log.LogWarning("A visitor address has asked Jev {Max} times today, its share. Its answers " +
                                   "go to the checker alone until midnight Pacific.", _perAddress.MaxPerDay);
                }
                return false;
            }
            _budget.RecordSent(today);
            _perAddress.Record(visitor, today);
            return true;
        }
    }
}
