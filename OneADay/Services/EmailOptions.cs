namespace OneADay.Services;

/// <summary>
/// Where notification mail goes, and how to send it.
///
/// Everything here except <see cref="AppPassword"/> is safe to commit — the password
/// is a Gmail **app password** and must come from user-secrets locally or an
/// environment variable in production. It must never appear in appsettings.json.
/// </summary>
public sealed class EmailOptions
{
    public const string Section = "Email";

    /// <summary>
    /// Master switch.
    /// </summary>
    /// <remarks>
    /// The property default is <c>false</c>, which is what keeps <b>tests</b> silent —
    /// they construct options directly and never load appsettings.
    ///
    /// <b>appsettings.json sets it true</b>, so a local run with the app password
    /// present really does send. That is deliberate while this is a one-person project
    /// with a single machine and no deployment: local is the only place the app runs,
    /// so switching it off here would mean the feature is never exercised at all.
    ///
    /// Revisit when there is a real production environment. At that point the base
    /// config should be <c>false</c> and each environment should opt in, or a second
    /// developer will mail the author's live inbox with test data the moment they set
    /// a password.
    /// </remarks>
    public bool Enabled { get; set; }

    /// <summary>Where notifications are delivered — the author's inbox.</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// The sending address. With Gmail this must be the same account the app password
    /// belongs to; Gmail rewrites or rejects anything else.
    /// </summary>
    public string From { get; set; } = string.Empty;

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;

    /// <summary>
    /// Gmail **app password** (16 characters, requires 2FA on the account). A normal
    /// account password will not work — Google stopped accepting those in 2022.
    /// </summary>
    public string AppPassword { get; set; } = string.Empty;

    /// <summary>
    /// Most notification emails to send in one Pacific day. Issue reports are
    /// deliberately uncapped in the product (see PRD 06), so without a ceiling here a
    /// single frustrated visitor — or a bot that got through — could bury the inbox.
    /// Hitting the cap stops the mail, never the save.
    /// </summary>
    public int MaxPerDay { get; set; } = 25;

    /// <summary>
    /// Usable only when switched on and fully configured. Anything missing means the
    /// notifier quietly does nothing, which is what keeps dev, tests, and an
    /// unconfigured deployment working rather than erroring.
    /// </summary>
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(To)
        && !string.IsNullOrWhiteSpace(From)
        && !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(AppPassword);
}
