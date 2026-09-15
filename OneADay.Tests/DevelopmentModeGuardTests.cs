using Microsoft.Extensions.Configuration;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// Development mode switches the admin page on, so it must never start on a server. These
/// pin the two checks that stop the realistic accidents — and pin that the live site, in
/// Production, is never refused.
/// </summary>
public sealed class DevelopmentModeGuardTests : IDisposable
{
    private readonly TestEnvironment _env = new();

    private static IConfiguration Config(string? marker) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DevelopmentModeGuard.MarkerKey] = marker })
            .Build();

    private string? Refusal(string environment, bool debugBuild, string? marker)
    {
        _env.EnvironmentName = environment;
        return DevelopmentModeGuard.ReasonToRefuse(_env, Config(marker), debugBuild);
    }

    [Theory]
    [InlineData("Production", false, null)]
    [InlineData("Production", true, null)]
    [InlineData("Production", false, "true")]
    [InlineData("Staging", false, null)]
    public void Outside_development_it_never_refuses(string environment, bool debugBuild, string? marker)
    {
        // The live site must always be able to start. The guard only ever narrows Development.
        Assert.Null(Refusal(environment, debugBuild, marker));
    }

    [Fact]
    public void A_release_build_refuses_development_even_on_a_marked_machine()
    {
        // A published app is a Release build. Development mode on one is the host-variable
        // accident, and a marker copied across doesn't excuse it.
        var reason = Refusal("Development", debugBuild: false, marker: "true");

        Assert.NotNull(reason);
        Assert.Contains("Release build", reason);
        Assert.Contains("Production", reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("yes please")]
    public void An_unmarked_machine_refuses_development(string? marker)
    {
        // The dotnet-run-on-a-server accident: a Debug build, Development switched on by
        // launchSettings.json, and no marker — because it isn't the author's computer.
        var reason = Refusal("Development", debugBuild: true, marker);

        Assert.NotNull(reason);
        Assert.Contains("launchSettings.json", reason);
    }

    [Fact]
    public void The_authors_computer_still_starts_in_development()
    {
        // The control case: without it, a guard that refused everything would pass.
        Assert.Null(Refusal("Development", debugBuild: true, marker: "true"));
    }

    [Fact]
    public void A_refusal_never_hands_a_server_the_command_that_would_silence_it()
    {
        // Whoever reads this may be standing on the server. Pointing at the setup doc is
        // fine; printing the bypass as a one-liner to paste is not.
        var unmarked = Refusal("Development", debugBuild: true, marker: null);
        var release = Refusal("Development", debugBuild: false, marker: null);

        // Both must actually refuse first. Without these, a guard that refused nothing would
        // pass the wording checks below trivially — a mutation run caught exactly that.
        Assert.NotNull(unmarked);
        Assert.NotNull(release);
        Assert.DoesNotContain("user-secrets", unmarked);
        Assert.DoesNotContain(DevelopmentModeGuard.MarkerKey, unmarked);
        Assert.DoesNotContain("user-secrets", release);
        Assert.DoesNotContain(DevelopmentModeGuard.MarkerKey, release);
    }

    public void Dispose() => _env.Dispose();
}
