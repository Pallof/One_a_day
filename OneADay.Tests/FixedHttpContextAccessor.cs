using System.Net;
using Microsoft.AspNetCore.Http;

namespace OneADay.Tests;

/// <summary>
/// A fixed HttpContext, so per-address limits are reachable in component tests.
/// </summary>
/// <remarks>
/// The real <see cref="HttpContextAccessor"/> is backed by an AsyncLocal that is empty under
/// bUnit, which makes the visitor's address null and silently disables every per-address gate.
/// A test that can't reach the gate it claims to test is worse than no test — it passes for the
/// wrong reason.
/// </remarks>
internal sealed class FixedHttpContextAccessor(HttpContext? context) : IHttpContextAccessor
{
    public HttpContext? HttpContext { get => context; set { } }

    /// <summary>An accessor whose request comes from <paramref name="address"/>.</summary>
    public static FixedHttpContextAccessor From(string address)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        return new FixedHttpContextAccessor(context);
    }
}
