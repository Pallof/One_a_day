# PRD 14 — Email notifications

**Status:** Spec of record · **Config:** `Email` section + `Email:AppPassword` secret

## In plain terms

When a visitor suggests a puzzle or reports a broken question, it gets saved — but nothing
told the author it had arrived. So both now also send an email. The rule that shapes
everything here: **save first, email second.** A mail problem must never cost a visitor their
submission.

## Problem

Suggestions and issue reports were visible only to someone who opened `/admin`. Nothing
announced them. The inbox held a suggestion from **15 July that went unseen until 8
September** — nearly two months.

The failure mode isn't lost data, it's silence. A broken question stays broken because the
report nobody read said so. And a visitor who reports a problem and sees nothing change
assumes reporting is pointless.

## Decision

**Both channels also email the author.** Everything is written to JSON exactly as before, and
a notification is queued afterwards.

## The contract

> **Saved first, notified second. A mail failure must never affect the save.**

`Submit()` runs on the live connection, so anything slow or throwing on that path costs a real
person their submission. The suggestion is the valuable artefact; the email is a convenience:

1. Write to the store — the visitor's confirmation depends only on this
2. Queue the notification — returns immediately, never throws
3. A background service drains the queue and sends

Consequences that must hold:

- **Enqueuing never blocks and never throws.** Every failure inside it is swallowed and logged.
- **Unconfigured is a supported state.** With no app password the notifier does nothing,
  silently. Dev, CI and an unconfigured deployment all work unchanged.
- **The queue is bounded (100) and drops when full.** Reports are uncapped by design
  ([PRD 06](06-community-feedback.md)), so an unbounded queue would be a memory leak with a
  hostile trigger. Dropping is safe — the item is already on disk.
- **Automated and rate-limited submissions never mail.** Both the bot checks and the daily
  caps sit *before* the notify call and return early.

> **That last one is load-bearing.** If a blocked submission still notified, the decoy field
> would be worse than useless on this path — it would look like protection while a script
> quietly filled the author's inbox, and an inbox has no admin page to clear it from. The
> guarantee rests on one early `return` in each component, and nothing else in the suite
> would notice if it moved, so `NotificationGatingTests` pins it directly.

## What gets sent

| Trigger | Subject | Body carries |
|---|---|---|
| New suggestion | `Stumpty — new teaser suggestion (Hard)` | difficulty, timestamp, the teaser, the solution + hint |
| Issue report | `Stumpty — issue reported (answer not accepted)` | category, page, timestamp, the teaser on screen, the details |

- **Plain text only.** Nothing to escape, renders everywhere. Subscriber mail is styled like
  the site ([PRD 15](15-email-subscriptions.md)); these stay plain because they have one
  reader, who wants to scan them.
- **Subjects carry no visitor text**, only difficulty or category — and categories are
  shortened, because the real ones are long sentences and a subject that starts with forty
  identical characters is unscannable.
- Visitor text is **stripped of line breaks before reaching any header.** A newline in a
  subject lets an attacker inject their own headers; strip rather than trust the mail library
  to notice.

## Delivery

Sending lives in `SmtpMailer`, shared with the daily email, so the timeout and retry rules
apply to both. The queue and the cap are this feature's own.

- **Gmail SMTP** (`smtp.gmail.com:587`) using the built-in client, so the project keeps its
  **zero NuGet packages**.
- Auth is a Gmail **app password**, which requires 2FA. A normal password won't work — Google
  stopped accepting those in 2022. `From` must equal the Gmail account or Gmail rejects it.
- **Three attempts** with backoff, then give up and log. The item is already saved, so a
  permanent failure costs a ping, not data.
- **15-second timeout per attempt.** The default is 100 seconds, and since the queue drains
  one message at a time, one unreachable host would block everything for over five minutes —
  during which new notifications get discarded as the queue fills.
- **A daily cap** (default 25, Pacific day). Reports are uncapped in the product, so this is
  what stops a burst burying the inbox. Hitting it skips the mail, never the save.
- **The cap counts deliveries, not attempts.** A slot is spent only once a send succeeds.
  Counting attempts would let an outage burn the whole day's budget on mail that never
  arrived — and when service returned, the first *working* notifications would be dropped.
- **Queue overflow discards the oldest, not the newest.** Under a burst the most recent
  activity is the half worth keeping. Note the queue returns "success" even when it drops, so
  overflow is detected by checking depth, not by that result.

## Untrusted field values

Form values are constrained by the *browser*, not the server — nothing verifies that a
dropdown value was one of the rendered options, so a client driving the connection directly
can send anything.

- The suggestion form's **difficulty** is forced back to a known value, falling back to
  `Medium`, before being stored or used in a subject. Unchecked, a 40,000-character subject
  that Gmail rejects would burn three retries and a cap slot.
- The report dialog's **category** is already safe — it maps through a `switch` with a default.

## Secrets

The app password is the first real secret in this project, and the repo has had content leak
into git history before ([PRD 00](00-product-overview.md)).

- **Local:** `dotnet user-secrets set "Email:AppPassword" "..."` — stored outside the repo.
- **Production:** the `Email__AppPassword` environment variable.
- **`appsettings.json` holds only non-secret shape** — recipient, host, port, cap. The
  password key is deliberately absent, not blank.

## Non-goals

- HTML email or branding for these notifications
- Notifying anyone but the author — subscriber mail is [PRD 15](15-email-subscriptions.md)
- Email as a *reply* channel — reporters are anonymous by design

## Known ceiling

**This does not scale, and that is accepted for now.** Gmail caps around 500 messages a day
and throttles well before that; this feature's 25 is one line of the shared budget in
[PRD 15](15-email-subscriptions.md). The built-in SMTP client is documented as not
recommended for new development, and mail from a cloud IP through a personal Gmail account
has mediocre deliverability — sending to yourself is the forgiving case.

**The upgrade path:** swap `SmtpMailer`'s send for a transactional API (Resend, Postmark,
SES). The queue, the contract and both call sites stay as they are. That containment is the
point of splitting the notifier from the sender.

> **The better long-term answer is probably a daily summary, not more mail.** One message a
> day carrying "2 new suggestions · 3 days of teasers left" would also fix a problem this
> project already has: the recycling box hides a dry queue from visitors
> ([PRD 08](08-recycling-rotation.md)), so the author has *no signal at all* that content is
> running low. [PRD 13](13-content-pipeline.md) proposes the warning; a summary would deliver
> it. Immediate mail was chosen first because volume is about one suggestion every two months.

## Acceptance criteria

- [x] A suggestion is saved to JSON **and** queued for email; likewise an issue report
- [x] With no app password configured, nothing is queued and nothing throws

### The guards gate the email too — `NotificationGatingTests`

- [x] A decoy hit saves nothing and queues nothing, on **both** forms
- [x] A submission faster than the 5-second floor queues nothing, on both forms
- [x] A suggestion over the daily cap queues nothing
- [x] Over the cap the form isn't rendered **and** driving `Submit()` directly still queues
      nothing — hiding the UI must not be the only thing protecting the inbox
- [x] An empty form queues nothing even when `Submit()` is invoked directly
- [x] Emails queued never exceed items stored — nothing is mailed that wasn't saved
- [x] A genuine submission on each form queues **exactly one** — the control case, without
      which everything above would pass on a notifier that was never wired up
- [x] `IsConfigured` is false if any of enabled/to/from/host/password is missing
- [x] Enqueuing 50 messages with no reader returns in well under a second
- [x] Line breaks are stripped from subjects; the body is left intact
- [x] A 5,000-message flood drops to the queue bound rather than growing
- [x] The daily cap defaults to neither zero nor unlimited

### The daily budget — `DailySendBudgetTests`

- [x] Checking for room spends nothing; only a recorded send does
- [x] **Failed sends cost nothing** — 100 failures leave the allowance untouched
- [x] A new day restores the full allowance; yesterday's total isn't carried forward
- [x] A cap of zero fails closed rather than sending without limit
- [x] Both mutation-verified: reverting to count-on-check fails 4 tests, removing the day
      rollover fails 4

### Untrusted values — `NotificationGatingTests`

- [x] A forged 40,000-character difficulty is stored as `Medium` and never reaches the subject
      (mutation-verified)
- [x] Genuine values round-trip, including differing case; unknown values fall back

### Not covered

- [x] ~~An actual send against Gmail~~ — verified by hand 2026-09-08: a suggestion submitted
      through the form arrived in the inbox
- [ ] Behaviour when Gmail rejects or throttles — the retry path has never run against a real
      server that refuses
- [ ] `EmailSenderService`'s drain loop. The cap and rollover are covered; the loop is now
      reachable (the send is injectable) but the test hasn't been written
- [ ] `SmtpMailer`'s retry and timeout — needs an SMTP server that refuses or stalls. The
      message it builds *is* tested (`SmtpMailerTests`)

## Implementation notes

Four pieces, split so the sending method can be replaced without touching the callers:

| File | Role |
|---|---|
| `Services/EmailOptions.cs` | Config + `IsConfigured` |
| `Services/EmailNotifier.cs` | The queue. Injected into components; never sends |
| `Services/EmailSenderService.cs` | Background service that drains the queue and counts against the cap |
| `Services/SmtpMailer.cs` | The SMTP send — timeout, retry, logging. Shared with [PRD 15](15-email-subscriptions.md) |

Call sites are `Pages/Contact.razor` and `Components/ReportIssue.razor`, both immediately
**after** their store write.

> The notifier is deliberately the only thing components see. A component that could reach the
> sender directly could block the connection, which is the exact failure this design prevents.
