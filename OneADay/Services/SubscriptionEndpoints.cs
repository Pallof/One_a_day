namespace OneADay.Services;

/// <summary>
/// The one subscription URL that isn't a page. Lives here rather than inline in
/// Program.cs so the HTTP tests map exactly what the app maps.
/// </summary>
public static class SubscriptionEndpoints
{
    /// <summary>
    /// One-click unsubscribe (RFC 8058). Gmail, Outlook and Apple Mail show their own
    /// "Unsubscribe" control for mail carrying List-Unsubscribe-Post, and clicking it sends
    /// a POST here — the visitor never sees the site.
    /// </summary>
    /// <remarks>
    /// <para>Antiforgery is off for this one endpoint, and that is safe: CSRF defends
    /// against a hostile page making the browser act with credentials it already holds, but
    /// this endpoint takes no credentials — the 256-bit token in the URL IS the
    /// authorisation, and an attacker who doesn't have it can't use it. Always answers 200,
    /// so it can't be used to test which tokens exist.</para>
    ///
    /// <para><b>Why the order matters.</b> The /unsubscribe page accepts POSTs too — Blazor
    /// maps every page for form posts — so without a lower order, one method on one path
    /// matches two endpoints. That is an AmbiguousMatchException: a 500 to every mail
    /// client's Unsubscribe button, while the page itself still looks fine in a browser.
    /// GETs are unaffected; this endpoint only claims POST.</para>
    /// </remarks>
    public static IEndpointRouteBuilder MapOneClickUnsubscribe(this IEndpointRouteBuilder app)
    {
        app.MapPost("/unsubscribe", (string? token, SubscriberStore store) =>
            {
                store.Unsubscribe(token);
                return Results.Ok();
            })
            .DisableAntiforgery()
            .WithOrder(-1);   // beat the page's own POST route — see remarks

        return app;
    }
}
