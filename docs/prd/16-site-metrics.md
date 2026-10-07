# PRD 16 — Site metrics

**Status:** Proposed · **Priority: P2 — post-launch** · **Data:** `App_Data/metrics.json` and `App_Data/weekly-summary.json` (new)

## In plain terms

The site is live, and the author can't yet answer basic questions about it: how many people come,
whether they try the puzzle, how hard each puzzle really is, whether they come back, and whether
the recycling rotation ([PRD 08](08-recycling-rotation.md)) is spreading the puzzles evenly. Admin
shows two numbers per puzzle — times shown, and "X/Y successful" answers — but those count
**answers, not people**: one visitor guessing ten times counts as ten.

This adds a **Metrics** section to admin, built on one rule:

> **The browser remembers; the server only counts.** Each browser keeps its own small, encrypted
> note — *"I visited today, made 3 attempts, solved it"* — and the server adds one to a daily
> total when something new happens. The server never stores who did what: no visitor IDs, no
> addresses, no answers, only totals per day.

That keeps the no-accounts promise ([PRD 00](00-product-overview.md)), and it filters most bots for
free: the note is only written once a page goes live in a real browser, which page-fetching bots
never do.

Every Monday the live site also **emails the author last week's headline numbers**, so the basics
reach them without downloading anything.

### How this squares with the 2026-09-22 decision

[PRD 06](06-community-feedback.md) stopped counting people on 2026-09-22 *(author's decision)*. That
count stored an anonymous ID on the server for everyone who tried each puzzle; the IDs were never
pruned, and every answer rewrote the whole file, so the site slowed as it grew. This design differs
on exactly those points:

- **The server stores no IDs.** Each browser keeps its own note; the server keeps only totals.
- **The file grows with days, not visitors** — a few numbers per day, whatever the traffic.
- **Saves are batched**, not one rewrite per answer.

What solvers see doesn't change: PRD 06's statistics stay as they are, and its non-goals (median
attempts, time-to-solve) are about what solvers are shown. Everything here is for the author only.

## Goals

- See whether the rotation is fair: each puzzle's share of days, and of the people who saw it.
- See how each puzzle actually plays: how many tried it, how many solved it, and in how many
  attempts.
- See whether the daily habit works: how many visitors come back the next day and within a week.
- Know the size of the email list and the reach of Twenty Four.
- Judge all of it without collecting anything about any one person.

## Non-goals

- Per-person histories, profiles, or anything that follows one visitor across days on the server
- Recognising the same person across devices, or fingerprinting browsers
- Email open tracking (tracking pixels)
- Adding third-party analytics scripts
- Live or real-time numbers, or a dashboard on the live site — admin stays on the author's machine
  ([PRD 10](10-admin-authentication.md)). The Monday email is a weekly snapshot sent to the author,
  not a page anyone can open.
- **Where visitors come from, their countries and devices.** Cloudflare's Web Analytics already
  measures these for the proxied site, without cookies; read them in the Cloudflare dashboard.

## Where the numbers live

The live site writes the totals on Fly; admin reads files on the author's Mac, and nothing syncs
between them ([PRD 10](10-admin-authentication.md), *Who writes what*). So this feature includes a
**download command** that copies the live counting files down, and the Metrics section says how old
its data is. For the headline numbers, the Monday email (requirements 22–29) needs no download.

- The command copies an **allowlist** of files — `metrics.json`, `stats.json` and `rotation.json` —
  never a whole folder. An allowlist means a private file added later is left behind by default.
- It **must never copy `subscribers.json` or `keys/`.** The Mac holds the real Gmail password, so
  a real subscriber list there could mail real people. The subscriber numbers this PRD needs reach
  the Mac as totals inside `metrics.json` instead.
- **Nothing is ever copied back up.** The live site is the only writer of these files.
- **The author's machine never counts** *(author's decision, 2026-10-07: testing locally would
  only ever count the author)*. Wherever admin exists — Development, the same rule as
  [PRD 10](10-admin-authentication.md) — nothing is counted and `metrics.json` is never written;
  admin reads the file fresh from disk, so it shows the latest download, and the save time it
  shows is the live site's.

> **History — the Mac overwrote the download** (code review, 2026-10-07). As first built, the Mac
> counted like the live site. On every start its once-a-minute save stamped its own time and its
> own test subscriber totals over the downloaded file, so the freshness line and the list size
> were always the Mac's. A Mac left running during a download kept showing the old numbers, and
> its next save — of `stats.json` and `rotation.json` too, which it holds in memory — wrote them
> back over the download. Hence the rule above, and a download script that refuses to run while
> the site is running here.

## Requirements

### Who counts

1. **The unit is a browser, not a person.** A phone and a laptop are two; a cleared browser or a
   private window starts fresh. Labels say "browsers" or "visitors", never "people" or "users".
2. **Only a live browser counts.** Every count needs the browser's note saved first, and that takes
   the live connection — so a plain page fetch (a scanner, a link preview) counts nothing. Saving
   comes *before* counting: a note that can't be saved means the step isn't counted, rather than
   counted again on every click.
3. Each browser keeps one note, encrypted by the server in the browser's own storage — the same
   storage the suggestion limit ([PRD 06](06-community-feedback.md)) already uses. A note this
   server can't read is treated as a first visit, never an error.
4. Every count belongs to a **Pacific day** and to **the puzzle shown** — normally one per day,
   but a puzzle published later for an already-settled day replaces it ([PRD 08](08-recycling-rotation.md)),
   so counts are kept per day *and* per puzzle.
5. **Counting must never break a page.** A failure to count is logged and skipped, the same rule
   as email ([PRD 14](14-email-notifications.md)).

### What's counted per day

6. **Visitors** — browsers that **opened today's challenge**, or **submitted a Twenty Four answer**,
   counted once a day however many pages or reloads. Opening Twenty Four without submitting, and
   the other pages on their own, don't count *(author's decision, 2026-10-06: a visitor is someone
   who engaged with a puzzle)*. Split by when the browser last counted: **new**, **back from
   yesterday**, **back within a week** (2–7 days), or **back after longer**.
7. **The challenge funnel**, per puzzle:
   - **Saw it** — browsers that opened the challenge page live
   - **Tried it** — browsers that submitted at least one answer
   - **Solved it** — browsers that reached a correct answer
8. **Attempts to solve**, per puzzle — for each solving browser, the number of answers it took,
   **across reloads that day**, grouped as 1, 2, 3, 4 and 5+, plus the exact total so the average
   is exact. A browser that solves, comes back and solves again adds nothing the second time
   (re-solving is allowed on purpose: [PRD 06](06-community-feedback.md)).
9. **Subscribers** — confirmed and pending totals, checked every minute and recorded when they
   change, plus first-time confirmations and unsubscribes that day (both unsubscribe routes go
   through the same store, [PRD 15](15-email-subscriptions.md)). Totals only.
10. **Twenty Four** ([PRD 07](07-twenty-four.md)) — **players** (browsers that submitted at least
    one answer that day), **hands solved** and **hands passed**. Passing is a hand but not a
    submission, so it makes no player and no visitor.

The existing per-puzzle stats (every answer submitted, and how many were right) stay as they are.

### The Site metrics section in admin

11. A date range — **last 7 days**, **last 30 days** (the default), **all time** — and when the
    live site last saved the counts, which is how fresh the download is.
12. **Rotation fairness**, from the rotation history and the counts above. For each puzzle that
    was due: days shown (and how many of those were recycled), **share of all days**, **share of
    viewers** (browsers that saw a puzzle), and when it last ran. Listed **longest-unseen first**,
    with never-shown puzzles at the top. Above the table: the share of days that were recycled.
13. **The funnel per puzzle**: saw → tried → solved, with **solve rate** (solved ÷ tried), the
    attempts-to-solve spread as five small bars, and the average.
14. **The habit**, per day: visitors split as in requirement 6, and two rates:
    - **Back the next day** — of a day's visitors, the share counted "back from yesterday" on the
      next day.
    - **Had visited in the week before** — of a day's visitors, the share last seen 1–7 days
      earlier *(author's decision, 2026-10-06: a week is a generous enough window)*.

    *Why not "of a day's visitors, the share back within a week"?* Totals can't follow a browser
    past the next day without storing who visited — the very thing this design avoids. The
    next-day rate is exact because "back from yesterday" can only mean yesterday's visitors.
15. **Subscribers**: the latest list size and its date, and confirmations and unsubscribes in the
    range.
16. **Twenty Four**: players (per day, added up), hands solved and passed in the range.
17. **Every percentage shows its count beside it** (*"58% · 7 of 12"*). Below 20 browsers, a
    figure is marked **too few to judge** — at that size, one browser moves it by five points.
18. Plain bars drawn with CSS, no chart library — the project keeps its no-packages rule.

### The download command

19. `sh deploy/pull-live-data.sh`, run from the repo folder, copies `metrics.json`, `stats.json`
    and `rotation.json` from the live site into the Mac's `App_Data/`, overwriting the Mac's test
    copies of those files only. All three arrive before any is swapped in, so a failed download
    never leaves a mix of live and local files.
20. **It refuses while the site is running on the Mac** (a process named `OneADay`): the running
    site holds `stats.json` and `rotation.json` in memory and would save its old copies over the
    download. Stop the site, download, start it again.
21. A test runs the script against a stand-in for Fly and checks it asks for exactly these three
    files, so adding one is a deliberate edit to both; the script itself also refuses
    `subscribers.json`, anything in `keys/`, and any path.

### The Monday summary email

22. **Every Monday from 7am Pacific** the live site emails the author the Monday-to-Sunday week
    just ended: the same figures admin's "last 7 days" would show on that Sunday, so Monday's
    unfinished numbers are left out. If the server was down at 7am, it goes out when the server is
    back, any day up to Sunday — last week's numbers don't go stale the way a puzzle does. After
    Sunday that week is skipped.
23. **At most once a week, across restarts and redeploys.** The last week sent is saved in
    `App_Data/weekly-summary.json`, on the volume that survives a deploy
    ([PRD 11](11-deployment.md)). It's remembered in memory first, so a file that can't be written
    can't turn into a resend every minute. An unreadable file counts as not sent: at worst one
    repeat, rather than no summary ever again.
24. **A failed send is tried again an hour later**, not every minute, and nothing is recorded until
    one gets through.
25. **Mail never affects the site.** With no email set up, nothing is sent or recorded. Anything
    that goes wrong in a check is logged, and the next minute's check runs as normal. It's sent
    directly, not through the notification queue ([PRD 14](14-email-notifications.md)).
26. **Off on the author's Mac**, like counting itself. In Development, admin already shows these
    numbers, and the Mac holds the real Gmail password — left on, it would mail a second copy built
    from the Mac's data.
27. **What it says**, headline numbers first: visitors (added up, and day by day), back the next
    day, had visited in the week before, recycled days, the list's size with the week's
    confirmations and unsubscribes, Twenty Four players and hands. Then one block per puzzle that
    ran or was opened: when it ran and its share of viewers, saw → tried → solved, solve rate, and
    attempts to solve with the average. A puzzle that ran with nobody opening it is still listed,
    and a week with nothing counted still sends and says so — silence would look like the summary
    breaking.
    - Requirement 17 applies: every percentage shows its count, and below 20 browsers it's marked
      too few to judge. The average attempts is marked too, judged by how many solved.
    - Recycled days is a count of days, not a share of browsers, so it reads "3 of 7" with no
      percentage.
28. **Styled like the subscriber email, with a plain-text twin** *(author's decision, 2026-10-07:
    as plain text alone it read as a wall of words)*. Three headline tiles, a bar per day of
    visitors, a row of smaller facts, then a card per puzzle with saw → tried → solved as bars and
    attempts as five small columns. Small figures get a gold "too few to judge" tag rather than
    words in the sentence. Every bar prints its number too, so nothing is lost where bars don't
    draw. A heavy week stays well under the ~102 KB at which Gmail hides the rest of an email.
29. **Totals and puzzle questions only.** It's built from the same report as admin
    (`MetricsReport`), which holds no answers, hints, solutions or addresses and nothing about any
    one visitor. Questions are cut at 160 characters in the styled version, and to one line of 70
    in the plain text, like admin's tables. Both are HTML-encoded where they need to be.

## Not in this version

Worth adding once the basics have run for a few weeks:

- **Solve rate against the difficulty label** — flags an "Easy" puzzle that plays Hard.
- **Most common wrong answers per puzzle** — catches answers the checker should accept before
  anyone reports them. Stores visitor text, so only the top few, shortened.
- **Median time to solve.**
- **Visits from the daily email**, by tagging the email's button link.

**Hint use is left out** *(author's decision, 2026-10-06)*. [PRD 03](03-hints-and-solutions.md)
says using a hint "isn't recorded", and that stays true.

## Acceptance criteria

### Counting — `MetricsCountingTests`

- [x] A browser that reloads the challenge five times in a day counts as one visitor; the next day
      it counts as "back from yesterday" (mutation-verified)
- [x] Visits are sorted new / yesterday / within a week / longer at the right edges — 7 days is
      "within a week", 8 is not (mutation-verified) — and a note dated after today counts nothing
- [x] A page with no live connection counts nothing, and neither does a note that can't be saved
      (mutation-verified: counting before saving fails it)
- [x] Attempts made before and after a reload add up to one solve with the right attempt count;
      solves past four share the last bucket, and the average stays exact
- [x] Solving again after returning the same day adds no second solve and no attempts
      (mutation-verified)
- [x] A puzzle replaced mid-day keeps its own numbers (mutation-verified)
- [x] A failure to count never reaches the page (mutation-verified)
- [x] Twenty Four counts a visitor and a player only on submitting; a pass counts a hand only

### Storage — `MetricsStoreTests`

- [x] Counts only reach the file on a save, and survive a restart because shutting down saves
      (mutation-verified)
- [x] On the author's machine nothing is counted and nothing saved — its once-a-minute save leaves
      a downloaded file byte for byte as it was (mutation-verified)
- [x] On the author's machine admin shows the latest download without a restart, with the live
      site's save time (mutation-verified)
- [x] Unchanged list totals don't rewrite the file (mutation-verified)
- [x] A confirmation counts once — a second click on the link doesn't — and an unsubscribe counts
      once, unknown tokens never (mutation-verified)
- [x] The list reaches the file as totals; no address is ever in it (mutation-verified)
- [x] `metrics.json` holds only known counters, days, puzzle ids and the save time — checked by a
      test that scans a populated file (mutation-verified: an added field fails it)

### The report — `MetricsReportTests`

- [x] Rotation shares follow the history and add up to the whole; recycled days are counted
- [x] Longest-unseen first, never-shown on top, puzzles not yet due left out (mutation-verified)
- [x] The next-day rate divides by the day before; today has none yet (mutation-verified)
- [x] "Had visited in the week before" counts 1–7 days, not longer (mutation-verified)
- [x] Last 7 days means seven, counting today (mutation-verified)
- [x] A recycled puzzle adds up across its runs; fewer than 20 browsers is too few to judge, and
      20 is enough (mutation-verified)

### Pages — `MetricsPageTests`, `DeployFilesTests`

- [x] The daily challenge counts saw → tried → solved; any other challenge view counts nothing
      (mutation-verified)
- [x] Opening Twenty Four counts nothing until an answer is submitted (mutation-verified)
- [x] A solved Twenty Four hand can't be solved or passed again over the live connection — the
      buttons were only disabled in the browser (mutation-verified)
- [x] Admin shows each percentage with its count, marks small ones on the figure itself, and says
      so when there's nothing yet (mutation-verified)
- [x] The download script, run against a stand-in for Fly, asks for exactly the three files and
      lands them; an extra copy line or a fourth file fails it (mutation-verified — the first
      version of this test read the script's text and missed an extra copy line)
- [x] It refuses while the site runs on the Mac, before asking Fly for anything (mutation-verified;
      also by hand: with the local site running, the real script found it and stopped)

### The Monday summary — `WeeklySummaryTests`

All mutation-verified: 43 deliberate breaks on 2026-10-07, each failing the test written for it.

- [x] Nothing before 7am Monday; at 7 it goes to the author, with the week in the subject
- [x] Once a week however often it checks, and the next Monday sends the next week
- [x] A restart or redeploy doesn't send the week again. An unreadable record counts as not sent
      and is replaced. A record that can't be saved still stops a resend, both when the week is
      remembered only after a good save and when the save error escapes
- [x] A Monday the server missed goes out later that week
- [x] A failed send waits an hour, and isn't recorded as sent
- [x] Without email set up nothing is sent or recorded. In Development, and without email, the
      service stops at startup; on the live site it sends and keeps watching
- [x] An exception in a check is logged and the service keeps running
- [x] `Program.cs` starts it — every other test passes on a service nothing starts
- [x] The week is the Monday to Sunday just ended: the Sunday before and Monday morning are left out
- [x] Headlines first, then each puzzle, with how it ran and how it played. A puzzle nobody opened
      is still listed, and an empty week still sends
- [x] Every percentage shows its count — checked across the whole body, and caught when only one
      share loses it. Under 20 browsers is too few to judge, 20 is enough, and the average is
      judged by how many solved
- [x] No answer, hint or solution, whether one replaces the question or sits beside it, from the
      puzzle rows or the rotation rows
- [x] The list goes in as totals. No address does, even with real subscribers in the same
      `App_Data` and the service built by the container, as the site builds it
- [x] Styled: it goes with the plain text beside it, and the wordmark links to the site
- [x] Styled: a question's `<`, `>` and `&` are encoded, never sent as markup
- [x] Styled: every percentage a reader sees is followed by its count — tiles, viewers and solve
      rate each caught when its count goes missing
- [x] Styled: "too few to judge" tags below 20 browsers and not at 20, the average judged by how
      many solved
- [x] Styled: no answer and no address in the HTML, each caught when only the HTML leaks it
- [x] Styled: a nine-puzzle week stays under 92,000 bytes, clear of Gmail's clipping (84 KB on
      2026-10-07)

### By hand, after the next deploy

- [ ] The live site writes `metrics.json` within a minute of a visit
- [ ] `sh deploy/pull-live-data.sh` brings the live numbers into admin
- [x] Using the site on the Mac — the challenge and a 24 answer, then a shutdown — leaves
      `metrics.json` untouched (same checksum and timestamp)
- [ ] The first Monday summary arrives. Deployed after 7am on a Monday, or later in the week, it
      goes straight away, since last week's is already due; after that, Mondays at 7

## Risks

- **Small numbers mislead.** A puzzle with 9 tries can look easy or hard by chance. Requirement 17
  is the guard; resist changing the rotation on one week's data.
- **Browsers aren't people.** Private windows and cleared storage inflate visitors and deflate the
  return rate. Fine for spotting trends, wrong for absolute claims.
- **Stale data.** Admin shows whatever was last downloaded; the section says when it was saved.
- **Writes on the visitor's path.** Counting only touches memory; saving happens about once a
  minute, so a restart can lose up to a minute of counts.

## Implementation notes

- **`Models/BrowserNote.cs`** — the browser's note: the last day it counted as a visitor; today's
  puzzle, whether it was seen, attempts so far and whether it's solved; the last day it played
  Twenty Four. Every rule is a pure method on the note (`Visit`, `SeePuzzle`, `Answer`,
  `PlayTwentyFour`), so the rules are tested without a browser. It needs no ID: the server never
  has to tell browsers apart. A future streak ([PRD 12](12-streaks-and-sharing.md)) can live in
  the same note.
- **`Services/MetricsCounter.cs`** — one per live connection. Reads the note once
  (`ProtectedStorageExtensions.ReadOrDefaultAsync`, so an unreadable note is a fresh one), applies
  a rule, **saves the note, then counts**, one step at a time. Also holds `MetricsFlusher`, which
  records the list's size and saves every minute and on shutdown.
- **`Services/MetricsStore.cs`** — totals keyed by day, then puzzle, in memory; saved to
  `metrics.json` with `AtomicFile`. A year of days is a few hundred kilobytes. Where
  `AdminAccess.IsAvailable` (the author's machine) it counts nothing, never saves, and reads the
  file from disk on every `Read`.
- **Where counting happens** — `ChallengeView` (seen in `OnAfterRenderAsync`, answers in
  `CheckAnswer`; daily challenge only), `TwentyFour.razor` (`Submit`, `Pass`), and
  `SubscriberStore` (`Confirm`, `Unsubscribe`).
- **`Models/MetricsReport.cs`** works out every figure admin shows from the totals and the
  rotation history; **`Components/MetricsPanel.razor`** draws it.
- **`deploy/pull-live-data.sh`** — `fly ssh sftp get` per allowlisted file into a temporary
  folder, then swapped in.
- **`Services/WeeklySummaryService.cs`** — the Monday email. Checks once a minute like the daily
  digest ([PRD 15](15-email-subscriptions.md)), builds `MetricsReport` for the week with that
  week's Sunday as "today", sends through `SmtpMailer` directly, and records the week's Monday in
  `weekly-summary.json`. **`Models/WeeklySummaryMail.cs`** words the plain text, from the report
  alone, and **`Models/WeeklySummaryMail.Html.cs`** draws the styled version with `EmailLayout`,
  whose palette constants are now internal so both emails share them.
  `weekly-summary.json` isn't in the download allowlist: it means nothing on the Mac.
