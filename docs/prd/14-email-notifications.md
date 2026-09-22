# PRD 14 — Email notifications

**Status:** Spec of record · **Config:** `Email` settings + the `Email:AppPassword` secret

## In plain terms

Suggestions and issue reports were saved, but nothing announced them — only someone opening admin
would see them. One suggestion from **15 July went unseen until 8 September**. The failure isn't lost
data, it's silence: a broken question stays broken because nobody read the report, and a visitor who
reports a problem and sees nothing change assumes reporting is pointless.

So both now also **email the author**, under one rule: **save first, email second.** A mail problem
must never cost a visitor their submission.

## The contract

> **Saved first, notified second. A mail failure must never affect the save.**

Submitting happens over the visitor's live connection, so anything slow or failing on that path
costs a real person their submission. The submission is what matters; the email is a convenience:

1. Save it — the visitor's confirmation depends only on this.
2. Queue the notification — instant, and it never fails.
3. A background job works through the queue and sends.

So:

- **Queuing never waits and never fails**; any problem inside it is caught and logged.
- **No email set up is a supported state.** Without an app password the notifier quietly does
  nothing, so development, tests and an unconfigured server work unchanged.
- **The queue holds at most 100 and drops when full.** Reports are unlimited by design
  ([PRD 06](06-community-feedback.md)), so an endless queue would be a memory leak anyone could
  trigger. Dropping is safe — the item is already saved.
- **Blocked submissions never send mail.** The bot checks and daily limits both stop the submission
  *before* the notify step.

> **That last one matters most.** If a blocked submission still sent mail, the decoy field would be
> worse than useless — it would look like protection while a script quietly filled the author's
> inbox, which has no admin page to clear it from. It rests on one early `return` in each form, and
> nothing else would notice if that moved, so `NotificationGatingTests` pins it directly.

## What gets sent

| Trigger | Subject | Body carries |
|---|---|---|
| New suggestion | `Stumpty — new teaser suggestion (Hard)` | difficulty, time, the teaser, the solution + hint |
| Issue report | `Stumpty — issue reported (answer not accepted)` | category, page, time, the teaser on screen, the details |

- **Plain text only** — nothing to escape, displays everywhere. Subscriber emails are styled like the
  site ([PRD 15](15-email-subscriptions.md)); these have one reader, who wants to scan them.
- **Subjects carry no visitor text** — only difficulty or category, and categories are shortened: the
  real ones are long sentences, and forty identical characters at the start of every subject can't
  be scanned.
- Visitor text has **line breaks stripped before it reaches any email header** — a line break in a
  subject lets an attacker add their own headers. Strip rather than trust the mail code to notice.

## Delivery

Sending is done by `SmtpMailer`, shared with the daily email, so the timeout and retry rules cover
both; the queue and the daily cap belong to this feature.

- **Gmail** (`smtp.gmail.com:587`) through .NET's built-in mail sender, so the project still uses
  **no third-party packages**. It signs in with a Gmail **app password**, which needs two-step
  verification (Google stopped accepting normal passwords for this in 2022), and the sender address
  must be the Gmail account itself, or Gmail rejects the mail.
- **Three tries**, waiting longer each time, then give up and log — the item is already saved, so a
  permanent failure costs a ping, not data.
- **15 seconds per try.** The default is 100 seconds, and the queue sends one at a time, so one
  unreachable mail server would block everything for over five minutes while new notifications were
  thrown away as the queue filled.
- **A daily cap** (default 25, per Pacific day) stops a burst of reports burying the inbox. Hitting it
  skips the mail, never the save.
- **The cap counts deliveries, not tries.** Counting tries would let an outage burn the day's
  allowance on mail that never arrived, then drop the first *working* notifications once service
  returned.
- **Overflow drops the oldest, not the newest** — under a burst, the latest activity is the half worth
  keeping. The queue reports success even when it drops something, so overflow is detected by
  checking how full it is.

## Values the visitor controls

The browser limits what a form sends, but the server can't rely on that: nothing checks that a
dropdown value was one of the options shown, so someone driving the connection directly can send
anything.

- The suggestion form's **difficulty** is forced back to a known value (default `Medium`) before
  it's saved or put in a subject. Unchecked, a 40,000-character subject that Gmail rejects would burn
  three tries and a slot in the cap.
- The report dialog's **category** is already safe: it maps through a fixed list with a default.

## Secrets

The app password is the project's first real secret, and the repository has leaked content before
([PRD 00](00-product-overview.md)).

- **On the Mac:** `dotnet user-secrets set "Email:AppPassword" "..."` — stored outside the project.
- **On the server:** the `Email__AppPassword` environment variable.
- **`appsettings.json` holds only the non-secret parts** (recipient, server, port, cap); the password
  entry is deliberately absent, not blank.

## Non-goals

- HTML or branding for these notifications
- Emailing anyone but the author — subscriber email is [PRD 15](15-email-subscriptions.md)
- Email as a *reply* channel — reporters are anonymous by design

## Known ceiling

**This doesn't scale, and that's accepted for now.** Gmail allows roughly 500 messages a day and
slows senders well before that; this feature's 25 is one line of the shared budget in
[PRD 15](15-email-subscriptions.md). .NET's built-in mail sender is officially not recommended for new
work, and mail from a cloud server through a personal Gmail account doesn't always get delivered —
sending to yourself is the forgiving case.

**The upgrade path:** swap `SmtpMailer`'s sending for an email service (Resend, Postmark, SES). The
queue, the contract and both forms stay as they are — which is why the notifier and the sender are
separate.

> **The better long-term answer is probably one daily summary, not more mail.** A single message like
> *"2 new suggestions · 3 days of teasers left"* would also fix a gap: recycling hides a dry queue
> from visitors ([PRD 08](08-recycling-rotation.md)), and nothing warns the author — the count of
> teasers scheduled ahead sits in admin, seen only when they look. [PRD 13](13-content-pipeline.md)
> proposes the warning; a summary would deliver it. Immediate mail came first because volume is
> about one suggestion every two months.

## Acceptance criteria

- [x] A suggestion is saved to JSON **and** queued for email; likewise an issue report
- [x] With no app password configured, nothing is queued and nothing throws

### The bot checks gate the email too — `NotificationGatingTests`

- [x] A decoy hit saves and queues nothing, on **both** forms; so does a submission faster than the
      5-second minimum
- [x] A suggestion over the daily cap queues nothing — the form isn't shown, **and** calling
      `Submit()` directly still queues nothing, so hiding the form isn't the only protection; an
      empty form queues nothing even when `Submit()` is called directly
- [x] Emails queued never exceed items stored — nothing is mailed that wasn't saved
- [x] A genuine submission on each form queues **exactly one** — the control case, without which
      everything above would pass on a notifier that was never connected
- [x] `IsConfigured` is false if any of enabled/to/from/host/password is missing, and the daily cap
      defaults to neither zero nor unlimited
- [x] Queuing 50 messages with nothing reading them returns in well under a second; a 5,000-message
      flood drops to the queue limit rather than growing
- [x] Line breaks are stripped from subjects; the body is left intact

### The daily budget — `DailySendBudgetTests`

- [x] Checking for room uses nothing; only a recorded send does — **failed sends cost nothing**
      (100 failures leave the allowance untouched)
- [x] A new day restores the full allowance, without carrying yesterday's total forward
- [x] A cap of zero fails closed rather than sending without limit
- [x] Both mutation-verified: counting on the check fails 4 tests; removing the new-day reset fails 4

### Untrusted values — `NotificationGatingTests`

- [x] A forged 40,000-character difficulty is stored as `Medium` and never reaches the subject
      (mutation-verified); genuine values round-trip in any letter case, and unknown values fall back

### Not covered

- [x] ~~An actual send through Gmail~~ — verified by hand 2026-09-08: a suggestion submitted through
      the form arrived in the inbox
- [ ] What happens when Gmail rejects or slows the sender — the retry path has never run against a
      real server that refuses
- [ ] `EmailSenderService`'s sending loop. The cap and new-day reset are covered; the loop is now
      testable (the send can be swapped out) but the test isn't written
- [ ] `SmtpMailer`'s retry and timeout — needs a mail server that refuses or stalls. The message it
      builds *is* tested (`SmtpMailerTests`)

## Implementation notes

Four pieces, split so the sending method can be replaced without touching the forms:

| File | Role |
|---|---|
| `Services/EmailOptions.cs` | Settings, and `IsConfigured` |
| `Services/EmailNotifier.cs` | The queue. Given to the forms; never sends |
| `Services/EmailSenderService.cs` | Background job that works through the queue and counts against the cap |
| `Services/SmtpMailer.cs` | The send — timeout, retry, logging. Shared with [PRD 15](15-email-subscriptions.md) |

The callers, `Components/Pages/Contact.razor` and `Components/ReportIssue.razor`, queue immediately
**after** their save. The notifier is deliberately the only piece they can reach: a form that could
reach the sender directly could stall the visitor's connection — the exact failure this design
prevents.
