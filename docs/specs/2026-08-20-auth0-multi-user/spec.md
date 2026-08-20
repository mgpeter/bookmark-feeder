# Spec Requirements Document

> Spec: Auth0 Multi-User
> Created: 2026-08-20
> Status: Completed
> Completed: 2026-08-20

## Overview

Replace the single shared `X-API-Key` with real accounts. The web app signs in through Auth0
(OIDC, Authorization Code + PKCE); the browser extension authenticates with a per-user API key that
the signed-in user mints in Settings. Every bookmark, tag, category and saved search gains an owner,
and no account can see another's.

## User Stories

### Share the box without sharing the bookmarks

As the person who runs the NAS, I want to give someone else the URL without giving them my reading
list, so that the box can be useful to the household rather than only to me.

They open the same URL, sign in with their own identity, and land on an empty dashboard. Nothing I
have saved is visible to them, and nothing they save appears for me - including in search results,
tag lists and category trees.

### Stop pasting one secret into every browser

As a self-hoster, I want to stop copying a single all-powerful string into every browser and
extension, so that losing one of them does not mean rotating the credential everywhere at once.

I sign in to the web app, create a key named after that browser, and paste it into the extension.
When I retire that laptop I revoke that one key, and every other browser keeps working.

### Limit what a browser extension can do

As a security-conscious user, I want the extension to hold the least authority that still lets it
sync, so that a key scraped from a browser profile cannot destroy my collection.

A key can upload bookmarks in bulk and read tags and categories. Anything else - delete, update,
mark-all-read, saved searches, minting more keys - is refused with a 403 before the endpoint runs.

## Spec Scope

1. **Auth0 bearer authentication** - Validate RS256 tokens against the tenant's JWKS, replacing the
   `ApiKeyEndpointFilter`. Open signup with just-in-time user provisioning.
2. **Per-user data isolation** - `User` entity plus `OwnerId` on Bookmark, Tag, Category and
   SavedSearch, enforced by EF Core 10 *named* global query filters, with per-owner unique indexes.
3. **Minted API keys for the extension** - Hashed, prefixed, named, individually revocable,
   optionally expiring, and confined to a fail-closed allow-list of sync endpoints.
4. **Runtime Auth0 configuration for the prebuilt web image** - The published image must work
   against any tenant without being rebuilt.
5. **TLS via `tailscale serve`** - Now a requirement rather than an option, and documented as such.

## Out of Scope

- Roles, permissions or RBAC beyond the two-tier "signed-in user" vs "sync-scoped key" split.
- Organisations, teams or sharing a collection between accounts.
- Self-hosted identity (Keycloak, Authentik) - a fifth container to run and back up.
- OAuth inside the browser extension. Explicitly rejected; see DEC-008.
- Extension store publication, which stays with the `extension-publishing` spec.

## Expected Deliverable

1. Two accounts created by open signup on `https://<machine>.<tailnet>.ts.net` see two independent
   collections, and neither can read, modify or detect the other's - including via full-text search.
2. A key minted in Settings syncs from the extension, and returns 403 on every endpoint outside the
   sync allow-list. Revoking it stops that browser and no other.
3. The first account to sign in inherits the pre-migration collection; the second starts empty.
