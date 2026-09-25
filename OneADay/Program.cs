using Microsoft.AspNetCore.DataProtection;
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

// Three more refusals, for the same reason and in the same place: checked before anything is
// built, so a refused start never opens a port.
//
// Visitor addresses are hashed before they are stored, and that hash needs a secret key
// outside Development — without one it is a plain hash of an IPv4 address, which inverts in
// minutes (PRD 06). A proxy that is announced must actually be trusted, or the forwarded
// headers are ignored in silence (PRD 11). And the Cloudflare-only lock, on unless a host
// switches it off, needs the secret Cloudflare's header carries, or it can't tell Cloudflare's
// requests from anyone else's (PRD 11).
if (IpHasher.ReasonToRefuse(builder.Environment, builder.Configuration) is { } privacyRefusal)
{
    throw new InvalidOperationException(privacyRefusal);
}

var proxyOptions = builder.Configuration.GetSection(ProxyOptions.Section).Get<ProxyOptions>()
                   ?? new ProxyOptions();
if (proxyOptions.ReasonToRefuse() is { } proxyRefusal)
{
    throw new InvalidOperationException(proxyRefusal);
}

var cloudflareLock = builder.Configuration.GetSection(CloudflareLockOptions.Section).Get<CloudflareLockOptions>()
                     ?? new CloudflareLockOptions();
if (cloudflareLock.ReasonToRefuse() is { } lockRefusal)
{
    throw new InvalidOperationException(lockRefusal);
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

// Reading a visitor's real address behind a proxy, and storing it as a keyed hash rather
// than a recoverable one. See ProxyOptions and IpHasher.
builder.Services.Configure<ProxyOptions>(builder.Configuration.GetSection(ProxyOptions.Section));
builder.Services.Configure<CloudflareLockOptions>(builder.Configuration.GetSection(CloudflareLockOptions.Section));
builder.Services.AddSingleton(IpHasher.Create(builder.Environment, builder.Configuration));

// Data Protection encrypts every ProtectedLocalStorage value, the anonymous visitor id among
// them. The default key ring does not survive a restart, and losing it makes every stored
// value undecryptable: each returning visitor looks brand new, unique-attempter counts
// inflate, and the one-suggestion-per-day limit resets for everyone. It fails *silently* —
// the site keeps working — so nothing would ever draw attention to it. App_Data is the volume
// that has to survive a redeploy anyway (PRD 11), and it is gitignored.
//
// The application name is pinned rather than left to default. The default is the assembly
// name, so renaming this project would silently invalidate every stored value.
var keyRing = Directory.CreateDirectory(
    Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys"));
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(keyRing)
    .SetApplicationName("OneADay");

// Email notifications. The app password comes from user-secrets locally and an
// Email__AppPassword environment variable in production — never appsettings.json.
// Unconfigured is a supported state: the notifier no-ops and the site is unaffected.
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.Section));
builder.Services.AddSingleton<SmtpMailer>();
builder.Services.AddSingleton<EmailNotifier>();
builder.Services.AddHostedService<EmailSenderService>();

// The host bills for data sent and has no billing alerts of its own, so the site emails the
// author when a day's requests pass the alert line. See TrafficMonitor and PRD 14.
builder.Services.Configure<TrafficAlertOptions>(builder.Configuration.GetSection(TrafficAlertOptions.Section));
builder.Services.AddSingleton<TrafficMonitor>();
builder.Services.AddHostedService<TrafficAlertService>();

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

// Turn away anything that didn't come through Cloudflare — first, so nothing else runs for a
// request the lock refuses. Below the static files, the biggest files would go out before the
// check, which is the exact path it exists to close. See CloudflareLock and PRD 11.
app.UseCloudflareLock();

// Count every request the lock lets in, before anything can answer it — below the static
// files, a flood of downloads would go uncounted. The traffic alert reads these counts.
app.UseTrafficCount();

// Behind a proxy, rewrite the connection's address and scheme from its forwarded headers.
// This has to come before anything that reads either one: UseHsts and UseHttpsRedirection
// both read the scheme, and below them the app would see the proxy's inward hop as plain
// HTTP and redirect to HTTPS forever. See ProxyOptions and PRD 11.
app.UseProxyHeaders();

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
