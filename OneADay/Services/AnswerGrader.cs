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

    /// <summary>The day the cap was last reached and logged, so the warning comes once a day.</summary>
    private DateOnly _warnedFullOn;

    public async Task<bool> IsCorrectAsync(BrainTeaser teaser, string submission, CancellationToken cancellationToken = default)
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
        var settings = options.Value;
        if (judged.Find(teaser.Id, submission) is { } earlier)
        {
            // Judged against today's bar, not the one in force when Jev was asked.
            return settings.Passes(earlier.Chance);
        }

        if (!settings.IsOn || !teaser.AllowsSecondOpinion(submission) || judged.IsFull || !TakeTodaysSlot())
        {
            return false;
        }

        var chance = await judge.ChanceSameAnswerAsync(teaser.Question, teaser.AcceptedAnswers(), submission, cancellationToken);
        if (chance is not { } verdict)
        {
            // Not remembered: the same answer is asked about again next time.
            return false;
        }

        judged.Add(new JudgedAnswer
        {
            TeaserId = teaser.Id,
            Submission = submission.Trim(),
            Chance = verdict,
            JudgedAt = AppTime.NowPacific,
        });
        return settings.Passes(verdict);
    }

    /// <summary>
    /// Spends one of today's questions to Jev. Every question sent counts, answered or not: a
    /// timeout may still be billed. The count is in memory, so a restart starts the day afresh.
    /// </summary>
    private bool TakeTodaysSlot()
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
            _budget.RecordSent(today);
            return true;
        }
    }
}
