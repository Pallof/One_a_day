using OneADay.Components.Pages;

namespace OneADay.Services;

/// <summary>
/// Where the admin page exists: on the author's machine, and nowhere else.
/// </summary>
/// <remarks>
/// <para>The live site doesn't lock <c>/admin</c> — it doesn't have one. A lock can have a
/// flaw: a guessable passphrase, one admin action that forgets to check it, and nobody
/// watching a one-person site for someone working at it. A page that is never built has
/// no buttons for a script to press. See PRD 10.</para>
///
/// <para><b>Two layers, because there are two ways to reach a page.</b> A browser asking
/// the server for <c>/admin</c> is turned away by <see cref="UseAdminOnlyInDevelopment"/>
/// before anything renders. But a click inside a page that's already open never asks the
/// server for a URL — Blazor routes it inside the live connection — so the router checks
/// <see cref="Blocks"/> as well. Either layer alone leaves the other door open.</para>
/// </remarks>
public static class AdminAccess
{
    private static readonly PathString AdminPath = new("/admin");

    /// <summary>True only in Development, which is how the app runs on the author's Mac.</summary>
    /// <remarks>
    /// Fails closed: Production, Staging, or a typo in the environment name all mean no
    /// admin page.
    /// </remarks>
    public static bool IsAvailable(IHostEnvironment env) => env.IsDevelopment();

    /// <summary>True when the router must not build <paramref name="pageType"/> here.</summary>
    public static bool Blocks(Type? pageType, IHostEnvironment env) =>
        pageType == typeof(Admin) && !IsAvailable(env);

    /// <summary>
    /// Outside Development, answers every request under <c>/admin</c> with 404. Register it
    /// after <c>UseStatusCodePagesWithReExecute</c>, so visitors get the site's ordinary
    /// not-found page rather than a blank one.
    /// </summary>
    public static WebApplication UseAdminOnlyInDevelopment(this WebApplication app)
    {
        if (IsAvailable(app.Environment))
        {
            return app;
        }

        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(AdminPath, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            await next(context);
        });
        return app;
    }
}
