using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneADay.Services;

namespace OneADay.Tests;

public static class LayoutTestServices
{
    /// <summary>
    /// What the layout needs for the report dialog it carries on every page (PRD 06). The
    /// notifier is switched off unless a test passes its own.
    /// </summary>
    public static IssueStore AddReportDialog(this IServiceCollection services, TestEnvironment env,
        EmailNotifier? notifier = null)
    {
        var store = env.NewIssueStore();
        services.AddSingleton(store);
        services.AddSingleton(notifier
            ?? new EmailNotifier(Options.Create(new EmailOptions()), NullLogger<EmailNotifier>.Instance));
        services.AddScoped<CurrentTeaserContext>();
        return store;
    }
}
