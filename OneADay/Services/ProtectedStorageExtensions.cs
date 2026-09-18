using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Security.Cryptography;
using System.Text.Json;

namespace OneADay.Services;

/// <summary>
/// Reading <see cref="ProtectedLocalStorage"/> without letting an unreadable value take the
/// page down with it.
/// </summary>
/// <remarks>
/// <para><see cref="ProtectedLocalStorage.GetAsync{TValue}(string)"/> does not catch
/// decryption failures. When a stored value cannot be unprotected it throws a
/// <see cref="CryptographicException"/> out of <c>OnAfterRenderAsync</c>, which is unhandled,
/// which <b>kills the visitor's circuit</b> — their page stops working. The
/// <c>Success = false</c> result that the call site looks like it is handling is only
/// returned when nothing is stored at all.</para>
///
/// <para>That turns a routine event into an outage. A browser holds a value encrypted with a
/// key the server no longer has whenever the Data Protection key ring is rotated, restored
/// from a backup, or moved — and moving it is exactly what PRD 11 requires in order to stop
/// the ring being lost on every restart. Anyone carrying a value from before the move would
/// meet a broken page rather than being quietly treated as a new visitor.</para>
///
/// <para>Treating an unreadable value as "no value" is the honest reading: the server cannot
/// know what it said, and every caller here already has a correct answer for a visitor it has
/// never seen — mint a new id, assume the nudge was not dismissed. The stale value is
/// overwritten on the next write.</para>
/// </remarks>
public static class ProtectedStorageExtensions
{
    /// <summary>
    /// The stored value, or <c>default</c> when there is nothing stored <i>or</i> what is
    /// stored can no longer be read.
    /// </summary>
    public static async ValueTask<TValue?> ReadOrDefaultAsync<TValue>(
        this ProtectedLocalStorage storage, string key)
    {
        try
        {
            var stored = await storage.GetAsync<TValue>(key);
            return stored.Success ? stored.Value : default;
        }
        catch (CryptographicException)
        {
            // Encrypted with a key this server doesn't have: rotated, restored or moved.
            return default;
        }
        catch (FormatException)
        {
            // Defensive breadth, and honestly labelled: no test reaches this. A malformed
            // entry was expected to fail base64 decoding here, but mutation testing showed
            // the observed path is CryptographicException above — rethrowing from this block
            // changes no test result. Kept because the cost is a line and the failure it
            // would cover is the same kind: a stored value that cannot be read.
            return default;
        }
        catch (JsonException)
        {
            // Decrypted, but no longer the shape this code expects — after a stored type
            // changes shape across a deploy. Also unexercised; same reasoning as above.
            return default;
        }
    }
}
