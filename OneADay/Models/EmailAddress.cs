using System.Net.Mail;

namespace OneADay.Models;

/// <summary>
/// Validates and normalises a visitor-supplied email address.
///
/// <para>Deliberately modest. It isn't trying to prove an address exists — the
/// confirmation email does that, and nothing is ever broadcast to an address that
/// didn't click it. It exists to reject input that is unusable or dangerous before it
/// is stored or placed in a mail header.</para>
/// </summary>
public static class EmailAddress
{
    /// <summary>RFC 5321's practical ceiling for a whole address.</summary>
    public const int MaxLength = 254;

    /// <summary>
    /// Returns the normalised address, or null if it can't be used.
    /// </summary>
    /// <remarks>
    /// Lower-cased throughout so the same person can't subscribe twice as Alice@ and
    /// alice@. The local part is technically case-sensitive, but no mainstream provider
    /// treats it that way and every mailing list normalises like this.
    ///
    /// Line breaks are rejected outright rather than stripped: this value becomes the
    /// <c>To</c> header, and a CR/LF there is header injection. <see cref="MailAddress"/>
    /// would also refuse it, but a value that dangerous shouldn't reach the store at all.
    /// </remarks>
    public static string? Normalise(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        // Checked on the RAW input, before trimming. Trim() treats CR and LF as ordinary
        // whitespace, so trimming first would quietly strip a trailing line break and let
        // the value through — safe in that one case, but it breaks the rule stated above,
        // and "reject, never repair" is the only version of this that's easy to trust.
        if (raw.Any(char.IsControl))
        {
            return null;   // CR/LF and friends — never let them near a header
        }

        var candidate = raw.Trim();   // now only ordinary spaces can be removed

        if (candidate.Length > MaxLength || candidate.Any(char.IsWhiteSpace))
        {
            return null;   // a space inside the address
        }

        var at = candidate.IndexOf('@');
        if (at <= 0 || at != candidate.LastIndexOf('@') || at == candidate.Length - 1)
        {
            return null;   // exactly one @, with something on both sides
        }

        var domain = candidate[(at + 1)..];
        if (!domain.Contains('.') || domain.StartsWith('.') || domain.EndsWith('.'))
        {
            return null;   // "user@localhost" is valid mail syntax and useless here
        }

        if (!MailAddress.TryCreate(candidate, out var parsed)
            || !string.Equals(parsed.Address, candidate, StringComparison.OrdinalIgnoreCase))
        {
            // The second check catches display-name forms like "Bob <bob@x.com>", which
            // MailAddress accepts and quietly reduces to the address inside.
            return null;
        }

        return candidate.ToLowerInvariant();
    }

    /// <summary>
    /// A partly hidden address for display — <c>d****g@gmail.com</c> — so a page can
    /// confirm which subscription it's talking about without printing the whole thing
    /// to whoever holds the link.
    /// </summary>
    public static string Mask(string address)
    {
        var at = address.IndexOf('@');
        if (at <= 0)
        {
            return "your address";
        }

        var local = address[..at];
        var domain = address[at..];
        var shown = local.Length <= 2
            ? local[..1] + new string('*', Math.Max(1, local.Length - 1))
            : local[0] + new string('*', local.Length - 2) + local[^1];
        return shown + domain;
    }
}
