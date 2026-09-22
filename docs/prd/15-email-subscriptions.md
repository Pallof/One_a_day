# PRD 15 — Email subscriptions

**Status:** Spec of record · **Config:** `Subscriptions` + `Site` sections; shares the
Gmail account and secret with [PRD 14](14-email-notifications.md)

## In plain terms

Someone who enjoys today's puzzle has no reason to remember to come back tomorrow — there
are no accounts, no app, no notifications. So they can ask for the puzzle to arrive by
email instead: type an address, click a link in a confirmation email to prove it's
theirs, then get one email each morning at 7am with the day's question. Never the answer.
One click in any email removes them permanently.

## Problem

Stumpty is a daily habit with nothing that forms the habit. For a daily puzzle, the
reminder that works is the puzzle itself arriving on its own.

## Decision

**An opt-in daily email carrying the day's question — never its answer — at 7am
Pacific**, to anyone who asks and confirms their address. Sign-up is on the home page;
leaving takes one click from any email.

**This is not an account.** [PRD 00](00-product-overview.md)'s first principle — nothing
about a daily puzzle needs identity — still holds. The address is kept for one purpose,
sending this email, and is linked to nothing else: not answers, not stats, not the
per-device visitor id. No login, no profile, and unsubscribing deletes it.

## The contract

Most of what matters is what this must **not** do. Each of these fails silently, and a
subscriber would notice before the author did:

- **Nothing is sent to an address that didn't confirm.** Anyone can type anyone's
  address; without confirmation the form is a way to sign strangers up for daily mail.
- **The form says the same thing whatever happened** — new, pending, already subscribed,
  or dropped as a bot. Anything else lets a stranger test whether an address is on the list.
- **No answer or hint ever reaches an inbox.** The email carries the question and a link;
  solving happens on the site, where the spoiler rules live ([PRD 03](03-hints-and-solutions.md)).
- **One send a day, at 7am.** Never the same challenge twice, never an email at night.
- **Unsubscribing deletes the address**, rather than flagging it.
- **Subscriber addresses never reach the public repository.**

## Signing up

The link — "Get the daily challenge by email" — sits in the footer of the **home page
only**. The offer is "this, every morning", which makes sense under today's challenge and
is clutter anywhere else. *(Author's decision, 2026-09-11.)*

It opens a dialog with one email field. Addresses are trimmed, lower-cased, and checked
for exactly one `@`, a dotted domain, and at most 254 characters. **Control characters
are rejected, never repaired** — the address becomes the `To` header, and a line break
there lets an attacker inject their own headers. The check runs on the raw input, before
trimming, because trimming would quietly remove a trailing line break and let it through.

An unusable address gets an error — **the only message allowed to differ**, because it is
about what was typed, not about who is subscribed. Every other outcome shows **"Check your
inbox"**, and only a genuinely new sign-up queues a confirmation. That screen also points
at the spam folder: mail from a new sender often lands there, and a confirmation nobody
finds is a subscriber lost **silently**, because they assume the sign-up failed.

**Bot checks** are the same two layers as [PRD 06](06-community-feedback.md) — a decoy
field and a minimum time on the form — with the floor at **1.5 seconds, not 5**. With
autofill a real person can click, pick an address and press Enter in two seconds; held to
five they'd be silently dropped. A short floor still catches the naive scripts that submit
in milliseconds. A caught submission sees the ordinary confirmation screen.

**Limits on confirmation mail.** A confirmation is the one email an unconfirmed address
can receive, so it is what a hostile script would aim at:

| Limit | Behaviour |
|---|---|
| One per pending address per 24 hours | Signing up again sooner sends nothing |
| 50 a day in total (`MaxConfirmationsPerDay`) | Past it, confirmations are **dropped, not deferred**; the sign-up stays pending and can be requested again after 24 hours |
| Unconfirmed sign-ups deleted after 7 days | There was never consent to keep them |

A per-IP limit can't bound this — IPs rotate, and are often unknown on a live connection
— so a fixed global cap does.

## Confirming

- The email links to `/subscribe/confirm?token=…`. The token is 256 random bits, never
  derived from the address: **whoever holds it controls the subscription**, so it must be
  unguessable.
- **Confirming takes a button press**, not just opening the link. Some mail filters, mostly
  corporate, open every link in incoming mail to scan it. If loading the page confirmed,
  the scanner — not the person — would be saying yes, defeating confirmation for everyone
  behind one.
- Confirming twice is harmless; a used link shows the success state.
- The page shows the address **masked** (`d****g@gmail.com`), so it can say which
  subscription it means without printing the address to whoever holds the link.

## The daily email

- **Sent at 7am Pacific** (`SendHourPacific`). Midnight is when the challenge changes, but
  nobody wants a midnight email. *(Author's decision, 2026-09-11.)*
- **Only subscribers confirmed before that day's send get that day's email.** Someone
  confirming at 11:50pm has just seen today's challenge — the sign-up link sits under it —
  and sending immediately would be exactly the late-night email the 7am rule avoids.
- **Polls once a minute** rather than sleeping until 7am, which handles DST, laptop sleep
  and restarts with no extra code. Delivery is recorded per subscriber, so a crash halfway
  down the list resumes, and a server down at 7am sends when it returns the same day. **A
  day missed entirely is skipped** — by then it is no longer the puzzle on the site.
- **The teaser comes from `DailySchedule.ForDay`**, the same path the home page uses, so
  the email and the site always agree. If nobody has visited yet, the 7am send is what
  settles the day ([PRD 08](08-recycling-rotation.md)).

| Part | Content |
|---|---|
| Subject | `Stumpty — Friday, September 11 · Medium` |
| Preview line | Difficulty and the start of the question |
| Body | Date, "Challenge of the day", the question with its difficulty pill, one button: **Solve today's challenge** |
| Picture | Not embedded; a teaser with one says so, rather than sending a question that makes no sense without it |
| Footer | The site tagline and a one-click **Unsubscribe** |
| Headers | `List-Unsubscribe` and `List-Unsubscribe-Post` ([RFC 8058](https://www.rfc-editor.org/rfc/rfc8058)) |

### How the emails look

**Styled like the site, with a plain-text twin.** Every email goes out in two versions at
once — plain text first, HTML last. Clients show the last one they can render, so HTML
wins where supported and text remains everywhere else, including for spam filters, which
distrust HTML-only mail. *(Plain text alone was the first version; the author asked for the
site's look on 2026-09-11.)* The author's own notifications ([PRD 14](14-email-notifications.md))
stay plain text on purpose.

Email HTML is its own dialect. These five rules are what make it render:

| Rule | Because |
|---|---|
| Every style inline, layout in tables | Gmail ignores most of a `<style>` block; Outlook renders with Word |
| Colours are literal copies of the `app.css` values | Email clients don't support CSS variables — keep `EmailLayout.cs` in step when the palette changes |
| Georgia and system sans stand in for Lora and Atkinson Hyperlegible | Webfonts load only in Apple Mail |
| Everything not a fixed string is HTML-encoded, teasers included | A question about `a < b` must arrive as text |
| The HTML is base64-encoded on the wire | Its lines run past SMTP's 998-byte limit and a relay may cut one mid-tag |

## Unsubscribing

**One click, no button.** The link opens the site, which says **"Sorry to see you go —
you're now unsubscribed."** Nothing else to press. *(Author's decision, 2026-09-11.)*

It happens when the page **runs in a real browser**, not when it is merely fetched. The
same scanners that open confirmation links open unsubscribe links; if a plain fetch
unsubscribed, readers behind one would be removed the moment the email landed. A scanner
downloads the HTML but doesn't run the page and hold the live connection open, which is the
step that does the work. Not airtight — a scanner driving a full browser would still count
— and accepted, because the failure costs a subscription rather than sending mail to
someone who never asked. That asymmetry is exactly why confirmation *does* keep its button.

- **Unsubscribing deletes the record** — not a flag, not a soft delete.
- **A used or broken link says "You're not subscribed"** and points to the newest email if
  mail is still arriving. Because records are deleted, a used link and a mangled one look
  identical, so the page can't honestly promise the emails have stopped.
- **Mail clients' own Unsubscribe button** never opens the page: RFC 8058 has the client
  POST to the same URL. That endpoint always answers 200, so it can't be used to test which
  tokens exist.

> **Route order matters.** Blazor maps every page for form submissions too, so
> `/unsubscribe` the page and `/unsubscribe` the endpoint both claim POST. Without
> `WithOrder(-1)` on the endpoint that's a 500 to every mail client's button, while the
> page looks fine in a browser. Found 2026-09-11, now pinned by a test over real HTTP.

## Privacy and storage

- Subscribers live in **`App_Data/subscribers.json`**, which is gitignored from the moment
  it is created — verified with `git check-ignore`, and a test fails if the ignore line is
  ever removed.
- **Not encrypted at rest, deliberately.** The threat is the public repo, which the
  gitignore closes completely. Encryption would only help against someone who can read the
  server's disk — who can also read the key, since it lives on the same disk — and it adds
  a way to lose the whole list.
- **Addresses never appear in logs.** A failed send logs the subject, not the recipient;
  logs outlive subscriptions.
- **Retention is the minimum:** pending sign-ups expire after 7 days, unsubscribing deletes.
- **Backups of `App_Data/` now contain personal data** and must be treated that way.

## Configuration

| Key | Default | Notes |
|---|---|---|
| `Site:BaseUrl` | `http://localhost:5178` | **Must be the public address in production.** Every link in every email is built from it; a wrong value silently breaks every unsubscribe link ever sent |
| `Subscriptions:SendHourPacific` | `7` | |
| `Subscriptions:MaxConfirmationsPerDay` | `50` | |
| `Subscriptions:MaxDigestsPerDay` | `400` | |

The Gmail account and app password belong to [PRD 14](14-email-notifications.md). With no
password configured, nothing is queued or sent and the site works unchanged.

## Known ceiling

**This does not scale past a few hundred subscribers, and that is accepted for now.**
Gmail allows roughly 500 messages a day from one account, and everything shares it:

| Use | Daily cap |
|---|---|
| Author notifications ([PRD 14](14-email-notifications.md)) | 25 |
| Confirmations | 50 |
| Daily email | 400 |
| **Total** | **475** of ~500 |

So **about 400 confirmed subscribers is the hard ceiling.** Past the digest cap the rest of
the list gets nothing that day and a warning is logged. The fix is not raising the numbers
— that only brings an account suspension closer — but moving the daily email to a bulk
provider (Resend, Postmark, SES). `SmtpMailer` is the only class that talks to SMTP, so
that change doesn't reach the store, the schedule or the wording.

> **The first emails landed in Gmail's spam folder (2026-09-11).** Not an authentication
> problem — both ends are Gmail, so SPF, DKIM and DMARC pass. The likely causes are that
> every link pointed at `http://localhost:5178`, which reads as suspicious to any filter,
> and a sending address with no reputation. Both resolve with a real domain, a mail
> provider, and `Site__BaseUrl` pointing at the live site. Until then expect spam placement
> in testing — and note a confirmation lost to spam is a subscriber lost *silently*, which
> is why the dialog says to look there.

## Known gaps

- **A teaser dated today and published after 7am.** [PRD 08](08-recycling-rotation.md)'s
  precedence lets it outrank a day that has already settled, so the site switches while
  subscribers already have the old one. A PRD 08 issue the email makes visible; parked.
- **A confirmation is marked sent when requested, not when delivered.** If Gmail fails all
  three attempts, that address can't get another confirmation for 24 hours.
- **A scanner running a full browser can unsubscribe someone** (see Unsubscribing).

## Non-goals

- A send time or time zone per subscriber
- More than one email a day, or reminders, streak nudges, or marketing
- Embedding the teaser's picture — the email says there is one
- Open or click tracking
- A subscriber list in `/admin`

## Acceptance criteria

### Sign-up dialog — `SubscribeDialogTests`

- [x] A new address is stored pending and queues **exactly one** confirmation — the control
      case, without which the rest would pass on a dialog wired to nothing
- [x] An already-subscribed or already-pending address sees the **same screen** and queues
      nothing (mutation-verified)
- [x] A decoy hit and a submission inside 1.5 seconds both see that same screen and store
      nothing (mutation-verified)
- [x] An unusable address is the one message allowed to differ
- [x] The confirmation carries an absolute link with that subscriber's own token
- [x] The screen points people at their spam folder

### Addresses and lifecycle — `SubscriptionTests`

- [x] Valid addresses normalised, unusable ones rejected
- [x] A line break is rejected **before** trimming could remove it (mutation-verified)
- [x] Masking hides the address but keeps it recognisable
- [x] Signing up twice sends one confirmation; after 24 hours, another
- [x] Signing up a confirmed address again sends nothing
- [x] A wrong or empty token confirms and unsubscribes nothing; confirming twice is harmless
- [x] Unsubscribing **deletes** the record
- [x] Tokens are 64 hex characters, unique, unrelated to the address
- [x] Unconfirmed sign-ups expire after 7 days; confirmed ones never do
- [x] Subscriptions survive a restart, and `.gitignore` keeps the file out of the repo

### The daily email — `DailyDigestTests`

- [x] Nothing before 7am; every confirmed subscriber gets one after, an unconfirmed address
      never does (mutation-verified)
- [x] Running again the same day sends nothing (mutation-verified)
- [x] A failed send retries on the next tick; a new day sends again
- [x] The daily cap stops the send and the rest stay owed
- [x] Confirming after the send waits for next morning; before it, gets that morning's
      (mutation-verified)
- [x] Each email carries its own unsubscribe link and both headers
- [x] The email carries the site's challenge and **no answer or hint, in either version**

### Content and format — `SubscriptionTests`, `SmtpMailerTests`

- [x] Both versions carry the question and links, neither carries an answer or hint
- [x] Teaser text is HTML-encoded (mutation-verified)
- [x] A teaser with a picture says so, in both versions
- [x] The difficulty pill uses the site's colour for each level
- [x] The confirmation link appears as a button **and** as printed text
- [x] On the wire: plain text first, HTML last; a plain mail stays one part; unsubscribe
      headers survive; no line exceeds 998 characters

### Unsubscribing — `UnsubscribePageTests`, `OneClickUnsubscribeTests`

- [x] Opening the link in a browser unsubscribes, with no button on the page
- [x] A plain fetch — what a scanner does — changes nothing, checked in both bUnit and real
      HTTP (mutation-verified)
- [x] A used or broken link says "You're not subscribed" instead of claiming success
- [x] The RFC 8058 POST unsubscribes and answers 200; an unknown token also gets 200
      (mutation-verified: removing the route order returns the 500)

### Verified by hand — 2026-09-11

- [x] End to end against Gmail: signed up, confirmation arrived, link confirmed, email
      arrived within a minute
- [x] The styled email renders at desktop and phone widths
- [x] The styled email in a real inbox — arrived, and **landed in spam** (see Known ceiling)
- [x] One-click unsubscribe from a real email removed the subscriber with no further click
- [ ] Gmail's own Unsubscribe button — needs the public HTTPS site

### Not covered

- [ ] The confirm page has no component test
- [ ] `ConfirmationSender`'s drain loop (its budget is covered by PRD 14's tests)

## Implementation notes

| File | Role |
|---|---|
| `Components/SubscribeDialog.razor` | Sign-up link and dialog; home page footer only |
| `Components/Pages/ConfirmSubscription.razor` | `/subscribe/confirm` — the confirm button |
| `Components/Pages/Unsubscribe.razor` | `/unsubscribe` — unsubscribes once the page is live |
| `Services/SubscriptionEndpoints.cs` | The RFC 8058 one-click POST |
| `Services/SubscriberStore.cs` | `subscribers.json` — lifecycle, tokens, retention |
| `Services/ConfirmationMail.cs` | Confirmation queue and sender, with the global cap |
| `Services/DailyDigestService.cs` | The 7am send |
| `Services/SmtpMailer.cs` | SMTP, timeout, retry, MIME; shared with PRD 14 |
| `Models/SubscriptionMail.cs` | The wording of both emails |
| `Models/EmailLayout.cs` | The HTML shell, mirroring `app.css` |
| `Models/EmailAddress.cs` | Validation, normalisation, masking |

> The dialog runs on the live connection, so it only ever **queues** a confirmation.
> Sending happens in the background, where a slow SMTP server can't cost a visitor their page.
