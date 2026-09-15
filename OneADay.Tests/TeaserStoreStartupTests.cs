using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// What the question bank does when <c>teasers.json</c> isn't what it should be.
///
/// <para>On the live site, loading the bank is the step right after a publish, so a bad
/// file has to stop the app — loudly, and without touching the file — rather than limp on.
/// The worst version of limping on already existed: a missing file used to be replaced,
/// silently, by three sample riddles.</para>
/// </summary>
public sealed class TeaserStoreStartupTests : IDisposable
{
    private readonly TestEnvironment _env = new();

    private string BankPath => Path.Combine(_env.ContentRootPath, "App_Data", "teasers.json");

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void A_missing_bank_stops_the_live_site_and_invents_nothing(string environment)
    {
        _env.EnvironmentName = environment;

        var error = Assert.Throws<InvalidOperationException>(() => new TeaserStore(_env));

        Assert.Contains("teasers.json", error.Message);
        Assert.False(File.Exists(BankPath));   // no sample riddles saved in the bank's place
    }

    [Fact]
    public void A_missing_bank_on_the_authors_machine_still_starts_with_samples()
    {
        // A fresh checkout should still just run.
        _env.EnvironmentName = "Development";

        var store = new TeaserStore(_env);

        Assert.Equal(3, store.GetAll().Count);
        Assert.True(File.Exists(BankPath));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void A_broken_bank_stops_the_app_and_is_left_exactly_as_it_was(string environment)
    {
        _env.EnvironmentName = environment;
        const string cutOff = "[ { \"Question\": \"What has keys but can't open locks?\", ";
        _env.WriteDataFile("teasers.json", cutOff);

        var error = Assert.Throws<InvalidOperationException>(() => new TeaserStore(_env));

        Assert.Contains("teasers.json", error.Message);
        Assert.Equal(cutOff, _env.ReadDataFile("teasers.json"));
    }

    public void Dispose() => _env.Dispose();
}
