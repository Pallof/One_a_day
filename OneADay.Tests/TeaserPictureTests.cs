using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
using OneADay.Components;
using OneADay.Components.Pages;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// A teaser's picture (PRD 05): every picture is described in words, for screen readers and
/// for email apps that hold pictures back, and a picture missing from the server can be found
/// before anyone sees it broken (PRD 11). The picture in the daily email is pinned in
/// SubscriptionTests.
/// </summary>
public class TeaserPictureTests : BunitContext
{
    private const string Poles = "Two 50 m poles some distance apart, with 80 m of rope sagging between their tops.";

    private readonly TestEnvironment _env = new();

    public TeaserPictureTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ---- writing the description in admin --------------------------------------------

    private IRenderedComponent<Admin> AdminWithAQuestion()
    {
        _env.SeedTeasers(TeaserFactory.On("2026-09-01"));
        Services.AddSingleton(_env.NewTeaserStore());
        Services.AddSingleton(_env.NewStatsStore());
        Services.AddSingleton(_env.NewSuggestionStore());
        Services.AddSingleton(new ImageStore(_env));
        Services.AddSingleton(_env.NewIssueStore());
        Services.AddSingleton(_env.NewRotationStore());
        Services.AddSingleton(_env.NewMetricsStore());

        var page = Render<Admin>();
        page.Find("#question").Change("How far apart are the poles?");
        page.Find("#answer").Change("0");
        return page;
    }

    private static void Upload(IRenderedComponent<Admin> page, int bytes) =>
        page.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromBinary(new byte[bytes], "poles.png", contentType: "image/png"));

    private static AngleSharp.Dom.IElement SaveButton(IRenderedComponent<Admin> page) =>
        page.Find(".oad-admin-actions button.oad-btn-green");

    [Fact]
    public void A_picture_cannot_be_saved_without_a_description()
    {
        var page = AdminWithAQuestion();
        Assert.Empty(page.FindAll("#image-alt"));   // nothing to describe yet
        Assert.False(SaveButton(page).HasAttribute("disabled"));

        Upload(page, 30 * 1024);

        Assert.True(SaveButton(page).HasAttribute("disabled"));
        page.Find("#image-alt").Input(Poles);
        Assert.False(SaveButton(page).HasAttribute("disabled"));

        SaveButton(page).Click();
        var saved = Services.GetRequiredService<TeaserStore>().GetAll()
            .Single(t => t.Question == "How far apart are the poles?");
        Assert.NotNull(saved.ImageFileName);
        Assert.Equal(Poles, saved.ImageAlt);
    }

    [Fact]
    public void Editing_a_teaser_keeps_its_pictures_description()
    {
        // Lost on the way into the form, it would leave Save disabled for no visible reason.
        var page = AdminWithAQuestion();
        Upload(page, 30 * 1024);
        page.Find("#image-alt").Input(Poles);
        SaveButton(page).Click();

        page.FindAll("tr").Single(row => row.TextContent.Contains("How far apart are the poles?"))
            .QuerySelectorAll("td.oad-cell-actions button").Single(b => b.TextContent == "Edit").Click();

        Assert.Equal(Poles, page.Find("#image-alt").GetAttribute("value") ?? page.Find("#image-alt").TextContent);
        Assert.False(SaveButton(page).HasAttribute("disabled"));
    }

    // ---- showing it ---------------------------------------------------------------------

    [Fact]
    public void The_challenge_and_yesterdays_page_describe_the_picture_in_words()
    {
        _env.SeedTeasers(Enumerable.Range(1, 6)
            .Select(i => Pictured(TeaserFactory.On(new DateOnly(2026, 8, 1).AddDays(i).ToString("yyyy-MM-dd"), $"question {i}")))
            .ToArray());
        Services.AddDataProtection();
        Services.AddScoped<ProtectedLocalStorage>();
        Services.AddSingleton(_env.NewStatsStore());
        Services.AddMetrics(_env);
        Services.AddScoped<CurrentTeaserContext>();
        Services.AddSingleton(new DailySchedule(_env.NewTeaserStore(), _env.NewRotationStore()));

        var challenge = Render<ChallengeView>(p => p.Add(c => c.Teaser, Pictured(TeaserFactory.On("2026-08-12"))));
        var yesterday = Render<Yesterday>();

        Assert.Equal(Poles, challenge.Find("img.cv-image").GetAttribute("alt"));
        Assert.Equal(Poles, yesterday.Find("img.ys-image").GetAttribute("alt"));
    }

    private static BrainTeaser Pictured(BrainTeaser teaser)
    {
        teaser.ImageFileName = "poles.png";
        teaser.ImageAlt = Poles;
        return teaser;
    }

    // ---- a picture left behind ----------------------------------------------------------

    [Fact]
    public void A_picture_missing_from_the_server_is_found()
    {
        var images = new ImageStore(_env);
        File.WriteAllBytes(Path.Combine(images.PhysicalDirectory, "here.png"), [1]);

        var uploaded = TeaserFactory.On("2026-08-01");
        uploaded.ImageFileName = "here.png";
        var leftBehind = TeaserFactory.On("2026-08-02");
        leftBehind.ImageFileName = "not-uploaded.png";
        var noPicture = TeaserFactory.On("2026-08-03");

        Assert.Equal([leftBehind], images.MissingPictures([uploaded, leftBehind, noPicture]));
    }

    protected override void Dispose(bool disposing)
    {
        _env.Dispose();
        base.Dispose(disposing);
    }
}
