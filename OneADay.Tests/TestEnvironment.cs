using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using OneADay.Models;
using OneADay.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OneADay.Tests;

/// <summary>
/// A throwaway content root so the file-backed stores can be exercised in tests
/// without touching the real App_Data.
/// </summary>
public sealed class TestEnvironment : IWebHostEnvironment, IDisposable
{
    public TestEnvironment()
    {
        ContentRootPath = Path.Combine(Path.GetTempPath(), "oneaday-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ContentRootPath, "App_Data"));
    }

    /// <summary>Writes a teasers.json for the store to load.</summary>
    public void SeedTeasers(params BrainTeaser[] teasers)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };
        File.WriteAllText(
            Path.Combine(ContentRootPath, "App_Data", "teasers.json"),
            JsonSerializer.Serialize(teasers, options));
    }

    public TeaserStore NewTeaserStore() => new(this);

    public StatsStore NewStatsStore() => new(this);

    public string ContentRootPath { get; set; }
    public string WebRootPath { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = "OneADay.Tests";
    public string EnvironmentName { get; set; } = "Test";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(ContentRootPath))
            {
                Directory.Delete(ContentRootPath, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test over.
        }
    }
}

public static class TeaserFactory
{
    public static BrainTeaser On(string date, string question = "Q?", string answer = "a",
                                 string? hint = null, string? solution = null) => new()
    {
        Date = DateOnly.Parse(date),
        Question = question,
        Answer = answer,
        Hint = hint,
        Solution = solution,
    };
}
