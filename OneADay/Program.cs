using Microsoft.Extensions.FileProviders;
using OneADay.Components;
using OneADay.Services;

var builder = WebApplication.CreateBuilder(args);

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

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
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
