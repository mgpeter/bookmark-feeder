# Tasks: Auth0 Multi-User

> Spec: @docs/specs/2026-08-20-auth0-multi-user/spec.md
> Status: Completed (code + tests); deployment verification outstanding

- [x] **1. Authentication scheme and pipeline**
  - [x] 1.1 Add `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.9
  - [x] 1.2 `Auth0Options`, `AuthenticationExtensions`, `ApiKeyAuthenticationHandler`, policy scheme
  - [x] 1.3 Reorder the pipeline; swap the `/api` group to `RequireAuthorization()`
  - [x] 1.4 Delete `ApiKeyEndpointFilter`; repartition rate limiting per credential
  - [x] 1.5 Replace the fail-fast check; add the anonymous `GET /api/config`
  - [x] 1.6 Replace the OpenAPI/Scalar security schemes

- [x] **2. User, ownership and the migration**
  - [x] 2.1 `User` + `ApiKey` models and configurations; `ICurrentUserContext`; JIT provisioning
  - [x] 2.2 `OwnerId` on the four aggregates; named query filters; owner stamping in `SaveChanges`
  - [x] 2.3 Fix the three `IgnoreQueryFilters` cross-tenant leaks
  - [x] 2.4 Fix the favicon worker and the `SeedData` guard
  - [x] 2.5 Hand-order the migration; verify with `migrations script`

- [x] **3. API keys**
  - [x] 3.1 `ApiKeyService` (generate, hash, validate, revoke, throttled `LastUsedAt`)
  - [x] 3.2 `/api/keys` endpoints, DTOs and validator
  - [x] 3.3 `ApiKeyScopeFilter` + `AllowApiKey` on the three sync routes

- [x] **4. Tests**
  - [x] 4.1 `TestTokens`; both factories mint locally signed tokens
  - [x] 4.2 Repair the suites broken by the scoped-factory change
  - [x] 4.3 New: scope, key lifecycle, isolation, provisioning, favicon ownership
  - [x] 4.4 Green: 139 unit + 58 integration

- [x] **5. Web app**
  - [x] 5.1 Runtime config (`config.js`, nginx entrypoint, `no-store`, secure-origin check)
  - [x] 5.2 `Auth0Provider`, `RequireAuth`, `ApiTokenBridge`, splash/error/missing-config screens
  - [x] 5.3 Bearer token getter in `api-client`; never retry a 401
  - [x] 5.4 Settings: account panel + API keys card; sidebar user block
  - [x] 5.5 Tests updated and extended (59 passing)

- [x] **6. Extension**
  - [x] 6.1 Message copy, the link to Settings, and a 403 branch
  - [x] 6.2 Tests updated (35 passing)

- [x] **7. Config and docs**
  - [x] 7.1 AppHost parameters; regenerate compose; `--check` clean
  - [x] 7.2 `.env` templates
  - [x] 7.3 README, deployment guide, tech-stack, roadmap, DEC-008, rules, component READMEs
  - [x] 7.4 This spec

- [ ] **8. Deployment verification** - needs the user
  - [ ] 8.1 Create the Auth0 API and SPA application; enable the connection
  - [ ] 8.2 Fill the client id into AppHost user-secrets and run locally end to end
  - [ ] 8.3 `tailscale serve` on the NAS; sign in over the ts.net URL
  - [ ] 8.4 Mint a key, point the extension at the NAS, sync
  - [ ] 8.5 Confirm favicons still resolve, and that a second account sees an empty collection

## Notes

**The bar is a running deployment, not a green test suite.** Task 8 is the one that matters and it
cannot be done from here: it needs Auth0 dashboard access and the NAS. Everything above it is
verified by tests and a clean build.

**Three latent bugs were found while implementing, none of which the request was about.** All three
are the kind that pass a first smoke test:

1. `BookmarkService.CreateBatchAsync` used a bare `IgnoreQueryFilters()`. Once ownership became a
   query filter, a new user's first sync would have reported almost everything as "already present"
   and imported nothing - while also revealing which URLs other accounts held.
2. `SeedData`'s idempotence guard runs in the startup scope, where there is no current user, so the
   owner filter made it always false. The seeder would re-run and the **second** `dotnet run` would
   die on the unique index.
3. `FaviconBackgroundService` runs outside any request, so the owner filter would have matched
   nothing and favicon enrichment would have stopped silently, with no test noticing.

EF Core 10's *named* query filters are what made all three fixable without giving up ownership
scoping somewhere. `FaviconOwnershipTests` deliberately reproduces the unscoped worker environment
so removing the bypass fails a test that explains itself.

**One deviation from the plan.** The plan called for injecting `ICurrentUserContext` into the four
services to stamp `OwnerId` at each `Add`. Overriding `SaveChanges` in the DbContext turned out to
be strictly better: one mechanism instead of six edit sites, and a forgotten create path cannot
silently produce an unreachable row.

**Known gaps.** `Authorization` header forwarding through YARP has never been exercised in
production - `X-API-Key` is the header this deployment has proven - so it is worth a curl through
the gateway before trusting it. JWKS egress becomes a hard runtime dependency for the web path,
though not for the extension.
