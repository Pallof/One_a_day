# PRD 15 — Email subscriptions

**Status:** Spec of record · **Config:** `Subscriptions` + `Site` settings; shares the Gmail account
and password with [PRD 14](14-email-notifications.md)

## In plain terms

Stumpty is a daily habit with nothing that forms the habit — no accounts, no app, no notifications.
For a daily puzzle, the reminder that works is the puzzle itself arriving.

So anyone can ask for it by email: type an address, click the link in a confirmation email to prove
it's theirs, then get one email each morning at **7am Pacific** with the day's question — never the
answer. One click in any email removes them for good. Sign-up is on the home page.

**This is not an account.** [PRD 00](00-product-overview.md)'s first principle still holds. The
address is kept for one purpose — sending this email — and linked to nothing else: not answers, not
statistics, not the anonymous device ID. No login, no profile; unsubscribing deletes it.

## The contract

Mostly this is about what must **not** happen. Each would fail silently, and a subscriber would
notice before the author did:

- **Nothing is sent to an address that hasn't confirmed.** Anyone can type anyone's address; without
  confirmation, the form is a way to sign strangers up for daily mail.
- **The form says the same thing whatever happened** — new, pending, already subscribed, or dropped as
  a bot. Anything else would let a stranger test whether an address is on the list.
- **No answer or hint ever reaches an inbox** — just the question and a link. Solving happens on the
  site, where the spoiler rules live ([PRD 03](03-hints-and-solutions.md)).
- **One send a day, at 7am.** Never the same challenge twice, never at night.
- **Unsubscribing deletes the address**, rather than marking it.
- **Subscriber addresses never reach the public repository.**

## Signing up

The link, "Get the daily challenge by email", sits in the footer of the **home page only**: the offer
is "this, every morning", which makes sense under today's challenge and is clutter anywhere else.
*(Author's decision, 2026-09-11.)*

It opens a dialog with one field. Addresses are trimmed, lower-cased, and must have exactly one `@`,
a dotted domain, and at most 254 characters. **Invisible control characters are rejected, never
repaired**: the address becomes the email's *To* line, and a line break there lets an attacker add
their own headers. The check runs on the text exactly as typed, because trimming would quietly remove
a trailing line break and let it through.

An unusable address gets an error — **the only message allowed to differ**, because it's about what
was typed, not who is subscribed. Everything else shows **"Check your inbox"**, and only a new
sign-up sends a confirmation. That screen mentions the spam folder: mail from a new sender often
lands there, and a confirmation nobody finds is a subscriber lost **silently** — they assume the
sign-up failed.

**Bot checks** are PRD 06's two — a decoy field and a minimum time on the form — but the minimum is
**1.5 seconds, not 5**: with autofill, a real person can click, pick an address and press Enter in
two seconds, and held to five they'd be silently dropped. The shorter minimum still catches naive
scripts, which submit in milliseconds. A caught bot sees the ordinary "Check your inbox".

**Confirmation limits.** A confirmation is the one email an unconfirmed address can receive, so it's
what a hostile script would aim at:

| Limit | Behaviour |
|---|---|
| One per pending address per 24 hours | Signing up again sooner sends nothing |
| 50 a day in total (`MaxConfirmationsPerDay`) | Past it, confirmations are **dropped, not delayed**; the sign-up stays pending and can be requested again after 24 hours |
| Unconfirmed sign-ups deleted after 7 days | There was never consent to keep them |

A per-IP limit can't do this — addresses change, and on a live connection are often unknown — so a
fixed daily total does.

## Confirming

- The email links to `/subscribe/confirm?token=…`. The token is 256 random bits, never derived from
  the address: **whoever holds it controls the subscription**, so it must be unguessable.
- **Confirming takes a button press**, not just opening the link. Some mail filters, mostly at
  companies, open every link in incoming mail to scan it; if loading the page confirmed, the scanner
  would be saying yes, not the person — defeating confirmation for everyone behind one.
- Confirming twice is harmless; a used link shows the success screen.
- The page shows the address **partly hidden** (`d****g@gmail.com`), so it can say which
  subscription it means without printing the address for whoever holds the link.

## The daily email

- **Sent at 7am Pacific** (`SendHourPacific`). The challenge changes at midnight, but nobody wants a
  midnight email. *(Author's decision, 2026-09-11.)*
- **Only people confirmed before that day's send get that day's email.** Someone confirming at
  11:50pm has just seen today's challenge (the sign-up link sits under it); emailing them straight
  away would be exactly the late-night email the 7am rule avoids.
- **Checks once a minute** rather than sleeping until 7am, which copes with daylight saving, laptop
  sleep and restarts for free. Each delivery is recorded, so a crash halfway down the list resumes,
  and a server that was down at 7am sends when it returns the same day. **A day missed entirely is
  skipped** — by then it's no longer the puzzle on the site.
- **The teaser comes from `DailySchedule.ForDay`**, the same path as the home page, so email and site
  always agree. If nobody has visited yet, the 7am send is what settles the day
  ([PRD 08](08-recycling-rotation.md)).

| Part | Content |
|---|---|
| Subject | `Stumpty — Friday, September 11 · Medium` |
| Preview line | Difficulty and the start of the question |
| Body | Date, "Challenge of the day", the question with its difficulty pill, one button: **Solve today's challenge** |
| Picture | Not included; a teaser with one says so, rather than sending a question that makes no sense without it |
| Footer | The site tagline and a one-click **Unsubscribe** |
| Headers | `List-Unsubscribe` and `List-Unsubscribe-Post` — the standard ([RFC 8058](https://www.rfc-editor.org/rfc/rfc8058)) that lets mail apps show their own Unsubscribe button |

### How the emails look

**Styled like the site, with a plain-text twin.** Every email carries both — plain text first, the
styled (HTML) version last. Mail apps show the last one they can display, so the styled version wins
where it can and plain text remains everywhere else, including for spam filters, which distrust
styled-only mail. *(History: plain text alone came first; the author asked for the site's look on
2026-09-11.)* The author's own notifications ([PRD 14](14-email-notifications.md)) stay plain on
purpose.

Styled email is its own dialect. Five rules make it display properly:

| Rule | Because |
|---|---|
| Every style written inline, layout built with tables | Gmail ignores most shared style blocks; Outlook draws email with Word |
| Colours are literal copies of the `app.css` values | Mail apps don't understand the site's colour names (CSS variables) — keep `EmailLayout.cs` in step when the palette changes |
| Georgia and the system's sans-serif stand in for Lora and Atkinson Hyperlegible | Web fonts only load in Apple Mail |
| Everything that isn't fixed text is escaped, teasers included | A question about `a < b` must arrive as text |
| The HTML is base64-encoded for sending | Its lines run past email's 998-character line limit, and a relay may cut one mid-tag |

## Unsubscribing

**One click, no button.** The link opens the site, which says **"Sorry to see you go — you're now
unsubscribed."** Nothing else to press. *(Author's decision, 2026-09-11.)*

It happens only when the page **actually runs in a browser**, not when it's merely fetched. The
scanners that open confirmation links open unsubscribe links too; if a fetch unsubscribed, people
behind one would be removed the moment the email landed. A scanner downloads the page but doesn't
run it and hold the live connection open — the step that does the work. Not airtight (a scanner
driving a full browser still counts) and accepted, because that failure costs a subscription rather
than mailing someone who never asked. That imbalance is exactly why confirmation *does* keep its
button.

- **Unsubscribing deletes the record** — not a flag, not a soft delete. So a used link and a mangled
  one look the same: both say **"You're not subscribed"** and point to the newest email if mail is
  still arriving, because the page can't honestly promise the emails have stopped.
- **Mail apps' own Unsubscribe button** never opens the page: under RFC 8058 the app sends a direct
  request (a POST) to the same address. That always answers "OK" (200), so it can't be used to test
  which tokens exist.

> **History — route order matters (found 2026-09-11).** The framework lets every page accept form
> posts, so the `/unsubscribe` page and the `/unsubscribe` endpoint both claimed the mail app's POST.
> Without `WithOrder(-1)` on the endpoint, every mail app's button got a server error (500) while the
> page still looked fine in a browser. Now pinned by a test over real requests.

## Privacy and storage

- Subscribers live in **`App_Data/subscribers.json`**, excluded from the repository from the moment
  it's created — checked with `git check-ignore`, and a test fails if the exclusion is removed.
- **Not encrypted on disk, deliberately.** The threat is the public repository, which the exclusion
  closes completely. Encryption would only help against someone who can read the server's disk — who
  could read the key too, since it's on the same disk — and it adds a way to lose the whole list.
- **Addresses never appear in logs.** A failed send logs the subject, not the recipient; logs outlive
  subscriptions.
- **Backups of `App_Data/` now contain personal data** and must be treated that way.

## Configuration

| Setting | Default | Notes |
|---|---|---|
| `Site:BaseUrl` | `http://localhost:5178` | **Must be the public address in production.** Every link in every email is built from it; a wrong value silently breaks every unsubscribe link ever sent |
| `Subscriptions:SendHourPacific` | `7` | |
| `Subscriptions:MaxConfirmationsPerDay` | `50` | |
| `Subscriptions:MaxDigestsPerDay` | `400` | |

The Gmail account and password belong to [PRD 14](14-email-notifications.md); with no password set,
nothing is queued or sent and the site works unchanged.

## Known ceiling

**This doesn't scale past a few hundred subscribers, and that's accepted for now.** Gmail allows
roughly 500 messages a day from one account, and everything shares it:

| Use | Daily cap |
|---|---|
| Author notifications ([PRD 14](14-email-notifications.md)) | 25 |
| Confirmations | 50 |
| Daily email | 400 |
| **Total** | **475** of ~500 |

So **about 400 confirmed subscribers is the hard ceiling.** Past the daily-email cap, the rest of the
list gets nothing that day and a warning is logged. The fix isn't raising the numbers — that only
brings an account suspension closer — but moving the daily email to a bulk email service (Resend,
Postmark, SES). `SmtpMailer` is the only part that talks to the mail server, so that change doesn't
touch storage, scheduling or wording.

> **History — the first emails landed in spam (2026-09-11).** Not a sign-in problem: both ends are
> Gmail, so the standard sender checks (SPF, DKIM, DMARC) pass. The likely causes: every link pointed
> at `http://localhost:5178`, which looks suspicious to any filter, and the sending address had no
> reputation. A real domain, an email service and `Site__BaseUrl` pointing at the live site fix both;
> until then, expect spam in testing.

## Known gaps

- **A teaser dated today and published after 7am** takes over a day that has already settled
  ([PRD 08](08-recycling-rotation.md)), so the site switches while subscribers already have the old
  one. A PRD 08 issue the email makes visible; parked.
- **A confirmation counts as sent when requested, not when delivered.** If Gmail fails all three
  tries, that address can't get another for 24 hours.
- **A scanner running a full browser can unsubscribe someone** (see Unsubscribing).

## Non-goals

- A send time or time zone per subscriber
- More than one email a day, reminders, streak nudges or marketing
- Putting the teaser's picture in the email — the email says there is one
- Open or click tracking
- A subscriber list in `/admin`

## Acceptance criteria

### Sign-up dialog — `SubscribeDialogTests`

- [x] A new address is stored as pending and queues **exactly one** confirmation — the control case,
      without which the rest would pass on a dialog connected to nothing
- [x] An already-subscribed or already-pending address sees the **same screen** and queues nothing
      (mutation-verified)
- [x] A decoy hit and a submission inside 1.5 seconds both see that same screen and store nothing
      (mutation-verified)
- [x] An unusable address is the one message allowed to differ
- [x] The confirmation carries a full link with the subscriber's own token, and the screen points to
      the spam folder

### Addresses and lifecycle — `SubscriptionTests`

- [x] Valid addresses normalised, unusable ones rejected; a line break is rejected **before** trimming
      could remove it (mutation-verified); masking hides the address but keeps it recognisable
- [x] Signing up twice sends one confirmation, and another after 24 hours; a confirmed address
      signing up again gets nothing
- [x] A wrong or empty token confirms and unsubscribes nothing; confirming twice is harmless
- [x] Unsubscribing **deletes** the record
- [x] Tokens are 64 hex characters, unique, and unrelated to the address
- [x] Unconfirmed sign-ups expire after 7 days; confirmed ones never do
- [x] Subscriptions survive a restart, and `.gitignore` keeps the file out of the repository

### The daily email — `DailyDigestTests`

- [x] Nothing before 7am; after it, every confirmed subscriber gets one and an unconfirmed address
      never does (mutation-verified)
- [x] Running again the same day sends nothing (mutation-verified); a failed send retries on the next
      check; a new day sends again
- [x] The daily cap stops the send, and the rest stay owed
- [x] Confirming after the send waits for next morning; before it, gets that morning's
      (mutation-verified)
- [x] Each email carries its own unsubscribe link and both headers, and the site's challenge with
      **no answer or hint, in either version**

### Content and format — `SubscriptionTests`, `SmtpMailerTests`

- [x] Both versions carry the question and links, and neither an answer nor a hint; a teaser with a
      picture says so in both
- [x] Teaser text is HTML-encoded (mutation-verified)
- [x] The difficulty pill uses the site's colour for each level; the confirmation link appears as a
      button **and** as printed text
- [x] As sent: plain text first, HTML last; a plain email stays one part; the unsubscribe headers
      survive; no line exceeds 998 characters

### Unsubscribing — `UnsubscribePageTests`, `OneClickUnsubscribeTests`

- [x] Opening the link in a browser unsubscribes, with no button on the page
- [x] A plain fetch — what a scanner does — changes nothing, checked in both a component test and
      over real requests (mutation-verified)
- [x] A used or broken link says "You're not subscribed" instead of claiming success
- [x] The RFC 8058 POST unsubscribes and answers 200; an unknown token also gets 200
      (mutation-verified: removing the route order brings back the 500)

### Verified by hand — 2026-09-11

- [x] End to end through Gmail: signed up, the confirmation arrived, the link confirmed, and the
      daily email arrived within a minute
- [x] The styled email displays correctly at desktop and phone widths
- [x] The styled email in a real inbox — it arrived, and **landed in spam** (see Known ceiling)
- [x] One-click unsubscribe from a real email removed the subscriber with no further click
- [ ] Gmail's own Unsubscribe button — needs the public HTTPS site

### Not covered

- [ ] The confirm page has no component test
- [ ] `ConfirmationSender`'s sending loop (its budget is covered by PRD 14's tests)

## Implementation notes

The dialog is `Components/SubscribeDialog.razor` (home page footer only); the confirm and unsubscribe
pages are `Components/Pages/ConfirmSubscription.razor` and `Unsubscribe.razor`; the one-click POST is
`Services/SubscriptionEndpoints.cs`. `Services/SubscriberStore.cs` owns `subscribers.json` — the
lifecycle, tokens and retention; `Services/ConfirmationMail.cs` queues and sends confirmations under
the daily total; `Services/DailyDigestService.cs` is the 7am send; `Services/SmtpMailer.cs` sends
everything (shared with PRD 14). The emails' wording is `Models/SubscriptionMail.cs`, their HTML shell
`Models/EmailLayout.cs` (mirroring `app.css`), and address checking and masking
`Models/EmailAddress.cs`.

> The dialog runs on the visitor's live connection, so it only ever **queues** a confirmation. Sending
> happens in the background, where a slow mail server can't cost a visitor their page.
