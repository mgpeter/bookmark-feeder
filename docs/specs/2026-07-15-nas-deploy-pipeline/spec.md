# Spec Requirements Document

> Spec: NAS Deploy Pipeline
> Created: 2026-07-15
> Status: Completed
> Completed: 2026-07-15

## Overview

Make BookmarkFeeder actually run on the Synology NAS, deployed by a repeatable flow driven from the
Aspire AppHost: `aspire publish` emits the compose artifact, the three service images are built for
linux-amd64 and pushed to Docker Hub, and the NAS pulls and runs them.

## User Stories

### Run my own bookmarks on my own box

As a self-hoster, I want BookmarkFeeder running on my NAS rather than on my dev machine, so that my
collection is available to every device on my network without a laptop being switched on.

I open `http://<nas>:8081` from any machine on the LAN and the dashboard loads, served through the
gateway, with `/api` behind it. My bookmarks survive a NAS reboot and a `docker compose down`.

### Ship a change without remembering how

As the only maintainer, I want redeploying to be a short documented sequence I can follow months
later, so that shipping a fix doesn't mean re-deriving how the deployment works.

I build and push the images, copy the regenerated compose to the NAS, and bring it up. Secrets stay
on the NAS and are never re-entered; only the image tags change between releases.

## Spec Scope

1. **Pinned production versions** - Pin the Postgres image the AppHost emits (currently unpinned, so
   production floats on Aspire's default and drifts from the tested version) and the Aspire dashboard
   image (currently `:latest` nightly).
2. **AppHost-driven compose** - Configure the Docker Compose environment so `aspire publish` emits a
   NAS-ready artifact: Docker Hub image names, a fixed gateway host port, restart policies, and
   Postgres-healthcheck-gated startup.
3. **Image build + push** - Build `webapi`/`gateway` via .NET SDK container publish and `web` via its
   existing Dockerfile, all linux-amd64, tagged by git sha and pushed to Docker Hub.
4. **Repeatable secrets** - Give `api-key` and `postgres-password` stable, explicitly-supplied values
   so the NAS `.env` is filled once and survives every subsequent publish.
5. **Deploy runbook + doc truth** - Rewrite `docs/deployment.md` to the flow that actually works, and
   correct the roadmap and tech-stack entries that no longer match reality.

## Out of Scope

- TLS/HTTPS at the gateway - the NAS's own reverse proxy can terminate it. Already deferred by the
  production-deployment spec.
- Postgres backups, CI/CD, secret-manager integration, multi-user. All previously deferred.
- Re-architecting the four-service topology (locked by DEC-007) or the single-shared-API-key model
  (DEC-003).
- `aspire deploy` as the deployment command - it runs `compose up` against the *local* Docker daemon,
  not a remote NAS.

## Expected Deliverable

1. From a browser on another machine on the LAN, `http://<nas>:8081` loads the React app through the
   gateway, `/api` is routed behind it, and the browser extension can sync to `http://<nas>:8081/api`.
2. A fresh `docker compose up -d` on the NAS applies EF migrations automatically, and the data
   survives `docker compose down && docker compose up -d`.
3. Redeploying after a code change is the documented sequence, with the NAS `.env` untouched except
   for image tags.
