namespace OneADay.Services;

/// <summary>
/// Facts about where the site lives, for anything that has to build a link that works
/// outside the browser — which today means links inside emails.
/// </summary>
public sealed class SiteOptions
{
    public const string Section = "Site";

    /// <summary>
    /// The public address, no trailing slash — e.g. <c>https://oneaday.example</c>.
    /// </summary>
    /// <remarks>
    /// A page can use relative links because the browser already knows the host. An
    /// email can't: it is read in a mail client with no base to resolve against, so
    /// every confirm and unsubscribe link has to be absolute. This must be the address
    /// a subscriber can actually reach — localhost only works on the author's machine,
    /// and a wrong value here silently breaks every unsubscribe link ever sent.
    /// </remarks>
    public string BaseUrl { get; set; } = "http://localhost:5178";

    /// <summary>Joins a site-relative path onto the base, tolerating stray slashes.</summary>
    public string Link(string relative) =>
        $"{BaseUrl.TrimEnd('/')}/{relative.TrimStart('/')}";
}
