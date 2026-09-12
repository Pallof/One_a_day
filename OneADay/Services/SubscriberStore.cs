using System.Security.Cryptography;
using System.Text.Json;
using OneADay.Models;

namespace OneADay.Services;

public sealed class Subscriber
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Normalised through <see cref="EmailAddress.Normalise"/>.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The secret in every confirm and unsubscribe link. Whoever holds it controls the
    /// subscription, which is why it is 256 random bits and never derived from the email.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; }

    /// <summary>Null until the confirmation link is used. Nothing is sent to a null.</summary>
    public DateTime? ConfirmedAt { get; set; }

    public DateTime? ConfirmationSentAt { get; set; }

    /// <summary>
    /// The Pacific day this subscriber last received a digest. What stops a restart at
    /// 7:01 from sending everyone the same challenge twice.
    /// </summary>
    public DateOnly? LastDigestOn { get; set; }

    public bool IsConfirmed => ConfirmedAt is not null;
}

public enum SubscribeOutcome
{
    /// <summary>New, or pending long enough that another confirmation is reasonable.</summary>
    SendConfirmation,

    /// <summary>Pending, and a confirmation went out recently — send nothing.</summary>
    AlreadyPending,

    /// <summary>Already subscribed — send nothing.</summary>
    AlreadyConfirmed,
}

public sealed record SubscribeResult(SubscribeOutcome Outcome, string? Token);

/// <summary>
/// Email subscribers, in App_Data/subscribers.json.
///
/// <para><b>This file holds personal data.</b> <c>App_Data/</c> is gitignored, so it is
/// ignored from the moment it's created and can never reach the public repository —
/// verified with <c>git check-ignore</c>. It is not encrypted at rest; see PRD 15 for
/// why that would cost more than it protects.</para>
///
/// <para>Two retention rules keep it to the minimum: an address that never confirms is
/// dropped after <see cref="PendingExpiry"/>, because there was never consent to keep it;
/// and unsubscribing <b>deletes</b> the record rather than flagging it.</para>
/// </summary>
public class SubscriberStore
{
    /// <summary>
    /// How soon a second confirmation may go to the same pending address. Without this,
    /// the form becomes a way to fill a stranger's inbox with confirmation emails.
    /// </summary>
    public static readonly TimeSpan ConfirmationResendAfter = TimeSpan.FromHours(24);

    /// <summary>Unconfirmed sign-ups older than this are deleted.</summary>
    public static readonly TimeSpan PendingExpiry = TimeSpan.FromDays(7);

    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly List<Subscriber> _subscribers;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SubscriberStore(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "subscribers.json");
        _subscribers = File.Exists(_filePath)
            ? JsonSerializer.Deserialize<List<Subscriber>>(File.ReadAllText(_filePath), JsonOptions) ?? []
            : [];
    }

    /// <summary>
    /// Records a sign-up request and says whether a confirmation should be sent.
    /// </summary>
    /// <remarks>
    /// The caller must show the visitor the <b>same message whatever this returns</b>.
    /// Saying "you're already subscribed" would let anyone test whether an address is on
    /// the list, which leaks who reads the site.
    /// </remarks>
    public SubscribeResult Request(string normalisedEmail, DateTime nowUtc)
    {
        lock (_lock)
        {
            PruneExpired(nowUtc);

            var existing = _subscribers.FirstOrDefault(s => s.Email == normalisedEmail);

            if (existing is { IsConfirmed: true })
            {
                Persist();   // the prune may have removed others
                return new SubscribeResult(SubscribeOutcome.AlreadyConfirmed, null);
            }

            if (existing is not null)
            {
                if (existing.ConfirmationSentAt is { } sent && nowUtc - sent < ConfirmationResendAfter)
                {
                    Persist();
                    return new SubscribeResult(SubscribeOutcome.AlreadyPending, null);
                }

                existing.ConfirmationSentAt = nowUtc;
                Persist();
                return new SubscribeResult(SubscribeOutcome.SendConfirmation, existing.Token);
            }

            var subscriber = new Subscriber
            {
                Email = normalisedEmail,
                Token = NewToken(),
                RequestedAt = nowUtc,
                ConfirmationSentAt = nowUtc,
            };
            _subscribers.Add(subscriber);
            Persist();
            return new SubscribeResult(SubscribeOutcome.SendConfirmation, subscriber.Token);
        }
    }

    /// <summary>Marks the subscription confirmed. True if the token matched.</summary>
    public bool Confirm(string? token, DateTime nowUtc)
    {
        lock (_lock)
        {
            var subscriber = Find(token);
            if (subscriber is null)
            {
                return false;
            }
            subscriber.ConfirmedAt ??= nowUtc;   // idempotent: a second click is harmless
            Persist();
            return true;
        }
    }

    /// <summary>Deletes the subscription outright. True if the token matched.</summary>
    public bool Unsubscribe(string? token)
    {
        lock (_lock)
        {
            var subscriber = Find(token);
            if (subscriber is null)
            {
                return false;
            }
            _subscribers.Remove(subscriber);
            Persist();
            return true;
        }
    }

    /// <summary>For the confirm and unsubscribe pages, which show a masked address.</summary>
    public Subscriber? FindByToken(string? token)
    {
        lock (_lock)
        {
            return Find(token);
        }
    }

    /// <summary>Confirmed subscribers who haven't yet had <paramref name="day"/>'s digest.</summary>
    public IReadOnlyList<Subscriber> DueForDigest(DateOnly day)
    {
        lock (_lock)
        {
            return _subscribers
                .Where(s => s.IsConfirmed && s.LastDigestOn != day)
                .ToList();
        }
    }

    /// <summary>
    /// Records one subscriber's digest as sent. Per-subscriber rather than per-run, so a
    /// crash halfway through the list resumes where it stopped instead of either
    /// re-sending to the first half or skipping the second.
    /// </summary>
    public void MarkDigestSent(Guid id, DateOnly day)
    {
        lock (_lock)
        {
            var subscriber = _subscribers.FirstOrDefault(s => s.Id == id);
            if (subscriber is not null)
            {
                subscriber.LastDigestOn = day;
                Persist();
            }
        }
    }

    public (int Confirmed, int Pending) Counts
    {
        get
        {
            lock (_lock)
            {
                var confirmed = _subscribers.Count(s => s.IsConfirmed);
                return (confirmed, _subscribers.Count - confirmed);
            }
        }
    }

    /// <summary>Drops unconfirmed sign-ups past <see cref="PendingExpiry"/>.</summary>
    public int PruneExpired(DateTime nowUtc)
    {
        lock (_lock)
        {
            return _subscribers.RemoveAll(s => !s.IsConfirmed && nowUtc - s.RequestedAt > PendingExpiry);
        }
    }

    private Subscriber? Find(string? token) =>
        string.IsNullOrWhiteSpace(token)
            ? null
            : _subscribers.FirstOrDefault(s => s.Token == token);

    /// <summary>256 random bits, hex-encoded: URL-safe and unguessable.</summary>
    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private void Persist() =>
        AtomicFile.WriteAllText(_filePath, JsonSerializer.Serialize(_subscribers, JsonOptions));
}
