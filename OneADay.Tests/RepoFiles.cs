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
    public static (int Width, int Height) PngSize(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (!bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            throw new InvalidDataException($"{Path.GetFileName(path)} isn't a PNG.");
        }
        return (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20)));
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
