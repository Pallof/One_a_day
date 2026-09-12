# PRD 14 — Email notifications

**Status:** Spec of record · **Config:** `Email` section + `Email:AppPassword` secret

## Problem

Suggestions and issue reports land in `App_Data/*.json` and are visible only to
someone who opens `/admin`. Nothing announces them. The inbox held a suggestion from
**15 July that went unseen until 8 September** — nearly two months.

The failure mode isn't lost data, it's silence. A broken question stays broken because
the report nobody read said so, and a good suggestion goes stale. Both are worse than
they look: a visitor who reports a problem and sees nothing change assumes reporting
is pointless.

## Decision

**Both channels also email the author.** Suggestions and issue reports are written to
JSON exactly as before, and a notification is queued afterwards.

## The contract

> **Saved first, notified second. A mail failure must never affect the save.**

This is the whole design. `Submit()` runs on the **SignalR circuit**, so anything slow
or throwing on that path costs a real person their submission. The suggestion is the
valuable artefact; the email is a convenience. Ordering follows:

1. Write to the store — the visitor's confirmation depends only on this
2. Queue the notification — returns immediately, never throws
3. A background service drains the queue and sends

Consequences that must hold:

- **Enqueuing never blocks and never throws.** Every failure inside it is swallowed
  and logged.
- **Unconfigured is a supported state.** With no app password the notifier does
  nothing, silently. Dev, CI, and an unconfigured deployment all work unchanged.
- **The queue is bounded (100) and drops when full.** Issue reports are uncapped by
  design ([PRD 06](06-community-feedback.md)), so an unbounded queue would be a memory
  leak with a hostile trigger. Dropping is safe — the item is already on disk.
- **Automated and rate-limited submissions never mail.** The `SubmissionGuard` check
  and the daily caps both sit *before* the notify call, and both return early.

> **This is the load-bearing one.** If a blocked submission still notified, the
> honeypot would be worse than useless on this path — it would look like protection
> while a script quietly filled the author's inbox. And an email inbox has no admin
> page to clear it from, so the damage outlasts the attack in a way the JSON inbox's
> does not.
>
> The whole guarantee rests on one early `return` in each component. Nothing else in
> the suite would notice if it moved, so `NotificationGatingTests` pins it directly:
> honeypot hit, too-fast submit, and over-cap all assert **zero queued** alongside
> zero saved.

## Requirements

### What gets sent

| Trigger | Subject | Body carries |
|---|---|---|
| New suggestion | `One a Day — new teaser suggestion (Hard)` | difficulty, timestamp, the teaser, the solution + hint |
| Issue report | `One a Day — issue reported (answer not accepted)` | category, page, timestamp, the teaser on screen, the details |

- **Plain text only.** Nothing to escape, renders everywhere. Subscriber mail is styled
  like the site ([PRD 15](15-email-subscriptions.md)); these stay plain on purpose —
  they have one reader, who wants to scan them.
- **Subject lines carry no visitor text**, only the difficulty or category. Categories
  are shortened for the subject — the real ones are long sentences, and a subject that
  starts with forty identical characters is unscannable.
- Visitor text is **stripped of CR/LF before reaching any header**. A newline in a
  subject is header injection; strip rather than trusting the mail library to notice.

### Delivery

Sending lives in `SmtpMailer`, shared with the daily email
([PRD 15](15-email-subscriptions.md)), so the timeout and retry rules below apply to
both. The queue and the cap are this feature's own.

- **Gmail SMTP** (`smtp.gmail.com:587`, STARTTLS) using the built-in
  `System.Net.Mail.SmtpClient`, so the project keeps its **zero NuGet packages**.
- Auth is a Gmail **app password** — requires 2FA on the account. A normal password
  will not work; Google stopped accepting those in 2022.
- `From` must equal the Gmail account. Gmail rewrites or rejects anything else.
- **Three attempts** with backoff, then give up and log. The item is already saved, so
  a permanent failure costs a ping, not data.
- **A 15-second timeout per attempt.** `SmtpClient` defaults to 100 seconds, and because
  the queue is drained one message at a time, a single unreachable host would otherwise
  block everything for over five minutes — during which new notifications are discarded
  once the queue fills.
- **A daily cap** (default 25, Pacific day), in `Models/DailySendBudget.cs`. Reports are
  uncapped in the product, so this is what stops a burst burying the inbox. Hitting it
  skips the mail, never the save.
- **The cap counts deliveries, not attempts.** A slot is spent only once a send
  succeeds. Counting attempts would let an SMTP outage burn the whole day's budget on
  mail that never arrived — and when service returned, the first *working* notifications
  of the day would be the ones dropped.
- **Queue overflow discards the oldest, not the newest.** Under a burst the most recent
  activity is the half worth keeping; `DropWrite` would have kept the first hundred and
  silently binned everything after. Note that `TryWrite` returns **true even when it
  drops**, so overflow is detected by checking queue depth, not by its return value.

### Untrusted field values

Bound form values are constrained by the *browser*, not the server — Blazor does not
verify that a `<select>` value was one of the rendered options, so a client driving the
circuit can send anything.

- The suggestion form's **difficulty** is normalised through `Enum.TryParse`, falling
  back to `Medium`, before it is stored or used in a subject. Unchecked, arbitrary text
  would be persisted to `suggestions.json` and interpolated into a mail header — a
  40,000-character subject that Gmail rejects would burn three retries and a cap slot.
- The report dialog's **category** is already safe: `ShortCategory` maps through a
  `switch` whose default is `"other"`.

### Secrets

The app password is the first real secret in this project, and the repo has had
content leak into git history before ([PRD 00](00-product-overview.md)).

- **Local:** `dotnet user-secrets set "Email:AppPassword" "..."` — stored outside the
  repo entirely.
- **Production:** the `Email__AppPassword` environment variable.
- **`appsettings.json` holds only non-secret shape** — recipient, host, port, cap.
  The password key is deliberately absent, not blank.
- The author's address stays server-side. [PRD 06](06-community-feedback.md) keeps it
  off the page on purpose; this is outbound-only and does not change that.

## Non-goals

- HTML email, templates, or branding for these notifications
- Notifying anyone but the author — subscriber mail is its own feature,
  [PRD 15](15-email-subscriptions.md)
- Email as a *reply* channel — reporters are anonymous by design
- Daily summaries for the author (see below)

## Known ceiling

**This does not scale, and that is accepted for now.**

- Gmail caps around **500 messages/day** and throttles well before that from a server
  IP. Fine at one-a-day volume; not fine if the site ever gets popular. That allowance
  is now **shared with the daily email** — this feature's 25 is one line of the budget
  in [PRD 15](15-email-subscriptions.md).
- `SmtpClient` is documented as not recommended for new development. It is adequate for
  low-volume mail to a single inbox and nothing more.
- Mail from a cloud IP through a personal Gmail account has mediocre deliverability.
  Sending to yourself is the forgiving case; anything else would need better.

**The upgrade path**, when volume or reliability justifies it: swap `SmtpMailer`'s send
for a transactional API (Resend, Postmark, SES). The queue, the contract, and both call
sites stay as they are — only the send method changes. That containment is the point of
splitting the notifier from the sender.

> **The better long-term answer is probably a daily summary, not more mail.** One message
> a day carrying "2 new suggestions · 3 days of teasers left" would also solve a problem
> this project already has: since the recycling box hides a dry queue from visitors
> ([PRD 08](08-recycling-rotation.md)), the author has *no signal at all* that content
> is running low. [PRD 13](13-content-pipeline.md) proposes the warning; a summary would
> deliver it. Immediate mail was chosen first because volume is currently about one
> suggestion every two months.

## Acceptance criteria

- [x] A suggestion is saved to JSON **and** queued for email
- [x] An issue report is saved to JSON **and** queued for email
- [x] With no app password configured, nothing is queued and nothing throws

### The guards gate the email too — `NotificationGatingTests`

- [x] A honeypot hit on the **suggestion** form saves nothing and queues nothing
- [x] A honeypot hit on the **report** dialog saves nothing and queues nothing
- [x] A submission faster than the 5-second floor queues nothing (both forms)
- [x] A suggestion over the daily cap queues nothing
- [x] Over the cap the form is not rendered **and** driving `Submit()` directly still
      queues nothing — hiding the UI must not be the only thing protecting the inbox
- [x] An empty form queues nothing even when `Submit()` is invoked directly
- [x] Emails queued never exceed items stored (nothing is mailed that wasn't saved)
- [x] A genuine submission on each form queues **exactly one** — without this control,
      the tests above would pass even if the notifier were never wired up at all
- [x] `IsConfigured` is false if any of enabled/to/from/host/password is missing
- [x] Enqueuing 50 messages with no reader returns in well under a second
- [x] CR/LF are stripped from subjects; the body is left intact
- [x] A 5,000-message flood drops to the queue bound rather than growing
- [x] The daily cap defaults to a value that is neither zero nor unlimited
- [x] Submitting still saves correctly with the notifier wired in (verified in-app)

### The daily budget — `DailySendBudgetTests`

- [x] Checking for room spends nothing; only a recorded send does
- [x] **Failed sends cost nothing** — 100 failures leave the allowance untouched
- [x] A new day restores the full allowance; yesterday's total isn't carried forward
- [x] A cap of zero fails closed rather than sending without limit
- [x] Both behaviours are mutation-verified: reverting to count-on-check fails 4 tests,
      and removing the day rollover fails 4

### Untrusted values — `NotificationGatingTests`

- [x] A forged 40,000-character difficulty is stored as `Medium` and never reaches the
      subject (mutation-verified: removing the guard fails 4 tests)
- [x] Genuine values round-trip, including differing case; unknown values fall back

### Not covered

- [x] ~~An actual send against Gmail~~ — verified manually 2026-09-08: a suggestion
      submitted through the form arrived in the inbox.
- [ ] Behaviour when Gmail rejects or throttles (the retry path has never run against a
      real server that refuses)
- [ ] `EmailSenderService` itself — the extraction of `DailySendBudget` covers the cap
      and the rollover, but the drain loop is untested. The send operation is now
      injectable (`SmtpMailer.SendAsync` is virtual, and PRD 15's tests fake it), so
      this is reachable; it just hasn't been written.
- [ ] `SmtpMailer`'s retry loop and timeout — reaching them needs an SMTP server that
      refuses or stalls. (The message it builds is tested: `SmtpMailerTests`.)

## Implementation notes

Four pieces, split so the sending method can be replaced without touching the callers:

| File | Role |
|---|---|
| `Services/EmailOptions.cs` | Config + `IsConfigured` |
| `Services/EmailNotifier.cs` | The queue. Injected into components; never sends. |
| `Services/EmailSenderService.cs` | `BackgroundService` that drains the queue and counts against the cap |
| `Services/SmtpMailer.cs` | The SMTP send — timeout, retry, logging. Shared with [PRD 15](15-email-subscriptions.md). |

Call sites are `Components/Pages/Contact.razor` and `Components/ReportIssue.razor`,
both immediately **after** their store write.

> The notifier is deliberately the only thing components see. A component that could
> reach the sender directly could block the circuit, which is the exact failure this
> design exists to prevent.
