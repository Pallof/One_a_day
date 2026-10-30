namespace OneADay.Models;

/// <summary>
/// A Twenty Four player: the random id their browser keeps, and the number the server gave them,
/// in order, the first time they got a hand right (PRD 07). Kept together, encrypted, in their
/// browser; on the board with their name beside them.
/// </summary>
public sealed record TwentyFourPlayer(Guid Id, int Number)
{
    /// <summary>
    /// The name they go by, such as "Otter 17". Saved with every board row, so the board file can
    /// be read by eye; worked out afresh each time, never read back.
    /// </summary>
    public string Name => PlayerName.For(Id, Number);
}

/// <summary>
/// The name a Twenty Four player goes by on the scoreboard: an animal from their id and their
/// number, such as "Otter 17" (PRD 07).
/// </summary>
/// <remarks>
/// Players can't choose their name, so nothing a visitor types is ever shown to the others and
/// the board never needs moderating (author's decision, 2026-10-29). No two players share a name,
/// because no two share a number: the server gives them out in order (author's decision,
/// 2026-10-29; until then the number came from the id too, and names could repeat).
/// </remarks>
public static class PlayerName
{
    /// <summary>
    /// Every animal a player can be. Changing the list changes everyone's animal, though not their
    /// number. Each word is a plain animal with no second meaning on a public page.
    /// </summary>
    public static readonly string[] Animals =
    [
        "Duck", "Otter", "Badger", "Heron", "Fox", "Owl", "Hare", "Wren",
        "Robin", "Finch", "Puffin", "Lynx", "Panda", "Koala", "Gecko", "Newt",
        "Llama", "Bison", "Crane", "Goose", "Swan", "Falcon", "Hedgehog", "Walrus",
        "Marmot", "Lemur", "Tapir", "Ibex", "Seal", "Moose", "Penguin", "Sparrow",
    ];

    public static string For(Guid player, int number)
    {
        // The id's first bytes are random. Its middle bytes hold its version and variant, the
        // same in every id, and would make every animal alike.
        var animal = Animals[BitConverter.ToUInt32(player.ToByteArray(), 0) % (uint)Animals.Length];
        return $"{animal} {number}";
    }
}
