using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using OneADay.Models;

namespace OneADay.Services;

/// <summary>Where a Twenty Four player is kept, so they keep their name between visits.</summary>
public interface ITwentyFourPlayerStorage
{
    /// <summary>The stored player, or null when there's none.</summary>
    ValueTask<TwentyFourPlayer?> ReadAsync();
    ValueTask WriteAsync(TwentyFourPlayer player);
}

/// <summary>
/// The player, id and number, in the browser's own storage, encrypted by this server, so a
/// visitor can't pick an id or a number, and with it someone else's name or row (PRD 07).
/// </summary>
/// <remarks>
/// A key of its own, not the suggestion form's visitor id, so the board can't be matched to
/// suggestions.
/// </remarks>
public sealed class ProtectedTwentyFourPlayerStorage(ProtectedLocalStorage storage) : ITwentyFourPlayerStorage
{
    public const string Key = "oad-24-player";

    /// <summary>
    /// An unreadable player reads as none: a new player, never an error. That includes an id kept
    /// before players had numbers (2026-10-29), which no longer reads as a player.
    /// </summary>
    public ValueTask<TwentyFourPlayer?> ReadAsync() => storage.ReadOrDefaultAsync<TwentyFourPlayer>(Key);

    public ValueTask WriteAsync(TwentyFourPlayer player) => storage.SetAsync(Key, player);
}
