using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OneADay.Models;

/// <summary>
/// A name to credit a suggested teaser to: typed by a visitor on the Suggest a teaser form
/// (PRD 06), and shown to everyone on the day their teaser runs (PRD 01).
/// </summary>
/// <remarks>
/// Cleaned the same way wherever it's saved or shown. Line breaks and runs of space become one
/// space, control and formatting characters (zero-width spaces, bidi overrides) go, and what's
/// left counts as a name only with a letter or a digit in it. Without that last rule a name made
/// of blank-looking characters got past every check and printed "given by" with nothing after
/// it: the Hangul filler, for one, is a letter to Unicode and not space to .NET (cold review,
/// 2026-10-30). So a name of only emoji or punctuation counts as none too.
/// </remarks>
public static partial class CreditName
{
    /// <summary>The longest name the form takes. The server holds to it too.</summary>
    public const int MaxLength = 60;

    /// <summary>
    /// Characters that draw nothing yet count as letters or symbols, so neither <c>\s</c> nor
    /// <see cref="char.IsWhiteSpace(char)"/> catches them: the Hangul fillers and the blank
    /// braille cell. Read as spaces.
    /// </summary>
    private static readonly HashSet<int> Blanks = [0x115F, 0x1160, 0x3164, 0xFFA0, 0x2800];

    /// <summary>The name, cleaned and cut to <see cref="MaxLength"/>, or null if there's no name in it.</summary>
    public static string? Clean(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var kept = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune) || Blanks.Contains(rune.Value))
            {
                kept.Append(' ');
            }
            else if (Rune.GetUnicodeCategory(rune) is not (UnicodeCategory.Control or UnicodeCategory.Format))
            {
                kept.Append(rune.ToString());
            }
        }

        var name = TextLimit.Clip(Spaces().Replace(kept.ToString(), " "), MaxLength);
        return name.EnumerateRunes().Any(Rune.IsLetterOrDigit) ? name : null;
    }

    [GeneratedRegex(" {2,}")]
    private static partial Regex Spaces();
}
