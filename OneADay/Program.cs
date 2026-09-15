using Microsoft.Extensions.FileProviders;
using OneADay.Components;
using OneADay.Services;

var builder = WebApplication.CreateBuilder(args);

// Never start in Development mode anywhere but the author's computer: that mode switches the
// admin page on. Checked before anything is built, so a refused start opens no port — see
// DevelopmentModeGuard and PRD 10.
if (DevelopmentModeGuard.ReasonToRefuse(builder.Environment, builder.Configuration, DevelopmentModeGuard.IsDebugBuild) is { } refusal)
{
    throw new InvalidOperationException(refusal);
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<TeaserStore>();
builder.Services.AddSingleton<StatsStore>();
builder.Services.AddSingleton<SuggestionStore>();
builder.Services.AddSingleton<ImageStore>();
builder.Services.AddSingleton<IssueStore>();
builder.Services.AddSingleton<RotationStore>();
builder.Services.AddSingleton<DailySchedule>();
builder.Services.AddScoped<CurrentTeaserContext>();
builder.Services.AddHttpContextAccessor();

// Email notifications. The app password comes from user-secrets locally and an
// Email__AppPassword environment variable in production — never appsettings.json.
// Unconfigured is a supported state: the notifier no-ops and the site is unaffected.
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.Section));
builder.Services.AddSingleton<SmtpMailer>();
builder.Services.AddSingleton<EmailNotifier>();
builder.Services.AddHostedService<EmailSenderService>();

// Daily-challenge subscriptions (PRD 15). subscribers.json lives in App_Data/, which is
// gitignored, so subscriber addresses can never reach the public repository.
builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection(SiteOptions.Section));
builder.Services.Configure<SubscriptionOptions>(builder.Configuration.GetSection(SubscriptionOptions.Section));
builder.Services.AddSingleton<SubscriberStore>();
builder.Services.AddSingleton<ConfirmationQueue>();
builder.Services.AddHostedService<ConfirmationSender>();
builder.Services.AddHostedService<DailyDigestService>();

var app = builder.Build();

// Load the question bank now, not on the first visitor's request. A missing or broken
// teasers.json should stop a deploy at startup, loudly, rather than show an error page to
// whoever happens to arrive first (PRD 10).
app.Services.GetRequiredService<TeaserStore>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// The admin page exists only on the author's machine. Everywhere else /admin is a 404,
// shown as the ordinary not-found page — see AdminAccess and PRD 10.
app.UseAdminOnlyInDevelopment();
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

// Serve runtime-uploaded teaser images (MapStaticAssets only covers build-time wwwroot).
var teaserImages = app.Services.GetRequiredService<ImageStore>();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(teaserImages.PhysicalDirectory),
    RequestPath = ImageStore.RequestPath,
});

// One-click unsubscribe for mail clients (RFC 8058) — see SubscriptionEndpoints.
app.MapOneClickUnsubscribe();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
