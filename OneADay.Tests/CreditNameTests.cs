using OneADay.Models;

namespace OneADay.Tests;

/// <summary>
/// The name a suggested teaser is credited to (PRD 06): typed by a visitor, shown to everyone on
/// the day their teaser runs, so cleaned the same way wherever it's saved or shown.
/// </summary>
public class CreditNameTests
{
    [Theory]
    [InlineData("Sam Rivera", "Sam Rivera")]
    [InlineData("  Sam \r\n\tRivera  ", "Sam Rivera")]       // line breaks and runs of space
    [InlineData("Sam\u2028Rivera", "Sam Rivera")]             // a line separator .NET calls space
    [InlineData("Sam\u3164Rivera", "Sam Rivera")]             // a Hangul filler between words
    [InlineData("\u202ESam", "Sam")]                         // a bidi override, which would flip the line
    [InlineData("Sa\u200Bm\u0000", "Sam")]                   // a zero-width space and a control character
    [InlineData("José Núñez", "José Núñez")]
    [InlineData("李明", "李明")]
    [InlineData("42", "42")]
    public void A_name_keeps_what_shows_on_one_line(string typed, string kept)
    {
        Assert.Equal(kept, CreditName.Clean(typed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u3164")]           // Hangul filler: a letter to Unicode, not space to .NET
    [InlineData("\u115F\u1160")]     // the other Hangul fillers
    [InlineData("\uFFA0")]           // and the halfwidth one
    [InlineData("\u2800")]           // the blank braille cell
    [InlineData("\u200B\u200B")]     // zero-width spaces
    [InlineData("\u0000\u001B")]     // control characters
    [InlineData("...")]
    [InlineData("\U0001F986")]       // a duck emoji: no letter or digit, so no name
    public void A_name_with_nothing_to_show_is_none(string? typed)
    {
        // Each of these got past the first version's checks and printed "Today's question was
        // given by" with nothing after it (cold review, 2026-10-30).
        Assert.Null(CreditName.Clean(typed));
    }

    [Fact]
    public void A_name_is_cut_to_the_forms_limit()
    {
        Assert.Equal(new string('a', CreditName.MaxLength), CreditName.Clean(new string('a', 40_000)));
    }
}
