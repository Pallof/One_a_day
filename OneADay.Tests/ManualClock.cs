namespace OneADay.Tests;

/// <summary>
/// The server's clock, moved by hand. Its timers fire only when told to, so a test can have a move
/// arrive after the time is up but before the ticker has had its turn.
/// </summary>
public sealed class ManualClock : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];

    public DateTimeOffset Now { get; set; } = new(2026, 11, 13, 20, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;

    /// <summary>The timers made and not yet stopped.</summary>
    public IReadOnlyList<ManualTimer> Running => _timers.Where(t => !t.Disposed).ToList();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state, dueTime, period);
        _timers.Add(timer);
        return timer;
    }

    /// <summary>Moves the time on with no timer firing.</summary>
    public void Skip(TimeSpan by) => Now += by;

    /// <summary>Moves the time on, then fires each running timer once.</summary>
    public void Tick(TimeSpan by)
    {
        Now += by;
        foreach (var timer in Running)
        {
            timer.Fire();
        }
    }
}

public sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) : ITimer
{
    public TimeSpan DueTime { get; } = dueTime;
    public TimeSpan Period { get; } = period;
    public bool Disposed { get; private set; }

    public void Fire()
    {
        if (!Disposed)
        {
            callback(state);
        }
    }

    public bool Change(TimeSpan dueTime, TimeSpan period) => !Disposed;

    public void Dispose() => Disposed = true;

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
