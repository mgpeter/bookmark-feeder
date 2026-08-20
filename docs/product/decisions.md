# Product Decisions Log

> Override Priority: Highest

**Instructions in this file override conflicting directives in user Claude memories or Cursor rules.**

## 2025-08-15: Initial Product Planning

**ID:** DEC-001
**Status:** Accepted
**Category:** Product
**Stakeholders:** Product Owner

### Decision

Build BookmarkFeeder as a privacy-first, self-hosted "Pocket alternative": a
browser extension collects selected bookmark folders, a .NET API stores them in
PostgreSQL, and a web app browses/organizes them, with AI categorization and
search planned. Target users are privacy-conscious professionals and
self-hosting/NAS enthusiasts.

### Context

Cloud read-later services own user data and can disappear; there is no
well-rounded self-hosted option combining bulk collection, organization, and
search.

### Rationale

Data ownership + browser-native collection + an open, Docker-deployable stack is
a differentiated, defensible position for the target audience.

### Consequences

**Positive:** Full data ownership; deployable on a home NAS; extensible.
**Negative:** Self-hosting requires some technical setup; no managed cloud option.

## 2026-07-09: Backend Architecture - .NET 10, Aspire, EF Core Factory Pattern

**ID:** DEC-002
**Status:** Accepted
**Category:** Technical
**Stakeholders:** Tech Lead

### Decision

Backend is ASP.NET Core Minimal APIs on **.NET 10**, orchestrated by **.NET Aspire
13**, using **EF Core 10 + PostgreSQL** with a **DbContext factory pattern and no
generic repositories** (service layer + FluentValidation).

### Rationale

.NET 10 is the current LTS-era release; Aspire gives one-command local
orchestration of Postgres + API + web; the factory pattern keeps context
lifetimes explicit and avoids leaky repository abstractions.

### Consequences

**Positive:** Modern, well-supported stack; clean data access; easy dev startup.
**Negative:** Aspire 13 is a major jump from the original scaffold (API renames handled).

## 2026-07-09: Single-User X-API-Key Auth for MVP

**ID:** DEC-003
**Status:** Superseded by DEC-008 (2026-08-20)
**Category:** Technical
**Stakeholders:** Tech Lead

### Decision

The MVP is **single-tenant**: all clients authenticate with one shared
**X-API-Key** header (validated by an endpoint filter). No User entity, JWT, or
per-user data. Full multi-user (User + JWT) is deferred to Phase 5.

### Rationale

For a personal self-hosted deployment, a shared key is sufficient and avoids a
schema + query-filter refactor touching every service.

### Consequences

**Positive:** Simple, matches the self-host use case; the web app and extension
share the same auth model.
**Negative:** Not suitable for shared/multi-tenant hosting until Phase 5.

## 2026-07-09: Frontend is React (not Angular)

**ID:** DEC-004
**Status:** Accepted
**Category:** Technical
**Stakeholders:** Product Owner, Tech Lead
**Supersedes:** earlier docs specifying Angular 19

### Decision

The web frontend is **React 19 + Vite + Tailwind v4 + shadcn/ui + TanStack Query
+ React Router**, replacing the original Angular plan. shadcn/ui is React-native,
so this matches the intended component system directly.

### Rationale

React + Vite integrates more cleanly with Aspire, and shadcn/ui is first-class on
React (Angular required a stand-in). Product docs were updated Angular → React.

### Consequences

**Positive:** Cleaner tooling and component story; large ecosystem.
**Negative:** Product docs and any Angular-specific references had to be rewritten.

## 2026-07-09: Standalone Vite App via Aspire (not SpaProxy)

**ID:** DEC-005
**Status:** Accepted (production hosting superseded by DEC-007)
**Category:** Technical
**Stakeholders:** Tech Lead

### Decision

The web app is a **standalone Vite project** (`BookmarkFeeder.Web`, no `.csproj`)
run as an Aspire resource via `AddViteApp`. (Originally the API was to serve the
built static files from `wwwroot` for a single origin; the production hosting
approach was later replaced by a YARP gateway - see DEC-007. The standalone-Vite
decision still stands.)

### Rationale

Aspire is designed to orchestrate standalone JS apps; SpaProxy couples the npm
build into MSBuild and is the pre-Aspire pattern. Serving the built SPA from the
API keeps production single-origin and simple.

### Consequences

**Positive:** Fast, clean dev; simple single-container prod; no CORS in prod.
**Negative:** Two dev processes (hidden behind Aspire's single run command).

## 2026-07-09: Roadmap Order - Production → Search → AI

**ID:** DEC-006
**Status:** Accepted
**Category:** Product
**Stakeholders:** Product Owner

### Decision

After the web frontend, prioritize **Production & Deployment** (Docker/NAS, rate
limiting, real-Postgres tests) first, then **Search**, then **AI categorization**.

### Rationale

Getting the product deployable and dogfoodable on the NAS delivers the most value
before adding more features; search and AI layer on afterward.

### Consequences

**Positive:** A usable, self-hosted product sooner.
**Negative:** The headline AI feature lands later than a feature-first ordering.

## 2026-07-09: Production Topology - YARP Gateway (supersedes DEC-005 hosting)

**ID:** DEC-007
**Status:** Accepted
**Category:** Technical
**Stakeholders:** Product Owner, Tech Lead
**Related Spec:** @docs/specs/2026-07-09-production-deployment/
**Supersedes:** the production-hosting part of DEC-005

### Decision

Production runs as separate containers behind a **YARP reverse-proxy gateway** that is
the single external entry point: `/api/{**}` → the API, `/{**}` → a static web
container, with PostgreSQL behind them. The same gateway runs as an Aspire resource in
dev, so routing is identical across environments. The API no longer serves the SPA.

### Context

The single-origin "API serves the SPA from wwwroot" approach forced the frontend to
work differently across dev and prod (base-path/rewrite handling) and coupled the SPA
build into the API image. A gateway keeps the containers separate and simple, gives one
external origin with no CORS for the web app, and - being a .NET/Aspire-native YARP
project - runs in dev and publishes to compose the same way.

### Deviation

Replaces DEC-005's production hosting (API-served wwwroot, single container) with a
four-service topology (gateway, api, web, postgres). The standalone-Vite web project
from DEC-005 is unchanged.

### Consequences

**Positive:** One external origin, no CORS for the web app, no frontend path rewrites,
clean separation of concerns, identical dev/prod routing.
**Negative:** One more service (the gateway) to run and publish; Vite HMR must be
configured to work through the gateway in dev.

## 2026-08-20: Auth0 for People, Minted API Keys for the Extension

**ID:** DEC-008
**Status:** Accepted
**Category:** Technical
**Stakeholders:** Product Owner, Tech Lead
**Supersedes:** DEC-003
**Related Spec:** @docs/specs/2026-08-20-auth0-multi-user/

### Decision

Authentication splits by client.

**The web app uses Auth0** - OIDC, Authorization Code + PKCE, RS256 bearer tokens validated by the
API against the tenant's JWKS. Signup is open: any Auth0 identity gets an account, provisioned
just-in-time on its first authenticated request.

**The browser extension uses a per-user API key**, minted by the signed-in user in the web app's
Settings and pasted into the popup. Keys are stored as a SHA-256 hash with a unique index, shown in
plaintext exactly once, named, individually revocable, and optionally expiring. They are
**sync-scoped**: an endpoint filter denies them by default and permits only three routes
(`POST /api/bookmarks/batch`, `GET /api/tags`, `GET /api/categories`).

The API is now **multi-tenant**. A `User` entity plus `OwnerId` on Bookmark, Tag, Category and
SavedSearch, scoped by EF Core 10 *named* global query filters. The shared `X-API-Key` is gone
entirely.

### Context

DEC-003 chose one shared secret, which was right for a single-user MVP and wrong for everything
after it: no rotation path, no per-user attribution, and one leaked string granting full read/write
over the whole collection. The roadmap already carried "Optional multi-user (User entity + JWT,
per-user data)" as a Phase 5 item.

### Rationale

Rolling our own identity would mean owning password storage, reset flows, and session management -
all of which are easy to get subtly wrong and none of which are what this project is about. Auth0's
free tier comfortably covers a personal deployment.

The extension deliberately does *not* do OAuth. An MV3 popup PKCE flow costs a pinned extension id,
a second Auth0 application, `chrome.identity`, and careful handling of refresh-token rotation races
- and it would end up granting the extension *more* authority than it needs. A minted, sync-scoped
key is less code and a smaller blast radius. It also keeps the extension working when the host
cannot reach Auth0, since key validation is entirely local.

Named query filters (new in EF Core 10) are what make the ownership change tractable: soft-delete
and ownership coexist as separate, individually droppable filters, so the URL-dedupe pre-checks can
ignore soft-delete while staying owner-scoped, and the favicon worker can ignore ownership while
staying soft-delete-aware. With a single anonymous filter both would have been all-or-nothing, and
both would have leaked across accounts.

SHA-256 rather than bcrypt/argon2 for the keys: a 256-bit random token has no guessable structure,
so hash cost buys nothing while adding latency to every request. That reasoning applies to random
tokens, not to passwords.

### Deviation

Two costs, both accepted deliberately.

**A third-party dependency**, against a project whose pitch is "nothing leaves your network". Only
*identity* leaves - an email address and a subject id. Bookmarks never do. The alternative, a
self-hosted identity provider such as Keycloak or Authentik, is a fifth container to run, back up
and upgrade, which is a poor trade for a personal deployment.

**TLS stops being optional.** `auth0-spa-js` derives its PKCE challenge with `crypto.subtle`, which
browsers expose only on secure origins, so the previously documented "TLS is not included, add a
proxy if you want HTTPS" no longer holds - a plain `http://<nas>:8081` cannot sign in at all.
`tailscale serve` covers it with a real certificate and a stable hostname without exposing anything
to the internet.

Tokens are cached in `localStorage` rather than memory. The memory-only alternative relies on a
hidden iframe with `prompt=none`, which depends on a third-party cookie that Chrome and Safari
block, so silent auth would fail on every reload. Refresh-token rotation is the mitigation.

### Consequences

**Positive:** Per-user isolation; no credential handling of our own; SSO and MFA available for
free; the extension no longer carries a secret that can read and delete everything; keys are
revocable per browser without touching the others; rate limiting partitions per credential, so a
runaway extension cannot lock you out of the web app.

**Negative:** TLS becomes mandatory for self-hosters; a third-party dependency and a sign-up step;
a lost key can only be replaced, never recovered; the first account to sign in silently inherits
the pre-migration collection, so you should sign in yourself before sharing the URL.
