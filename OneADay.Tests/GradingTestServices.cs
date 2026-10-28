using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>Stands in for Jev: answers from a script, and remembers what it was asked.</summary>
public sealed class FakeJudge : IAnswerJudge
{
    /// <summary>The chance it gives each submission. By default no verdict at all, as when Jev is down.</summary>
    public Func<string, double?> Chance { get; set; } = _ => null;

    public List<string> Asked { get; } = [];

    /// <summary>The accepted answers it was shown on the last question.</summary>
    public IReadOnlyList<string>? LastAccepted { get; private set; }

    public Task<double?> ChanceSameAnswerAsync(
        string question, IReadOnlyList<string> accepted, string submission, CancellationToken cancellationToken)
    {
        Asked.Add(submission);
        LastAccepted = accepted;
        return Task.FromResult(Chance(submission));
    }
}

public static class GradingTestServices
{
    /// <summary>
    /// The grader a challenge page needs, over a fake Jev. Jev is off unless
    /// <paramref name="options"/> switches it on.
    /// </summary>
    public static FakeJudge AddGrading(this IServiceCollection services, TestEnvironment env, JevOptions? options = null)
    {
        var judge = new FakeJudge();
        services.AddLogging();
        services.AddSingleton<IAnswerJudge>(judge);
        services.AddSingleton(new JudgedAnswerStore(env));
        services.AddSingleton(Options.Create(options ?? new JevOptions()));
        services.AddSingleton<AnswerGrader>();
        return judge;
    }

    /// <summary>Jev switched on with a made-up key, as the tests need it.</summary>
    public static JevOptions On(double threshold = 0.7, int maxPerDay = 300) => new()
    {
        Enabled = true,
        ApiKey = "test-key-not-real",
        Threshold = threshold,
        MaxPerDay = maxPerDay,
    };
}
