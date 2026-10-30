using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>A player held in memory, standing in for the browser's own storage.</summary>
public sealed class InMemoryPlayerStorage : ITwentyFourPlayerStorage
{
    public TwentyFourPlayer? Player { get; set; }

    /// <summary>When set, every read and write throws this: a browser that stores nothing.</summary>
    public Exception? FailWith { get; set; }

    /// <summary>When set, reads work but saving throws this: storage that's full or read-only.</summary>
    public Exception? FailWritesWith { get; set; }

    /// <summary>When set, a read waits for it: a browser slow to answer.</summary>
    public TaskCompletionSource? Hold { get; set; }

    public async ValueTask<TwentyFourPlayer?> ReadAsync()
    {
        if (Hold is { } hold)
        {
            await hold.Task;
        }
        return FailWith is null ? Player : throw FailWith;
    }

    public ValueTask WriteAsync(TwentyFourPlayer player)
    {
        if ((FailWith ?? FailWritesWith) is { } failure)
        {
            return ValueTask.FromException(failure);
        }
        Player = player;
        return ValueTask.CompletedTask;
    }
}

public static class TwentyFourTestServices
{
    /// <summary>What the Twenty Four page needs besides metrics: its board and a player.</summary>
    public static TwentyFourBoard AddTwentyFour(this IServiceCollection services, TestEnvironment env,
        InMemoryPlayerStorage? player = null)
    {
        var board = new TwentyFourBoard(env, NullLogger<TwentyFourBoard>.Instance);
        services.AddSingleton(board);
        services.AddSingleton<ITwentyFourPlayerStorage>(player ?? new InMemoryPlayerStorage());
        return board;
    }
}
