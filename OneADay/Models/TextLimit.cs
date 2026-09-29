namespace OneADay.Models;

/// <summary>
/// Holds visitor text to the length its form shows.
/// </summary>
/// <remarks>
/// <para>A textarea's <c>maxlength</c> binds the browser, not the server. These forms run over
/// the live connection, and a script driving it can bind about 32,000 characters to a field in
/// one message (SignalR's default limit), which Blazor hands over as it arrived. In the security
/// review of 2026-09-28, 500 reports that size made a 91 MB file, and about 800 would have run
/// the 512 MB server out of memory, because every save rewrites the whole file.</para>
///
/// <para>So every stored field is cut on the server to the same number the page shows, the way
/// <c>ChallengeView</c> already cuts answers. Cut rather than refused: an honest browser can
/// never send more, so nobody real ever loses a character.</para>
/// </remarks>
public static class TextLimit
{
    /// <summary>Trims <paramref name="text"/>, then cuts it to <paramref name="max"/> characters.</summary>
    public static string Clip(string? text, int max)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);

        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length <= max)
        {
            return trimmed;
        }

        // Never end on half of a pair: an emoji is two characters to .NET, and half of one
        // isn't valid text.
        var end = char.IsHighSurrogate(trimmed[max - 1]) ? max - 1 : max;
        return trimmed[..end].TrimEnd();
    }
}
