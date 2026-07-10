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

## 2026-07-09: Backend Architecture — .NET 10, Aspire, EF Core Factory Pattern

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
**Status:** Accepted
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
approach was later replaced by a YARP gateway — see DEC-007. The standalone-Vite
decision still stands.)

### Rationale

Aspire is designed to orchestrate standalone JS apps; SpaProxy couples the npm
build into MSBuild and is the pre-Aspire pattern. Serving the built SPA from the
API keeps production single-origin and simple.

### Consequences

**Positive:** Fast, clean dev; simple single-container prod; no CORS in prod.
**Negative:** Two dev processes (hidden behind Aspire's single run command).

## 2026-07-09: Roadmap Order — Production → Search → AI

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

## 2026-07-09: Production Topology — YARP Gateway (supersedes DEC-005 hosting)

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
external origin with no CORS for the web app, and — being a .NET/Aspire-native YARP
project — runs in dev and publishes to compose the same way.

### Deviation

Replaces DEC-005's production hosting (API-served wwwroot, single container) with a
four-service topology (gateway, api, web, postgres). The standalone-Vite web project
from DEC-005 is unchanged.

### Consequences

**Positive:** One external origin, no CORS for the web app, no frontend path rewrites,
clean separation of concerns, identical dev/prod routing.
**Negative:** One more service (the gateway) to run and publish; Vite HMR must be
configured to work through the gateway in dev.
