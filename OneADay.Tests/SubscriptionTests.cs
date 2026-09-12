using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneADay.Models;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// Daily-challenge subscriptions: address validation, the subscriber lifecycle, and the
/// 7am digest.
///
/// <para>The properties worth pinning are mostly about <b>not</b> doing things — not
/// emailing an address that never confirmed, not sending the same challenge twice, not
/// leaking who is subscribed, not putting an answer in an inbox. Each of those fails
/// silently and in a way a subscriber would notice before the author did.</para>
/// </summary>
public class SubscriptionTests
{
    private static readonly DateTime T0 = new(2026, 9, 11, 16, 0, 0, DateTimeKind.Utc);

    // ==== address validation ====================================================

    [Theory]
    [InlineData("reader@example.com", "reader@example.com")]
    [InlineData("  Reader@Example.COM  ", "reader@example.com")]   // trimmed, lowered
    [InlineData("first.last+tag@sub.example.co.uk", "first.last+tag@sub.example.co.uk")]
    public void Valid_addresses_are_normalised(string raw, string expected)
    {
        Assert.Equal(expected, EmailAddress.Normalise(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign.com")]
    [InlineData("two@@example.com")]
    [InlineData("a@b@example.com")]
    [InlineData("@example.com")]
    [InlineData("reader@")]
    [InlineData("reader@localhost")]            // valid syntax, useless for a newsletter
    [InlineData("reader@.example.com")]
    [InlineData("with space@example.com")]
    [InlineData("Bob <bob@example.com>")]       // display-name form MailAddress would accept
    public void Unusable_addresses_are_rejected(string? raw)
    {
        Assert.Null(EmailAddress.Normalise(raw));
    }

    [Theory]
    [InlineData("reader@example.com\r\nBcc: victim@example.com")]
    [InlineData("reader@example.com\nX-Injected: yes")]
    [InlineData("reader@example.com\r")]
    public void Line_breaks_are_rejected_before_they_can_reach_a_mail_header(string raw)
    {
        // This value becomes the To header. A CR/LF there is header injection — it would
        // let a sign-up add its own Bcc and turn the site into a spam relay.
        Assert.Null(EmailAddress.Normalise(raw));
    }

    [Fact]
    public void Overlong_addresses_are_rejected()
    {
        var address = new string('a', EmailAddress.MaxLength) + "@example.com";
        Assert.Null(EmailAddress.Normalise(address));
    }

    [Theory]
    [InlineData("danielsing@gmail.com", "d********g@gmail.com")]
    [InlineData("ab@example.com", "a*@example.com")]
    [InlineData("a@example.com", "a*@example.com")]
    public void Masking_hides_the_local_part_but_keeps_it_recognisable(string address, string expected)
    {
        Assert.Equal(expected, EmailAddress.Mask(address));
    }

    // ==== the subscriber lifecycle ==============================================

    [Fact]
    public void A_new_address_is_stored_pending_and_asks_for_a_confirmation()
    {
        using var env = new TestEnvironment();
        var store = env.NewSubscriberStore();

        var result = store.Request("reader@example.com", T0);

        Assert.Equal(SubscribeOutcome.SendConfirmation, result.Outcome);
        Assert.NotNull(result.Token);
        Assert.Equal((0, 1), store.Counts);   // pending, not confirmed
    }

    [Fact]
    public void Signing_up_twice_in_a_row_sends_only_one_confirmation()
    {
        // Without this the form is a way to fill a stranger's inbox: submit their
        // address fifty times, and they get fifty emails from us.
        using var env = new TestEnvironment();
        var store = env.NewSubscriberStore();

        store.Request("reader@example.com", T0);
        var again = store.Request("reader@example.com", T0.AddMinutes(5));

        Assert.Equal(SubscribeOutcome.AlreadyPending, again.Outcome);
        Assert.Null(again.Token);
        Assert.Equal((0, 1), store.Counts);   // no duplicate record either
    }

    [Fact]
    public void A_stale_pending_sign_up_can_ask_again_after_a_day()
    {
        using var env = new TestEnvironment();
        var store = env.NewSubscriberStore();

        var first = store.Request("reader@example.com", T0);
        var later = store.Request("reader@example.com", T0 + SubscriberStore.ConfirmationResendAfter);

        Assert.Equal(SubscribeOutcome.SendConfirmation, later.Outcome);
        Assert.Equal(first.Token, later.Token);   // same link still works
    }

    [Fact]
    public void Re_subscribing_a_confirmed_address_sends_nothing()
    {
        using var env = new TestEnvironment();
        var store = env.NewSubscriberStore();
        var token = store.Request("reader@example.com", T0).Token;
        store.Confirm(token, T0);

        var again = store.Request("reader@example.com", T0.AddDays(3));

        Assert.Equal(SubscribeOutcome.AlreadyConfirmed, again.Outcome);
        Assert.Null(again.Token);
    }

    [Fact]
    public void Confirming_with_the_right_token_activates_the_subscription()
    {
        using var env = new TestEnvironment();
        var store = env.NewSubscriberStore();
        var token = store.Request("reader@example.com", T0).Token;

        Assert.True(store.Confirm(token, T0));
        Assert.True(store.FindByToken(token)!.IsConfirmed);
        Assert.Equal((1, 0), store.Counts);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-real-token")]
    public void A_wrong_token_confirms_and_unsubscribes_nothing(string? token)
    {
        using var env = new TestEnvironment();
        var store = env.NewSubscriberStore();
        store.Request("reader@example.com", T0);

        Assert.False(store.Confirm(token, T0));
        Assert.False(store.Unsubscribe(token));
        Assert.Equal((0, 1), store.Counts);   // the real sign-up is untouched
    }

    [Fact]
    public void Confirming_twice_is_harmless()
    {
        using var env = new TestEnvironment();
        var store = env.NewSubscriberStore();
        var token = store.Request("reader@example.com", T0).Token;

        store.Confirm(token, T0);
        var firstConfirmedAt = store.FindByToken(token)!.ConfirmedAt;
        store.Confirm(token, T0.AddHours(5));

        Assert.Equal(firstConfirmedAt, store.FindByToken(token)!.ConfirmedAt);
    }

    [Fact]
    public void Unsubscribing_deletes_the_address_rather_than_flagging_it()
    {
        using var env = new TestEnvironment();
        var store = env.NewSubscriberStore();
        var token = store.Request("reader@example.com", T0).Token;
        store.Confirm(token, T0);

        Assert.True(store.Unsubscribe(token));

        Assert.Null(store.FindByToken(token));
        Assert.Equal((0, 0), store.Counts);
        Assert.DoesNotContain("reader@example.com", env.ReadDataFile("subscribers.json"));
    }

    [Fact]
    public void Tokens_are_long_random_and_unrelated_to_the_address()
    {
        using var env = new TestEnvironment();
        var store = env.NewSubscriberStore();

        var tokens = Enumerable.Range(0, 200)
            .Select(i => store.Request($"reader{i}@example.com", T0).Token!)
            .ToList();

        Assert.All(tokens, t => Assert.Equal(64, t.Length));   // 256 bits, hex
        Assert.Equal(tokens.Count, tokens.Distinct().Count());
        Assert.All(tokens, t => Assert.DoesNotContain("reader", t));
    }

    [Fact]
    public void Unconfirmed_sign_ups_expire_but_confirmed_ones_never_do()
    {
        // An address that never confirmed was never consent to keep it.
        using var env = new TestEnvironment();
        var store = env.NewSubscriberStore();
        store.Request("abandoned@example.com", T0);
        var keeper = store.Request("keeper@example.com", T0).Token;
        store.Confirm(keeper, T0);

        var removed = store.PruneExpired(T0 + SubscriberStore.PendingExpiry + TimeSpan.FromMinutes(1));

        Assert.Equal(1, removed);
        Assert.Equal((1, 0), store.Counts);
    }

    [Fact]
    public void Subscriptions_survive_a_restart()
    {
        using var env = new TestEnvironment();
        var token = env.NewSubscriberStore().Request("reader@example.com", T0).Token;
        env.NewSubscriberStore().Confirm(token, T0);

        Assert.True(env.NewSubscriberStore().FindByToken(token)!.IsConfirmed);
    }

    [Fact]
    public void The_subscriber_file_can_never_be_committed()
    {
        // The whole point of keeping addresses in App_Data/ is that it is gitignored.
        // Proven against the real .gitignore rather than assumed, because this is the one
        // file in the project that would put other people's data on the public repo.
        var repoRoot = FindRepoRoot();
        var ignore = File.ReadAllLines(Path.Combine(repoRoot, ".gitignore"));

        Assert.Contains(ignore, line => line.Trim() == "OneADay/App_Data/");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, ".gitignore")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Couldn't find the repo root.");
    }

    // ==== email content =========================================================

    private static readonly DigestLinks Links = new("https://site/", "https://site/unsubscribe?token=abc123");

    private static MailContent Digest(string question = "Q?", bool hasImage = false,
                                      Difficulty difficulty = Difficulty.Easy) =>
        SubscriptionMail.Digest(new DateOnly(2026, 9, 11), difficulty, question, hasImage, Links);

    /// <summary>What a reader sees in the HTML version, entities and all resolved.</summary>
    private static string Rendered(MailContent mail) => WebUtility.HtmlDecode(mail.Html);

    [Fact]
    public void The_digest_carries_the_question_but_never_the_answer_or_hint()
    {
        // The site's spoiler rules live on the site. An inbox is not somewhere a solution
        // should ever arrive unprompted. Both versions — a reader may only ever see one.
        var mail = Digest("What has keys but can't open locks?", difficulty: Difficulty.Hard);

        foreach (var version in new[] { mail.Body, Rendered(mail) })
        {
            Assert.Contains("What has keys but can't open locks?", version);
            Assert.DoesNotContain("keyboard", version, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hint", version, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Every_digest_carries_its_unsubscribe_link_in_both_versions()
    {
        var mail = Digest();

        Assert.Contains("https://site/unsubscribe?token=abc123", mail.Body);
        Assert.Contains("href=\"https://site/unsubscribe?token=abc123\"", mail.Html);
    }

    [Fact]
    public void Both_versions_link_to_todays_challenge()
    {
        var mail = Digest();

        Assert.Contains("href=\"https://site/\"", mail.Html);
        Assert.Contains("Solve today's challenge: https://site/", mail.Body);
    }

    [Fact]
    public void A_teaser_with_a_picture_says_so()
    {
        Assert.Contains("picture", Digest(hasImage: true).Body);
        Assert.Contains("picture", Rendered(Digest(hasImage: true)));
        Assert.DoesNotContain("picture", Digest(hasImage: false).Body);
        Assert.DoesNotContain("picture", Rendered(Digest(hasImage: false)));
    }

    [Fact]
    public void Teaser_text_is_encoded_so_it_cannot_become_markup()
    {
        // Teasers come from the author, not visitors — but a question about "a < b" must
        // arrive as text, and a pasted tag must never become a live element in an inbox.
        var mail = Digest("Is 3 < 5 & 5 > 3? <script>alert(1)</script>");

        Assert.DoesNotContain("<script>", mail.Html);
        Assert.Contains("&lt;script&gt;", mail.Html);
        Assert.Contains("Is 3 < 5 & 5 > 3?", Rendered(mail));
    }

    [Theory]
    [InlineData(Difficulty.Easy, "#E7F3EC")]
    [InlineData(Difficulty.Medium, "#FDF3DA")]
    [InlineData(Difficulty.Hard, "#F9E9E7")]
    public void The_difficulty_pill_uses_the_sites_colour_for_each_level(Difficulty level, string tint)
    {
        var mail = Digest(difficulty: level);

        Assert.Contains($"background-color:{tint}", mail.Html);
        Assert.Contains($"· {level}", mail.Subject);
    }

    [Fact]
    public void The_confirmation_link_is_in_both_versions_and_nothing_else_is_promised()
    {
        var mail = SubscriptionMail.Confirmation("https://site/subscribe/confirm?token=t0k", "https://site/");

        Assert.Contains("https://site/subscribe/confirm?token=t0k", mail.Body);
        Assert.Contains("href=\"https://site/subscribe/confirm?token=t0k\"", mail.Html);
        // The fallback link is printed for clients that won't show the button.
        Assert.Contains(">https://site/subscribe/confirm?token=t0k<", mail.Html);
        Assert.Contains("Nothing is sent until you do", Rendered(mail));
    }

    [Fact]
    public void Site_links_join_cleanly_whatever_the_slashes()
    {
        Assert.Equal("https://site/unsubscribe?token=x",
            new SiteOptions { BaseUrl = "https://site/" }.Link("/unsubscribe?token=x"));
        Assert.Equal("https://site/unsubscribe?token=x",
            new SiteOptions { BaseUrl = "https://site" }.Link("unsubscribe?token=x"));
    }
}

/// <summary>A mailer that records instead of sending, and can be told to fail.</summary>
internal sealed class RecordingMailer() : SmtpMailer(
    Options.Create(new EmailOptions
    {
        Enabled = true, To = "a@x.com", From = "a@x.com", Host = "h", AppPassword = "p",
    }),
    NullLogger<SmtpMailer>.Instance)
{
    public List<OutgoingMail> Sent { get; } = [];

    /// <summary>Addresses whose sends should fail, to exercise the retry path.</summary>
    public HashSet<string> FailFor { get; } = [];

    public override Task<bool> SendAsync(OutgoingMail mail, CancellationToken token)
    {
        if (FailFor.Contains(mail.To))
        {
            return Task.FromResult(false);
        }
        Sent.Add(mail);
        return Task.FromResult(true);
    }
}

/// <summary>The 7am digest — its schedule, its idempotency, and its limits.</summary>
public class DailyDigestTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    private static readonly DateTime Morning = new(2026, 9, 11, 7, 30, 0);   // Pacific
    private static readonly DateTime Night = new(2026, 9, 11, 3, 0, 0);

    private (DailyDigestService Service, SubscriberStore Store, RecordingMailer Mailer) Build(
        int maxPerDay = 400)
    {
        _env.SeedTeasers(Enumerable.Range(1, 6)
            .Select(i => TeaserFactory.On($"2026-08-{i:00}", $"question {i}", answer: $"answer {i}", hint: $"hint {i}"))
            .ToArray());

        var services = new ServiceCollection();
        var store = _env.NewSubscriberStore();
        var teasers = _env.NewTeaserStore();
        services.AddSingleton(store);
        services.AddSingleton(new DailySchedule(teasers, _env.NewRotationStore()));
        var provider = services.BuildServiceProvider();

        var mailer = new RecordingMailer();
        var service = new DailyDigestService(
            provider, mailer,
            Options.Create(new SubscriptionOptions { SendHourPacific = 7, MaxDigestsPerDay = maxPerDay }),
            Options.Create(new SiteOptions { BaseUrl = "https://site" }),
            NullLogger<DailyDigestService>.Instance);
        return (service, store, mailer);
    }

    /// <summary>1pm Pacific the day before <see cref="Morning"/> — comfortably owed its digest.</summary>
    private static readonly DateTime DayBefore = new(2026, 9, 10, 20, 0, 0, DateTimeKind.Utc);

    private static string Subscribe(SubscriberStore store, string email, bool confirm = true,
                                    DateTime? confirmedAtUtc = null)
    {
        var at = confirmedAtUtc ?? DayBefore;
        var token = store.Request(email, at).Token!;
        if (confirm)
        {
            store.Confirm(token, at);
        }
        return token;
    }

    [Fact]
    public async Task Confirming_after_the_send_waits_for_the_next_morning()
    {
        // 10:30pm Pacific. "Send today's now" would be a late-night email about a puzzle
        // they just saw — the thing the 7am send exists to avoid.
        var (service, store, mailer) = Build();
        Subscribe(store, "night-owl@example.com",
                  confirmedAtUtc: new DateTime(2026, 9, 12, 5, 30, 0, DateTimeKind.Utc));

        Assert.Equal(0, await service.RunOnceAsync(new DateTime(2026, 9, 11, 22, 31, 0), default));
        Assert.Equal(1, await service.RunOnceAsync(new DateTime(2026, 9, 12, 7, 0, 0), default));
    }

    [Fact]
    public async Task Confirming_before_the_send_gets_that_mornings()
    {
        var (service, store, mailer) = Build();
        Subscribe(store, "early@example.com",
                  confirmedAtUtc: new DateTime(2026, 9, 11, 13, 0, 0, DateTimeKind.Utc));   // 6am PDT

        Assert.Equal(1, await service.RunOnceAsync(Morning, default));
    }

    [Fact]
    public async Task Every_digest_goes_out_styled_with_a_plain_text_twin()
    {
        var (service, store, mailer) = Build();
        Subscribe(store, "reader@example.com");

        await service.RunOnceAsync(Morning, default);
        var mail = Assert.Single(mailer.Sent);

        Assert.StartsWith("<!DOCTYPE html>", mail.Html);
        Assert.Contains("Challenge of the day", mail.Html);
        Assert.False(string.IsNullOrWhiteSpace(mail.Body));
    }

    [Fact]
    public async Task Nothing_goes_out_before_the_send_hour()
    {
        var (service, store, mailer) = Build();
        Subscribe(store, "reader@example.com");

        Assert.Equal(0, await service.RunOnceAsync(Night, default));
        Assert.Empty(mailer.Sent);
    }

    [Fact]
    public async Task Every_confirmed_subscriber_gets_one_after_the_send_hour()
    {
        var (service, store, mailer) = Build();
        Subscribe(store, "one@example.com");
        Subscribe(store, "two@example.com");

        Assert.Equal(2, await service.RunOnceAsync(Morning, default));
        Assert.Equal(["one@example.com", "two@example.com"], mailer.Sent.Select(m => m.To).Order());
    }

    [Fact]
    public async Task An_unconfirmed_address_is_never_mailed()
    {
        // Double opt-in: nothing is broadcast to anyone who didn't click the link.
        var (service, store, mailer) = Build();
        Subscribe(store, "confirmed@example.com");
        Subscribe(store, "pending@example.com", confirm: false);

        await service.RunOnceAsync(Morning, default);

        Assert.DoesNotContain(mailer.Sent, m => m.To == "pending@example.com");
    }

    [Fact]
    public async Task Running_again_the_same_day_sends_nothing()
    {
        // The poll runs every minute. Without per-subscriber tracking, everyone would get
        // the same challenge sixty times an hour.
        var (service, store, mailer) = Build();
        Subscribe(store, "reader@example.com");

        await service.RunOnceAsync(Morning, default);
        var second = await service.RunOnceAsync(Morning.AddMinutes(1), default);
        var third = await service.RunOnceAsync(Morning.AddHours(8), default);

        Assert.Equal(0, second);
        Assert.Equal(0, third);
        Assert.Single(mailer.Sent);
    }

    [Fact]
    public async Task A_failed_send_is_retried_on_the_next_tick_not_skipped()
    {
        var (service, store, mailer) = Build();
        Subscribe(store, "flaky@example.com");
        mailer.FailFor.Add("flaky@example.com");

        await service.RunOnceAsync(Morning, default);
        Assert.Empty(mailer.Sent);

        mailer.FailFor.Clear();
        await service.RunOnceAsync(Morning.AddMinutes(1), default);

        Assert.Single(mailer.Sent);   // delivered on the retry
    }

    [Fact]
    public async Task A_new_day_sends_again()
    {
        var (service, store, mailer) = Build();
        Subscribe(store, "reader@example.com");

        await service.RunOnceAsync(Morning, default);
        await service.RunOnceAsync(Morning.AddDays(1), default);

        Assert.Equal(2, mailer.Sent.Count);
    }

    [Fact]
    public async Task The_daily_cap_stops_the_send_and_the_rest_wait()
    {
        var (service, store, mailer) = Build(maxPerDay: 2);
        for (var i = 0; i < 5; i++)
        {
            Subscribe(store, $"reader{i}@example.com");
        }

        var sent = await service.RunOnceAsync(Morning, default);

        Assert.Equal(2, sent);
        Assert.Equal(3, store.DueForDigest(DateOnly.FromDateTime(Morning)).Count);   // still owed
    }

    [Fact]
    public async Task Each_digest_carries_its_own_subscribers_unsubscribe_link_and_header()
    {
        var (service, store, mailer) = Build();
        var token = Subscribe(store, "reader@example.com");

        await service.RunOnceAsync(Morning, default);
        var mail = Assert.Single(mailer.Sent);

        Assert.Contains($"https://site/unsubscribe?token={token}", mail.Body);
        Assert.Equal($"<https://site/unsubscribe?token={token}>", mail.Headers!["List-Unsubscribe"]);
        Assert.Equal("List-Unsubscribe=One-Click", mail.Headers["List-Unsubscribe-Post"]);
    }

    [Fact]
    public async Task The_digest_matches_the_challenge_on_the_site_and_leaks_nothing()
    {
        var (service, store, mailer) = Build();
        Subscribe(store, "reader@example.com");

        await service.RunOnceAsync(Morning, default);
        var mail = Assert.Single(mailer.Sent);

        // Whatever the rotation drew for today, the email must carry that question...
        // ...and none of the answers or hints in the bank — in either version.
        foreach (var version in new[] { mail.Body, WebUtility.HtmlDecode(mail.Html!) })
        {
            Assert.Matches(@"question \d", version);
            Assert.DoesNotContain("answer ", version);
            Assert.DoesNotContain("hint ", version);
        }
    }

    public void Dispose() => _env.Dispose();
}
