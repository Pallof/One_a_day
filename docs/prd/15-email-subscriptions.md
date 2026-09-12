# PRD 15 — Email subscriptions

**Status:** Spec of record · **Config:** `Subscriptions` + `Site` sections; shares the
`Email` account and secret with [PRD 14](14-email-notifications.md)

## Problem

One a Day is a daily habit with nothing that forms the habit. A visitor who enjoyed
today's puzzle has to remember to come back tomorrow, and nothing reminds them — no
accounts, no app, no notifications. For a daily puzzle, the reminder that works is the
puzzle itself arriving on its own.

## Decision

**An opt-in daily email carrying the day's question — never its answer — at 7am
Pacific**, to anyone who asks for it and confirms their address. Signing up happens on
the home page; leaving takes one click from any email.

### This is not an account

[PRD 00](00-product-overview.md)'s first principle is that nothing about a daily puzzle
needs identity. That still holds: the address is kept for one purpose — sending this
email — and nothing else is attached to it. It is not linked to answers, stats, or the
per-device visitor id; there is no login and no profile; and unsubscribing deletes it.
The site still knows nothing about what any person does on it.

## The contract

Most of what matters here is what the feature must **not** do. Each of these fails
silently, and in a way a subscriber would notice before the author did:

- **Nothing is sent to an address that didn't confirm.** Anyone can type anyone's
  address; without confirmation the form is a way to sign strangers up for daily mail
  from the author's Gmail.
- **The sign-up form says the same thing whatever happened** — new, already pending,
  already subscribed, or dropped as a bot. Anything else lets a stranger test whether an
  address is on the list.
- **No answer or hint ever reaches an inbox.** The email sends the question and a link;
  solving happens on the site, where the spoiler rules live ([PRD 03](03-hints-and-solutions.md)).
- **Never the same challenge twice, never an email at night.** One send a day, at 7am.
- **Unsubscribing deletes the address**, rather than flagging it.
- **Subscriber addresses never reach the public repository.**

## Requirements

### Signing up

- The link — "Get the daily challenge by email" — sits in the footer of the **home page
  only**. The offer is "this, every morning", which makes sense under today's challenge
  and nowhere else; elsewhere it is clutter. *(Author's decision, 2026-09-11.)*
- It opens a dialog with a single email field. Addresses go through
  `EmailAddress.Normalise`: trimmed and lower-cased, exactly one `@`, a dotted domain,
  at most 254 characters. **Control characters are rejected, never repaired** — the
  value becomes the `To` header, and a CR/LF there is header injection. The check runs
  on the raw input, before trimming, because `Trim()` would quietly remove a trailing
  line break and let the value through.
- An unusable address gets an error. That is the only message allowed to differ — it is
  about what was typed, not about who is subscribed.
- Every other outcome shows **"Check your inbox"**, and only a new sign-up (or a stale
  one, below) queues a confirmation. That screen also says to check the spam folder: mail
  from a new sender often lands there, and a confirmation nobody finds is a subscriber
  lost **silently** — they assume the sign-up failed.

**Automation checks.** The same two layers as [PRD 06](06-community-feedback.md) — a
honeypot field and a minimum time on the form — with one difference: the floor is
**1.5 seconds, not 5**. Five seconds is right for writing a riddle and wrong for an
email field: with autofill a real person can click, pick their address and press Enter
in two. Held to five, they would be silently dropped — shown "Check your inbox" and
then sent nothing, which is worse than an error. A short floor still catches the naive
scripts the check exists for, which submit in milliseconds. A caught submission sees the
ordinary confirmation screen, not an error.

**Limits on confirmation mail.** A confirmation is the one email an unconfirmed address
can receive, so it is what a hostile script would aim at:

- **At most one per pending address per 24 hours.** Signing up again sooner sends
  nothing.
- **At most 50 a day, in total** (`MaxConfirmationsPerDay`). A per-IP limit can't bound
  this — IPs rotate, and on a circuit the IP is often unknown — so a fixed global cap
  does. Past it, confirmations are **dropped, not deferred**; the sign-up stays pending
  and can be requested again after 24 hours.
- **An unconfirmed sign-up is deleted after 7 days.** There was never consent to keep it.

### Confirming

- The confirmation email links to `/subscribe/confirm?token=…`. The token is 256 random
  bits, hex-encoded, and never derived from the address — **whoever holds it controls
  the subscription**, so it must be unguessable.
- **Confirming takes a button press**, not just opening the link. Some mail filters,
  mostly corporate ones, open every link in incoming mail to scan it. If the page load
  confirmed, the scanner — not the person — would be saying yes, which defeats double
  opt-in for everyone behind one.
- Confirming twice is harmless, and a used link shows the success state.
- The confirm page shows the address **masked** (`d****g@gmail.com`), so it can say which
  subscription it means without printing the whole address to whoever holds the link.

### The daily email

- **Sent at 7am Pacific** (`SendHourPacific`). Midnight is when the challenge changes,
  but nobody wants a midnight email. *(Author's decision, 2026-09-11.)*
- **Only subscribers confirmed before that day's send get that day's email.** Someone
  confirming at 11:50pm has just seen today's challenge — the sign-up link sits under it
  — and "send it now" would be exactly the late-night email the 7am send exists to
  avoid. Their first email is the next morning's.
- **Polls once a minute** rather than sleeping until 7am: that gets DST, laptop sleep and
  restarts right with no extra code. It also heals itself. Delivery is recorded per
  subscriber, so a crash halfway down the list resumes with the rest; a server that was
  down at 7am sends when it comes back the same day. **A day missed entirely is skipped**,
  not sent late the next morning — by then it is no longer the puzzle on the site.
- **The teaser comes from `DailySchedule.ForDay`**, the path the home page uses, so the
  email and the site agree. If nobody has visited yet, the 7am send is what settles the
  day ([PRD 08](08-recycling-rotation.md)).

**What it carries:**

| Part | Content |
|---|---|
| Subject | `One a Day — Friday, September 11 · Medium` |
| Preview line | The difficulty and the start of the question |
| Body | Date, "Challenge of the day", the question with its difficulty pill, and one button: **Solve today's challenge** |
| Picture | Not embedded; a teaser with one says so, rather than sending a question that makes no sense without it |
| Footer | The site's tagline and a one-click **Unsubscribe** |
| Headers | `List-Unsubscribe` and `List-Unsubscribe-Post` ([RFC 8058](https://www.rfc-editor.org/rfc/rfc8058)) |

### How the emails look

**Styled like the site, with a plain-text twin.** Both emails go out as
`multipart/alternative`: plain text first, HTML last. Clients show the last part they
can render, so HTML wins where it's supported and the text remains everywhere else —
including for spam filters, which distrust HTML-only mail. *(Plain text alone was the
first version; the author asked for the site's look on 2026-09-11.)* The author's own
notifications ([PRD 14](14-email-notifications.md)) stay plain text on purpose.

Email HTML is its own dialect, and these rules are what make it render:

- **Every style is inline and the layout is tables.** Gmail ignores most of a
  `<style>` block, and Outlook for Windows renders with Word. The one `<style>` block
  is progressive only (phone padding, iOS data detectors).
- **Colours are literal copies of the `app.css` tokens** — email clients don't support
  CSS variables. `Models/EmailLayout.cs` must be kept in step when the palette changes.
- **Georgia and the system sans stand in for Lora and Atkinson Hyperlegible.** Webfonts
  would load only in Apple Mail.
- **Everything that isn't a fixed string is HTML-encoded**, the teaser included.
  Teasers are written by the author, not visitors, but a question about `a < b` must
  arrive as text.
- **The HTML is base64-encoded on the wire.** Its lines run far past SMTP's 998-byte
  limit, and a relay may cut a raw line mid-tag.

### Unsubscribing

**One click, no button.** The unsubscribe link in every email opens the site, which says
**"Sorry to see you go — you're now unsubscribed."** Nothing else to press. *(Author's
decision, 2026-09-11.)*

> **It happens when the page goes live in a browser, not when the page is fetched.**
> The same link scanners that open confirmation links open unsubscribe links. If the
> plain fetch unsubscribed, readers behind one would be removed the moment the digest
> landed, without ever seeing it. A scanner downloads the HTML — Blazor's prerender,
> where this page does nothing — and doesn't run the page and hold the live connection
> open, which is the step that does the work. A real reader never notices the
> difference.
>
> It is not airtight: a scanner that drives a full browser would still count. That is
> accepted. The failure costs a subscription, not an unwanted email, and it is the
> reverse of why confirmation *does* keep its button — a scanner wrongly saying "yes"
> sends mail to someone who never asked, which is the harm double opt-in exists to
> prevent.

- **Unsubscribing deletes the record** — not a flag, not a soft delete.
- **A used or broken link says "You're not subscribed"** and points to the newest email
  if mail is still arriving. Because records are deleted, a used link and a mangled one
  look the same, so the page can't honestly promise the emails have stopped.
- **Mail clients' own Unsubscribe button** (Gmail, Outlook, Apple Mail) never opens the
  page: RFC 8058 has the client POST to the same URL. That endpoint
  (`SubscriptionEndpoints`) always answers 200, so it can't be used to test which tokens
  exist. Antiforgery is off for it, which is safe — CSRF abuses credentials a browser
  already holds, and this endpoint takes none; the token *is* the authorisation.

> **Route order matters.** Blazor maps every page for form POSTs too, so `/unsubscribe`
> the page and `/unsubscribe` the endpoint both claim POST. Without `WithOrder(-1)` on
> the endpoint, that is an `AmbiguousMatchException` — a 500 to every mail client's
> button while the page looks fine in a browser. Found in testing on 2026-09-11 and now
> pinned by a test over real HTTP.

### Privacy and storage

- Subscribers live in **`App_Data/subscribers.json`**. `App_Data/` is gitignored, so the
  file is ignored from the moment it is created and can never reach the public
  repository — verified with `git check-ignore`, and a test fails if the `.gitignore`
  line is ever removed.
- **Not encrypted at rest, deliberately.** The threat is the public repo, which the
  gitignore closes completely. Encryption at rest would add protection only against
  someone who can read the server's disk — who can also read the key, since the Data
  Protection key ring lives on the same disk. And it adds a way to lose everything: lose
  the key ring (it does not survive a container restart by default —
  [PRD 11](11-deployment.md)) and the whole list is unreadable.
- **Addresses never appear in logs.** A failed send logs the subject, not the recipient —
  logs outlive subscriptions.
- **Retention is the minimum:** pending sign-ups expire after 7 days; unsubscribing
  deletes.
- **Backups of `App_Data/` now contain personal data** and must be treated that way.

### Configuration

| Key | Default | Notes |
|---|---|---|
| `Site:BaseUrl` | `http://localhost:5178` | **Must be the public address in production** (`Site__BaseUrl`). Every link in every email is built from it; a wrong value silently breaks every unsubscribe link ever sent. |
| `Subscriptions:SendHourPacific` | `7` | |
| `Subscriptions:MaxConfirmationsPerDay` | `50` | |
| `Subscriptions:MaxDigestsPerDay` | `400` | |

The Gmail account and app password are [PRD 14](14-email-notifications.md)'s. With no
password configured, nothing is queued or sent and the site works unchanged.

## Known ceiling

**This does not scale past a few hundred subscribers, and that is accepted for now.**

Gmail allows roughly **500 messages a day** from one account, and every email this site
sends shares it:

| Use | Daily cap |
|---|---|
| Author notifications ([PRD 14](14-email-notifications.md)) | 25 |
| Confirmations | 50 |
| Daily email | 400 |
| **Total** | **475** of ~500 |

So **about 400 confirmed subscribers is the hard ceiling.** Past the digest cap, the
rest of the list gets nothing that day and a warning is logged. The fix is not raising
the numbers — that only brings an account suspension closer — but moving the daily email
to a bulk provider (Resend, Postmark, SES). `SmtpMailer` is the one class that talks to
SMTP, so that change doesn't reach the store, the schedule or the wording.

Mail sent through a personal Gmail account also has mediocre deliverability, and Gmail's
own Unsubscribe button needs a public HTTPS address to POST to — it can't be tested on
`localhost`.

> **Observed on 2026-09-11: the first emails landed in Gmail's spam folder.**
> Authentication isn't the cause — both ends are Gmail, so SPF, DKIM and DMARC pass.
> The likely causes are that **every link points at `http://localhost:5178`**, which
> reads as a suspicious URL to any filter, and that the sending address has no
> reputation and no history with the recipient. Both resolve with the move above: a real
> domain, a mail provider, and `Site__BaseUrl` pointing at the live HTTPS site. Until
> then, expect spam placement in testing — and note that a confirmation lost to spam is
> a subscriber lost **silently**, which is why the sign-up dialog says to look there.

## Known gaps

- **A teaser dated today and published after 7am.** [PRD 08](08-recycling-rotation.md)'s
  precedence lets a teaser scheduled for a date outrank a day that has already settled,
  so the site switches to the new teaser while subscribers already have the old one.
  This is a PRD 08 issue the email makes visible, parked until it causes a problem.
- **A confirmation is marked sent when it's requested, not when it's delivered.** If
  Gmail fails all three attempts, or the daily cap or a full queue drops it, that
  address can't get another confirmation for 24 hours.
- **A scanner that runs a full browser can unsubscribe someone** (see Unsubscribing).

## Non-goals

- Choosing a send time or time zone per subscriber
- More than one email a day, or reminders, streak nudges, or marketing
- Embedding the teaser's picture — the email says there is one
- Open or click tracking
- A subscriber list in `/admin` (`SubscriberStore.Counts` exists; nothing shows it yet)

## Acceptance criteria

### The sign-up dialog — `SubscribeDialogTests`

- [x] A new address is stored pending and queues **exactly one** confirmation — the
      control case, without which the rest would pass on a dialog wired to nothing
- [x] An address already subscribed, or already pending, sees the **same screen** and
      queues nothing (mutation-verified: revealing the outcome fails it)
- [x] A honeypot hit and a submission inside the 1.5-second floor both see that same
      screen, and store and queue nothing (mutation-verified)
- [x] An unusable address is the one message allowed to differ
- [x] The confirmation carries an absolute link with that subscriber's own token
- [x] The screen points people at their spam folder

### Addresses and the subscriber lifecycle — `SubscriptionTests`

- [x] Valid addresses are normalised; unusable ones are rejected
- [x] A line break is rejected **before** trimming could remove it (mutation-verified)
- [x] Masking hides the address but keeps it recognisable
- [x] Signing up twice in a row sends one confirmation; after 24 hours, another
- [x] Signing up a confirmed address again sends nothing
- [x] A wrong or empty token confirms and unsubscribes nothing
- [x] Confirming twice is harmless
- [x] Unsubscribing **deletes** the record
- [x] Tokens are 64 hex characters, unique, and unrelated to the address
- [x] Unconfirmed sign-ups expire after 7 days; confirmed ones never do
- [x] Subscriptions survive a restart
- [x] `.gitignore` keeps `App_Data/` — and so the subscriber file — out of the repo

### The daily email — `DailyDigestTests`

- [x] Nothing goes out before 7am
- [x] Every confirmed subscriber gets one after 7am; an unconfirmed address never does
      (mutation-verified)
- [x] Running again the same day sends nothing (mutation-verified: never recording a
      send is caught)
- [x] A failed send is retried on the next tick, not skipped
- [x] A new day sends again
- [x] The daily cap stops the send and the rest stay owed
- [x] Confirming after the send waits for the next morning; before it, gets that
      morning's (mutation-verified)
- [x] Each email carries its own subscriber's unsubscribe link and both headers
- [x] The email carries the site's challenge and **no answer or hint — in either
      version**
- [x] Every email goes out styled, with a plain-text twin

### Content and format — `SubscriptionTests`, `SmtpMailerTests`

- [x] Both versions carry the question and the links, and neither carries an answer or
      hint
- [x] Teaser text is HTML-encoded (mutation-verified)
- [x] A teaser with a picture says so, in both versions
- [x] The difficulty pill uses the site's colour for each level
- [x] The confirmation link appears as a button **and** as printed text
- [x] On the wire: plain text first, HTML last; a plain mail stays one part; the
      unsubscribe headers survive; no line exceeds 998 characters

### Unsubscribing — `UnsubscribePageTests`, `OneClickUnsubscribeTests`

- [x] Opening the link in a browser unsubscribes, with no button on the page
- [x] The prerender — what a link scanner fetches — changes nothing, checked both in
      bUnit and over real HTTP (mutation-verified: acting on the prerender fails both)
- [x] A used or broken link says "You're not subscribed" instead of claiming success
- [x] The RFC 8058 POST unsubscribes and answers 200; an unknown token also gets 200
      (mutation-verified: removing the route order returns the 500)

### Verified by hand — 2026-09-11

- [x] End to end against Gmail, plain-text version: signed up at 12:35 PM Pacific, the
      confirmation arrived, the link confirmed, and the email arrived within a minute
- [x] The styled daily email renders at desktop and phone widths, and the confirmation
      at phone width, checked in a browser
- [x] The styled email in a real inbox — it arrived, and **landed in spam** (see Known
      ceiling)
- [x] One-click unsubscribe from a real email: the link removed the subscriber with no
      further click, and `subscribers.json` went back to an empty array
- [ ] Gmail's own Unsubscribe button — needs the public HTTPS site

### Not covered

- [ ] The confirm page has no component test.
- [ ] `ConfirmationSender` itself — its budget is `DailySendBudget`
      ([PRD 14](14-email-notifications.md)'s tests), but the drain loop is untested.

## Implementation notes

| File | Role |
|---|---|
| `Components/SubscribeDialog.razor` | The sign-up link and dialog; home page footer only (`MainLayout`) |
| `Components/Pages/ConfirmSubscription.razor` | `/subscribe/confirm` — the button that confirms |
| `Components/Pages/Unsubscribe.razor` | `/unsubscribe` — unsubscribes once the page is live |
| `Services/SubscriptionEndpoints.cs` | The RFC 8058 one-click POST |
| `Services/SubscriberStore.cs` | `subscribers.json` — lifecycle, tokens, retention |
| `Services/ConfirmationMail.cs` | Confirmation queue, and its sender with the global cap |
| `Services/DailyDigestService.cs` | The 7am send |
| `Services/SmtpMailer.cs` | SMTP, timeout, retry, and the MIME message; shared with PRD 14 |
| `Models/SubscriptionMail.cs` | The wording of both emails, HTML and plain text |
| `Models/EmailLayout.cs` | The HTML shell and parts, mirroring `app.css` |
| `Models/EmailAddress.cs` | Validation, normalisation, masking |

> The sign-up dialog runs on the SignalR circuit, so — as with [PRD 14](14-email-notifications.md) —
> it only ever **queues** a confirmation. Sending happens in the background, where a
> slow or failing SMTP server can't cost a visitor their page.
