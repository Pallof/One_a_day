namespace OneADay.Components;

/// <summary>
/// About how many lines a question takes in the laptop layout's left column, on today's
/// challenge and on yesterday's solution (PRD 09, "Fitting on one screen"). A long question's
/// picture gives up that much height so the page still fits the window; the CSS reads the
/// count from <c>--q-lines</c>.
/// </summary>
public static class QuestionLines
{
    /// <summary>
    /// The left column is the same width on every laptop, and measured over the bank no question
    /// runs more than 50 characters a line on either page, so dividing by 50 never undercounts
    /// (2026-10-23).
    /// </summary>
    public const int CharsPerLine = 50;

    public static int Of(string question) =>
        Math.Max(1, (int)Math.Ceiling(question.Length / (double)CharsPerLine));
}
