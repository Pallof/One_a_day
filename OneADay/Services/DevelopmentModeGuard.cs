namespace OneADay.Services;

/// <summary>
/// Stops the app starting in Development mode anywhere but the author's own computer.
/// </summary>
/// <remarks>
/// <para>Development mode is what switches the admin page on (see <see cref="AdminAccess"/>),
/// so a server started in it would put every future answer on the public internet. The
/// realistic ways that happens are accidents, and each one has a tell:</para>
/// <list type="bullet">
/// <item>A host with <c>ASPNETCORE_ENVIRONMENT=Development</c> set — often copied from a
/// tutorial, to see detailed errors — running the published app. <b>Published apps are
/// Release builds</b>, and a Release build refuses Development outright.</item>
/// <item>A server that clones the repository and uses <c>dotnet run</c>, which switches
/// Development on from <c>launchSettings.json</c> without anyone choosing it. That's a
/// Debug build, so it takes the second check: <b>the machine must be marked as the
/// author's</b>, by a flag kept in user-secrets — on the author's computer, outside the
/// repository, and never in a publish.</item>
/// </list>
/// <para>Either check alone leaves one of those accidents open. The refusal happens before
/// the app is built, so a refused start never opens a port. Checking where visitors connect
/// from was considered and rejected: behind a reverse proxy or a tunnel, every visitor
/// arrives from localhost. See PRD 10.</para>
/// </remarks>
public static class DevelopmentModeGuard
{
    /// <summary>The user-secrets flag that marks the author's computer. See PRD 10.</summary>
    public const string MarkerKey = "AuthorsMachine";

    /// <summary>Whether this assembly was compiled as a Debug build.</summary>
    public static bool IsDebugBuild =>
#if DEBUG
        true;
#else
        false;
#endif

    /// <summary>Why the app must not start, or null when it may.</summary>
    /// <remarks>
    /// The messages say how to fix a server, but deliberately don't print the command that
    /// marks a machine: whoever reads a refusal may be standing on the server, and a
    /// paste-ready way to silence it is the wrong thing to hand them.
    /// </remarks>
    public static string? ReasonToRefuse(IHostEnvironment env, IConfiguration config, bool isDebugBuild)
    {
        if (!env.IsDevelopment())
        {
            return null;   // Production, Staging, anything else: the admin page is already off
        }

        const string risk = "Development mode switches on the admin page — every future answer " +
                            "included — for anyone who can reach this app.";

        if (!isDebugBuild)
        {
            return $"Refusing to start in Development mode. {risk} This is a Release build, " +
                   "which is what gets deployed, so it must run in Production: remove " +
                   "ASPNETCORE_ENVIRONMENT=Development (or DOTNET_ENVIRONMENT, or " +
                   "--environment Development) and start it again. See PRD 10.";
        }

        if (!(bool.TryParse(config[MarkerKey], out var marked) && marked))
        {
            return "Refusing to start in Development mode on a machine that isn't marked as " +
                   $"the author's computer. {risk} If this is a server, run it in Production: " +
                   "remove ASPNETCORE_ENVIRONMENT=Development (or DOTNET_ENVIRONMENT, or " +
                   "--environment Development). Note that `dotnet run` switches Development on " +
                   "by itself, from launchSettings.json — deploy a published build instead. If " +
                   "this is the author's own computer, the one-time setup is in PRD 10.";
        }

        return null;
    }
}
