using Microsoft.Extensions.Options;
using OneADay.Models;

namespace OneADay.Services;

/// <summary>How many live connections one visitor address may hold open at once.</summary>
public sealed class LiveConnectionOptions
{
    public const string Section = "LiveConnections";

    /// <summary>
    /// Open live connections allowed per address. Zero or less switches the cap off.
    /// </summary>
    /// <remarks>
    /// Every open page holds one for as long as its tab stays open, so ten is ten tabs across a
    /// household. A school or office behind one address could need more; raise it if one ever
    /// does. Behind the whole site sits fly.toml's limit of 300.
    /// </remarks>
    public int MaxPerAddress { get; set; } = 10;
}

/// <summary>Counts each address's open live connections.</summary>
public sealed class LiveConnectionCounter(int maxPerAddress)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _open = [];

    /// <summary>Counts one more connection for the address. False, uncounted, when it's full.</summary>
    public bool TryOpen(string address)
    {
        lock (_gate)
        {
            var open = _open.GetValueOrDefault(address);
            if (open >= maxPerAddress)
            {
                return false;
            }
            _open[address] = open + 1;
            return true;
        }
    }

    /// <summary>Counts one connection closed. An address with none left is forgotten.</summary>
    public void Close(string address)
    {
        lock (_gate)
        {
            var open = _open.GetValueOrDefault(address);
            if (open <= 1)
            {
                _open.Remove(address);
            }
            else
            {
                _open[address] = open - 1;
            }
        }
    }
}

/// <summary>Wires <see cref="LiveConnectionOptions"/> into the request pipeline.</summary>
/// <remarks>
/// <para>Every open page keeps a live connection to the server, and the machine takes 300 at
/// once (fly.toml). Cloudflare's rate limit only slows how fast connections are opened, not how
/// many stay open, so one script could hold all 300 in about five minutes, and then no real
/// visitor could get in. With ten per address, filling the machine takes thirty addresses.
/// Security review, 2026-09-28.</para>
///
/// <para>A live connection is one long request to <see cref="HubPath"/> — the WebSocket, or a
/// held poll on older browsers — so counting that path's requests in flight counts connections.
/// The short ones beside it (negotiate, disconnect) count only for the moment they take.</para>
/// </remarks>
public static class LiveConnectionCap
{
    /// <summary>Where Blazor's live connection is served: /_blazor and the paths under it.</summary>
    public const string HubPath = "/_blazor";

    /// <summary>Turns away a live connection past an address's cap, with an empty 429.</summary>
    /// <remarks>
    /// <b>Register this after <see cref="ProxyHeaders.UseProxyHeaders"/></b>: above it, every
    /// visitor has Cloudflare's address, and the eleventh visitor anywhere would be refused.
    /// LiveConnectionCapTests pins the order against Program.cs.
    /// </remarks>
    public static WebApplication UseLiveConnectionCap(this WebApplication app)
    {
        var max = app.Services.GetRequiredService<IOptions<LiveConnectionOptions>>().Value.MaxPerAddress;
        if (max <= 0)
        {
            return app;
        }

        var counter = new LiveConnectionCounter(max);
        app.Use(async (context, next) =>
        {
            var address = context.Request.Path.StartsWithSegments(HubPath)
                ? VisitorAddress.Key(context.Connection.RemoteIpAddress)
                : null;
            if (address is null)
            {
                await next(context);
                return;
            }

            if (!counter.TryOpen(address))
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return;
            }

            try
            {
                await next(context);
            }
            finally
            {
                counter.Close(address);
            }
        });
        return app;
    }
}
