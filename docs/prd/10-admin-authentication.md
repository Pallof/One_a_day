# PRD 10 — Admin authentication

**Status:** Proposed · **Priority: P0 — blocks public launch**

## Problem

`/admin` is completely unauthenticated. Anyone who can reach the app can add, edit,
or delete teasers, read every answer before publication, and clear the suggestion and
issue queues. Today the only thing protecting it is that the app runs on localhost —
which stops being true the moment it is deployed.

The route is also linked in the public dropdown menu, so it isn't even obscure.

There are no user accounts and none are wanted ([PRD 00](00-product-overview.md)), so
this cannot be solved with a normal login system.

## Goals

- Only the author can reach `/admin` or mutate any data.
- No account system, user table, or password storage.
- Sign in once per session, not per action.
- Solvers never see a login prompt.

## Non-goals

- Multiple users, roles, or permissions
- Password reset flows, email verification, 2FA
- Protecting the public read-only pages

## Options considered

| Option | Effort | Notes |
|---|---|---|
| **A — Admin passphrase in config** ⭐ | ~½ day | Secret in an env var / user-secrets; one password box on `/admin`; unlock persists for the session. "The admin" = whoever knows the passphrase. No user records at all. |
| B — Host-level auth (reverse proxy, Azure Easy Auth, Cloudflare Access) | varies | App code unchanged; depends entirely on hosting choice; awkward locally. |
| C — Keep admin off the server | low | Deploy read-only; edit JSON locally and publish. Zero attack surface, clunky daily workflow. |
| D — Full ASP.NET Core Identity / Google OAuth | ~2 days | The "correct enterprise answer", and exactly the machinery deliberately removed. Overkill for one author. |

**Recommendation: Option A**, with Option B as defence-in-depth if the chosen host
makes it free.

## Requirements

1. The passphrase must come from **configuration or environment**, never source
   control. Committing it is a defect.
2. It must be compared against a **hash** (not stored in plaintext), using a
   **constant-time** comparison.
3. Until authenticated, `/admin` shows only a passphrase prompt — no teaser data,
   no queues, no answers.
4. Authentication must be enforced **server-side on every admin action**, not merely
   by hiding UI. A crafted request must not be able to save or delete.
5. Successful entry persists for the browser session; a **Log out** action clears it.
6. Failed attempts must be **rate-limited** (e.g. brief lockout after 5 failures) to
   frustrate brute force.
7. The public menu must stop advertising `/admin`; the author can bookmark it.
8. If no passphrase is configured, the app must **fail closed** — deny admin access
   rather than defaulting to open.
9. Admin routes must be served over **HTTPS** in production.

## Acceptance criteria

- [ ] `/admin` with no/incorrect passphrase reveals no data and permits no writes
- [ ] Correct passphrase grants access for the session; log out revokes it
- [ ] Direct POST/interop attempts without auth are rejected server-side
- [ ] Repeated wrong guesses are throttled
- [ ] No passphrase or hash appears anywhere in the repository
- [ ] Unconfigured deployment denies admin entirely
- [ ] Solvers see no auth UI on any public page

## Risks

- **Losing the passphrase** locks the author out of their own tool — recovery is
  redeploying with a new one. Acceptable, but worth documenting in the README.
- A single shared secret has no audit trail. Fine for one author; revisit if that
  ever changes.
