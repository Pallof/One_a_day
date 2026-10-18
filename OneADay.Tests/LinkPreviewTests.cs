using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneADay.Components;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// The link preview (PRD 09): what a messaging app shows when someone shares a link to the site,
/// so the person receiving it can see what it is before they tap it.
/// </summary>
public class LinkPreviewTests : BunitContext
{
    public LinkPreviewTests()
    {
        Services.AddSingleton(Options.Create(new SiteOptions { BaseUrl = "https://stumpty.com" }));
    }

    private static string? Meta(IRenderedComponent<LinkPreview> preview, string key) =>
        preview.Nodes.OfType<IElement>()
            .SingleOrDefault(m => m.GetAttribute("property") == key || m.GetAttribute("name") == key)
            ?.GetAttribute("content");

    [Fact]
    public void The_preview_names_the_site_and_says_what_it_is()
    {
        var preview = Render<LinkPreview>();

        // The title invites; the name comes from the site name, which the apps show beside it.
        Assert.Equal("Can you solve today's puzzle?", Meta(preview, "og:title"));
        Assert.Equal("Stumpty", Meta(preview, "og:site_name"));
        Assert.Equal("website", Meta(preview, "og:type"));
        // The reassurance leads, because WhatsApp cuts the description off after two lines.
        Assert.StartsWith("Free, no sign-up.", Meta(preview, "og:description"));
        Assert.Contains("brain teaser every day", Meta(preview, "og:description"));
        // Search results and shared links describe the site in the same words.
        Assert.Equal(Meta(preview, "og:description"), Meta(preview, "description"));
    }

    [Fact]
    public void The_picture_is_a_full_address_on_the_site_and_a_large_card()
    {
        // The apps fetch the picture from their own servers, where a relative address means
        // nothing. Outside the running site, Assets hands the file name back as written.
        var preview = Render<LinkPreview>();

        Assert.Equal("https://stumpty.com/img/stumpty-card.png", Meta(preview, "og:image"));
        var card = Path.Combine(RepoFiles.Wwwroot, "img", "stumpty-card.png");
        Assert.Equal((1200, 630), RepoFiles.PngSize(card));
        Assert.Equal("1200", Meta(preview, "og:image:width"));
        Assert.Equal("630", Meta(preview, "og:image:height"));
        Assert.Contains("duck", Meta(preview, "og:image:alt"));
        // X shows a small square thumbnail without this.
        Assert.Equal("summary_large_image", Meta(preview, "twitter:card"));
    }

    [Fact]
    public void The_card_is_small_enough_for_whatsapp()
    {
        // WhatsApp is known to skip preview pictures over about 300 KB.
        var card = new FileInfo(Path.Combine(RepoFiles.Wwwroot, "img", "stumpty-card.png"));

        Assert.True(card.Length < 300 * 1024, $"The card is {card.Length / 1024} KB.");
    }

    [Fact]
    public void Every_page_carries_the_preview_in_its_head()
    {
        // The apps read only the page as first sent, so it has to be in App's <head>, which
        // every page shares, and not left to a page or to a script.
        var app = RepoFiles.AppRazor();
        var head = app[app.IndexOf("<head>", StringComparison.Ordinal)..app.IndexOf("</head>", StringComparison.Ordinal)];

        Assert.Contains("<LinkPreview />", head);
    }
}
