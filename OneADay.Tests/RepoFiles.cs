using System.Buffers.Binary;

namespace OneADay.Tests;

/// <summary>
/// The site's own files, for tests that check them directly: the pictures in wwwroot, and
/// App.razor, the page shell, which only renders inside the running site.
/// </summary>
public static class RepoFiles
{
    public static string Root { get; } = FindRoot();

    public static string Wwwroot { get; } = Path.Combine(Root, "OneADay", "wwwroot");

    public static string AppRazor() =>
        File.ReadAllText(Path.Combine(Root, "OneADay", "Components", "App.razor"));

    /// <summary>A PNG's width and height, from its header. Fails if the file isn't a PNG.</summary>
    public static (int Width, int Height) PngSize(string path) =>
        PngSize(File.ReadAllBytes(path), Path.GetFileName(path));

    /// <summary>
    /// The sizes inside an .ico, in order, as docs/brand/icons.swift writes one: a square PNG
    /// for each. Fails if the file isn't an icon, or if a picture isn't the size its entry says.
    /// </summary>
    public static int[] IcoSizes(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var name = Path.GetFileName(path);
        if (BinaryPrimitives.ReadUInt16LittleEndian(bytes) != 0 || BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)) != 1)
        {
            throw new InvalidDataException($"{name} isn't an icon.");
        }
        var sizes = new int[BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4))];
        for (var i = 0; i < sizes.Length; i++)
        {
            var entry = bytes.AsSpan(6 + 16 * i, 16);
            var size = entry[0] == 0 ? 256 : entry[0];   // one byte, so 256 is written as 0
            var picture = bytes.AsSpan(BinaryPrimitives.ReadInt32LittleEndian(entry[12..]),
                                       BinaryPrimitives.ReadInt32LittleEndian(entry[8..]));
            if (entry[1] != entry[0] || PngSize(picture, $"{name}'s picture {i + 1}") != (size, size))
            {
                throw new InvalidDataException($"{name}'s picture {i + 1} isn't {size} x {size}.");
            }
            sizes[i] = size;
        }
        return sizes;
    }

    private static (int Width, int Height) PngSize(ReadOnlySpan<byte> bytes, string name)
    {
        if (!bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            throw new InvalidDataException($"{name} isn't a PNG.");
        }
        return (BinaryPrimitives.ReadInt32BigEndian(bytes[16..]), BinaryPrimitives.ReadInt32BigEndian(bytes[20..]));
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, ".gitignore")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Couldn't find the repo root.");
    }
}
