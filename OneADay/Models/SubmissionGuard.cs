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
    /// The floor for a form with a single autofillable field. See <see cref="LooksAutomated(string?, TimeSpan, TimeSpan)"/>.
    /// </summary>
    public static readonly TimeSpan SingleFieldFloor = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// True when the submission should be dropped. Uses the default compose floor.
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
        LooksAutomated(honeypot, elapsed, TimeSpan.FromSeconds(MinComposeSeconds));

    /// <summary>
    /// True when the submission should be dropped, with a floor chosen by the form.
    /// </summary>
    /// <remarks>
    /// The floor has to fit the form. Five seconds is right for writing a riddle and its
    /// solution, and wrong for an email field: with browser autofill a real person can
    /// click, pick their address and press Enter in two seconds. Held to five, they would
    /// be silently dropped — shown the ordinary "check your inbox" and then never sent
    /// anything, which is worse than an error. A short floor still catches the naive
    /// scripts this exists for, which submit in milliseconds.
    /// </remarks>
    public static bool LooksAutomated(string? honeypot, TimeSpan elapsed, TimeSpan minimum) =>
        !string.IsNullOrWhiteSpace(honeypot) || elapsed < minimum;
}
