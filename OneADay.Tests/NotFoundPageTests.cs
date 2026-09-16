using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OneADay.Components.Pages;

namespace OneADay.Tests;

/// <summary>
/// The not-found page speaks twice: a plain apology for an address that doesn't exist, and a
/// cheekier line for someone knocking on the admin page.
///
/// <para>The split matters because this one page answers every wrong address on the site.
/// Telling a visitor who fumbled a URL "nice try" reads as an accusation.</para>
/// </summary>
public class NotFoundPageTests : BunitContext
{
    public NotFoundPageTests()
    {
        // Registered, but with no request behind it — as in a live circuit, where the page
        // falls back to the address in the browser.
        Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    }

    private IRenderedComponent<NotFound> Open(string path)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(path);
        return Render<NotFound>();
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("admin/teasers")]
    public void Knocking_on_admin_gets_the_cheeky_line(string path)
    {
        var page = Open(path);

        Assert.Contains("nothing to see here", page.Markup);
        Assert.DoesNotContain("may be old", page.Markup);
    }

    [Theory]
    [InlineData("yesterdayy")]
    [InlineData("administrator")]   // shares the letters, not the path segment
    public void A_mistyped_address_gets_a_plain_apology(string path)
    {
        var page = Open(path);

        Assert.Contains("may be old", page.Markup);
        Assert.DoesNotContain("Nice try", page.Markup);
    }

    [Fact]
    public void Both_voices_keep_the_cat_and_the_way_home()
    {
        foreach (var path in new[] { "admin", "yesterdayy" })
        {
            var page = Open(path);

            Assert.Contains("🐱", page.Markup);
            Assert.NotEmpty(page.FindAll("a[href='']"));
        }
    }
}
