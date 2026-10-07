using Microsoft.Extensions.DependencyInjection;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>A browser's note held in memory, standing in for the browser's own storage.</summary>
public sealed class InMemoryNoteStorage : IBrowserNoteStorage
{
    public BrowserNote? Note { get; set; }

    /// <summary>When set, every read and write throws this — a page with no live connection.</summary>
    public Exception? FailWith { get; set; }

    /// <summary>When set, reads work but saving throws this — storage that's full or blocked.</summary>
    public Exception? FailWritesWith { get; set; }

    public ValueTask<BrowserNote?> ReadAsync() =>
        FailWith is null ? ValueTask.FromResult(Note) : ValueTask.FromException<BrowserNote?>(FailWith);

    public ValueTask WriteAsync(BrowserNote note)
    {
        if ((FailWith ?? FailWritesWith) is { } failure)
        {
            return ValueTask.FromException(failure);
        }
        Note = note;
        return ValueTask.CompletedTask;
    }
}

public static class MetricsTestServices
{
    /// <summary>Everything a page that counts needs, over an in-memory browser note.</summary>
    public static MetricsStore AddMetrics(this IServiceCollection services, TestEnvironment env,
        InMemoryNoteStorage? note = null)
    {
        var store = env.NewMetricsStore();
        services.AddLogging();
        services.AddSingleton(store);
        services.AddSingleton<IBrowserNoteStorage>(note ?? new InMemoryNoteStorage());
        services.AddScoped<MetricsCounter>();
        return store;
    }
}
