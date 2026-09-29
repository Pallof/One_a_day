using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneADay.Services;

namespace OneADay.Tests;

/// <summary>
/// What stops one script spending the day's 50 confirmation emails, and what lets a real
/// sign-up try again when its confirmation never went out (security review, 2026-09-28).
///
/// <para>The stakes: with the cap spent, every real sign-up that day sees "Check your inbox"
/// and gets nothing — and until this fix, couldn't ask again for 24 hours. Done daily, it
/// means the author's Gmail emailing strangers, which is how accounts get restricted. The
/// dialog's side of the allowance is in SubscribeDialogTests.</para>
/// </summary>
public class SignUpLimitTests
{
    private static readonly DateOnly Day = new(2026, 9, 28);
    private static readonly DateTime T0 = new(2026, 9, 28, 16, 0, 0, DateTimeKind.Utc);

    private static SignUpLimit Limit(int perDay = 3) =>
        new(Options.Create(new SubscriptionOptions { MaxSignUpsPerAddressPerDay = perDay }));

    // ---- the per-address allowance ---------------------------------------------------

    [Fact]
    public void Each_address_gets_its_daily_sign_ups_and_no_more()
    {
        var limit = Limit(perDay: 3);

        Assert.True(limit.TryTake("203.0.113.7", Day));
        Assert.True(limit.TryTake("203.0.113.7", Day));
        Assert.True(limit.TryTake("203.0.113.7", Day));
        Assert.False(limit.TryTake("203.0.113.7", Day));
    }

    [Fact]
    public void One_address_running_out_leaves_the_others_alone()
    {
        var limit = Limit(perDay: 1);
        limit.TryTake("203.0.113.7", Day);

        Assert.False(limit.TryTake("203.0.113.7", Day));
        Assert.True(limit.TryTake("198.51.100.42", Day));
    }

    [Fact]
    public void A_new_day_brings_a_fresh_allowance()
    {
        var limit = Limit(perDay: 1);
        limit.TryTake("203.0.113.7", Day);

        Assert.True(limit.TryTake("203.0.113.7", Day.AddDays(1)));
    }

    [Fact]
    public void An_unknown_address_is_left_to_the_global_cap()
    {
        // The address isn't always known on a live connection. Refusing those would turn real
        // people away; the 50-a-day cap still stands behind them.
        var limit = Limit(perDay: 1);

        Assert.True(limit.TryTake(null, Day));
        Assert.True(limit.TryTake(null, Day));
    }

    [Fact]
    public void Past_the_days_address_table_new_addresses_wait_for_tomorrow()
    {
        // Keeps the table small under a flood from many addresses. Addresses already in it keep
        // their allowance.
        var limit = Limit(perDay: 2);
        for (var i = 0; i < SignUpLimit.MaxAddressesPerDay; i++)
        {
            Assert.True(limit.TryTake($"address {i}", Day));
        }

        Assert.False(limit.TryTake("one address too many", Day));
        Assert.True(limit.TryTake("address 0", Day));
        Assert.True(limit.TryTake("one address too many", Day.AddDays(1)));
    }

    // ---- the ceiling on unconfirmed sign-ups -----------------------------------------

    private static SubscriberStore StoreWithPending(TestEnvironment env, int count)
    {
        env.WriteDataFile("subscribers.json", JsonSerializer.Serialize(
            Enumerable.Range(0, count).Select(i => new Subscriber
            {
                Email = $"pending{i}@example.com",
                Token = $"token{i}",
                RequestedAt = T0,
                ConfirmationSentAt = T0,
            })));
        return env.NewSubscriberStore();
    }

    [Fact]
    public void Below_the_ceiling_a_new_address_is_stored()
    {
        using var env = new TestEnvironment();
        var store = StoreWithPending(env, SubscriberStore.MaxPending - 1);

        Assert.Equal(SubscribeOutcome.SendConfirmation, store.Request("new@example.com", T0).Outcome);
    }

    [Fact]
    public void At_the_ceiling_a_new_address_is_neither_stored_nor_sent_anything()
    {
        using var env = new TestEnvironment();
        var store = StoreWithPending(env, SubscriberStore.MaxPending);

        var result = store.Request("new@example.com", T0);

        Assert.Equal(SubscribeOutcome.ListFull, result.Outcome);
        Assert.Null(result.Token);
        Assert.Equal((0, SubscriberStore.MaxPending), store.Counts);
    }

    [Fact]
    public void At_the_ceiling_a_waiting_sign_up_can_still_ask_again()
    {
        // The ceiling stops new addresses, not the ones already waiting on a confirmation.
        using var env = new TestEnvironment();
        var store = StoreWithPending(env, SubscriberStore.MaxPending);

        var again = store.Request("pending0@example.com", T0 + SubscriberStore.ConfirmationResendAfter);

        Assert.Equal(SubscribeOutcome.SendConfirmation, again.Outcome);
    }

    [Fact]
    public void Repeating_a_sign_up_never_rewrites_the_file()
    {
        // Every save rewrites the whole list, so an outcome that changes nothing mustn't save.
        // The file is swapped for a marker behind the store's back: a save would overwrite it.
        using var env = new TestEnvironment();
        var store = env.NewSubscriberStore();
        var token = store.Request("confirmed@example.com", T0).Token!;
        store.Confirm(token, T0);
        store.Request("pending@example.com", T0);
        env.WriteDataFile("subscribers.json", "[]");

        store.Request("confirmed@example.com", T0.AddMinutes(1));
        store.Request("pending@example.com", T0.AddMinutes(1));

        Assert.Equal("[]", env.ReadDataFile("subscribers.json"));
    }

    // ---- asking again after a confirmation that never went out -----------------------

    [Fact]
    public void A_confirmation_that_never_went_out_can_be_asked_for_again_at_once()
    {
        using var env = new TestEnvironment();
        var store = env.NewSubscriberStore();
        var first = store.Request("reader@example.com", T0);
        Assert.Equal(SubscribeOutcome.AlreadyPending,
            store.Request("reader@example.com", T0.AddMinutes(5)).Outcome);   // the control

        store.ConfirmationNotSent("reader@example.com");
        var again = store.Request("reader@example.com", T0.AddMinutes(6));

        Assert.Equal(SubscribeOutcome.SendConfirmation, again.Outcome);
        Assert.Equal(first.Token, again.Token);
    }

    [Fact]
    public void An_unsent_confirmation_never_touches_a_confirmed_subscriber()
    {
        using var env = new TestEnvironment();
        var store = env.NewSubscriberStore();
        var token = store.Request("reader@example.com", T0).Token!;
        store.Confirm(token, T0);

        store.ConfirmationNotSent("reader@example.com");

        var subscriber = store.FindByToken(token)!;
        Assert.True(subscriber.IsConfirmed);
        Assert.Equal(T0, subscriber.ConfirmationSentAt);   // only a pending sign-up is reopened
    }

    private static ConfirmationSender Sender(SubscriberStore store, SmtpMailer mailer, int perDay) =>
        new(new ConfirmationQueue(Options.Create(new EmailOptions())), mailer, store,
            Options.Create(new SubscriptionOptions { MaxConfirmationsPerDay = perDay }),
            NullLogger<ConfirmationSender>.Instance);

    private static OutgoingMail Confirmation(string to) => new(to, "Confirm", "body");

    [Fact]
    public async Task Over_the_days_cap_a_confirmation_is_dropped_and_its_sign_up_can_ask_again()
    {
        using var env = new TestEnvironment();
        var store = env.NewSubscriberStore();
        store.Request("first@example.com", T0);
        store.Request("second@example.com", T0);
        var mailer = new RecordingMailer();
        var sender = Sender(store, mailer, perDay: 1);

        await sender.SendOneAsync(Confirmation("first@example.com"), CancellationToken.None);
        await sender.SendOneAsync(Confirmation("second@example.com"), CancellationToken.None);

        Assert.Equal(new[] { "first@example.com" }, mailer.Sent.Select(m => m.To));
        var later = T0.AddMinutes(1);
        Assert.Equal(SubscribeOutcome.SendConfirmation, store.Request("second@example.com", later).Outcome);
        Assert.Equal(SubscribeOutcome.AlreadyPending, store.Request("first@example.com", later).Outcome);
    }

    [Fact]
    public async Task A_confirmation_the_mail_server_refused_can_be_asked_for_again()
    {
        using var env = new TestEnvironment();
        var store = env.NewSubscriberStore();
        store.Request("reader@example.com", T0);
        var mailer = new RecordingMailer();
        mailer.FailFor.Add("reader@example.com");

        await Sender(store, mailer, perDay: 50).SendOneAsync(Confirmation("reader@example.com"), CancellationToken.None);

        Assert.Empty(mailer.Sent);
        Assert.Equal(SubscribeOutcome.SendConfirmation,
            store.Request("reader@example.com", T0.AddMinutes(1)).Outcome);
    }

    [Fact]
    public void A_confirmation_pushed_out_of_a_full_queue_can_be_asked_for_again()
    {
        using var env = new TestEnvironment();
        var store = env.NewSubscriberStore();
        var queue = new ConfirmationQueue(Options.Create(new EmailOptions
        {
            Enabled = true, To = "a@x.com", From = "a@x.com", Host = "h", AppPassword = "p",
        }), store);

        for (var i = 0; i <= ConfirmationQueue.Capacity; i++)   // one more than fits
        {
            var address = $"reader{i}@example.com";
            store.Request(address, T0);
            queue.Enqueue(Confirmation(address));
        }

        // The oldest was pushed out to make room; the next oldest is still queued.
        var later = T0.AddMinutes(1);
        Assert.Equal(SubscribeOutcome.SendConfirmation, store.Request("reader0@example.com", later).Outcome);
        Assert.Equal(SubscribeOutcome.AlreadyPending, store.Request("reader1@example.com", later).Outcome);
    }
}
