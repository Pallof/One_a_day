using Bunit;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// Reading a stored value that can no longer be read.
///
/// <para>This is not a hypothetical. <c>ProtectedLocalStorage.GetAsync</c> throws rather than
/// reporting failure, and an exception out of <c>OnAfterRenderAsync</c> kills the visitor's
/// circuit — their page stops working. Moving the Data Protection key ring to
/// <c>App_Data/keys/</c> made every browser holding an older value hit exactly that, which is
/// how this was found (PRD 11, requirement 4).</para>
///
/// <para>The same thing happens on any key rotation or restore from backup, so the page must
/// survive it rather than the deployment having to avoid it.</para>
/// </summary>
public class ProtectedStorageTests : BunitContext
{
    private const string Key = "oad-visitor-id";

    /// <summary>
    /// A real <see cref="ProtectedLocalStorage"/> over bUnit's JS runtime. Everything is
    /// registered before the first service is read — bUnit locks its container otherwise.
    /// </summary>
    private ProtectedLocalStorage Storage()
    {
        Services.AddDataProtection();
        JSInterop.Mode = JSRuntimeMode.Loose;
        return new ProtectedLocalStorage(
            Services.GetRequiredService<IJSRuntime>(),
            Services.GetRequiredService<IDataProtectionProvider>());
    }

    [Fact]
    public async Task A_value_this_server_cannot_read_comes_back_as_no_value()
    {
        // The failure that took the page down: something is stored, but this server can't
        // make sense of it. The visitor should look new, not meet a dead circuit.
        var storage = Storage();
        JSInterop.Setup<string>("localStorage.getItem", _ => true)
                 .SetResult("this is not a protected value");

        var value = await storage.ReadOrDefaultAsync<Guid>(Key);

        Assert.Equal(Guid.Empty, value);
    }

    [Fact]
    public async Task Nothing_stored_comes_back_as_no_value()
    {
        var storage = Storage();
        JSInterop.Setup<string>("localStorage.getItem", _ => true).SetResult(null!);

        Assert.Equal(Guid.Empty, await storage.ReadOrDefaultAsync<Guid>(Key));
    }

    [Fact]
    public async Task A_readable_value_still_comes_back()
    {
        // The control case, and the one that matters: without it, a helper that swallowed
        // everything and always returned default would pass both tests above — and would
        // silently give every returning visitor a new identity on every page load.
        var storage = Storage();
        var written = Guid.NewGuid();

        await storage.SetAsync(Key, written);

        // Feed back exactly what was handed to localStorage.setItem.
        var stored = (string)JSInterop.Invocations["localStorage.setItem"].Single().Arguments[1]!;
        JSInterop.Setup<string>("localStorage.getItem", _ => true).SetResult(stored);

        Assert.Equal(written, await storage.ReadOrDefaultAsync<Guid>(Key));
    }
}
