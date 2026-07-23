using Microsoft.AspNetCore.Components.Forms;

namespace OneADay.Services;

/// <summary>
/// Saves and deletes optional support images for teasers. Files live in
/// App_Data/teaser-images and are served at /teaser-images/{name} (wired up
/// in Program.cs). Filenames are random GUIDs — the uploader's name is never
/// trusted or used on disk.
/// </summary>
public class ImageStore
{
    public const long MaxBytes = 3 * 1024 * 1024; // 3 MB

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp",
    };

    public const string RequestPath = "/teaser-images";

    private readonly string _dir;

    public ImageStore(IWebHostEnvironment env)
    {
        _dir = Path.Combine(env.ContentRootPath, "App_Data", "teaser-images");
        Directory.CreateDirectory(_dir);
    }

    public string PhysicalDirectory => _dir;

    public static bool IsAllowedType(string contentType) => AllowedTypes.ContainsKey(contentType);

    /// <summary>Saves the browser file and returns the stored filename.</summary>
    public async Task<string> SaveAsync(IBrowserFile file)
    {
        if (!AllowedTypes.TryGetValue(file.ContentType, out var ext))
        {
            throw new InvalidOperationException("Unsupported image type.");
        }

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(_dir, fileName);

        await using var target = File.Create(fullPath);
        await file.OpenReadStream(MaxBytes).CopyToAsync(target);

        return fileName;
    }

    public void Delete(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }
        // Guard against path traversal — only ever touch a bare filename in our dir.
        var fullPath = Path.Combine(_dir, Path.GetFileName(fileName));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}
