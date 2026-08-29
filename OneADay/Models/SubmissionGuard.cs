namespace OneADay.Models;

/// <summary>
/// Cheap, silent checks that a form submission came from a person rather than a script.
///
/// Neither check costs a real visitor anything — there is no puzzle to solve and no
/// extra click. They are deliberately weak on their own: the point is to price out
/// naive automation, not to stop someone determined. See the daily caps in
/// <see cref="OneADay.Services.SuggestionStore"/> for the layer that actually limits volume.
/// </summary>
public static class SubmissionGuard
{
    /// <summary>
    /// How long a genuine visitor needs, at minimum, to compose a teaser and its
    /// solution. Anything faster was typed by something that already had the text.
    /// </summary>
    public const int MinComposeSeconds = 5;

    /// <summary>
    /// True when the submission should be dropped.
    /// </summary>
    /// <param name="honeypot">
    /// Contents of the decoy field. It is hidden from view, skipped by the tab order,
    /// and hidden from screen readers, so a person never fills it in — but a script
    /// that walks the DOM filling every input will.
    /// </param>
    /// <param name="elapsed">
    /// Time since the form became interactive. Measured on the server from a timestamp
    /// the client never sees, so it cannot be forged by replaying a request.
    /// </param>
    public static bool LooksAutomated(string? honeypot, TimeSpan elapsed) =>
        !string.IsNullOrWhiteSpace(honeypot) || elapsed < TimeSpan.FromSeconds(MinComposeSeconds);
}
