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
builder.Services.AddHttpContextAccessor();

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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
