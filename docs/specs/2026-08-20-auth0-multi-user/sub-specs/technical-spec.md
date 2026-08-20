# Technical Spec: Auth0 Multi-User

## Two authentication schemes, chosen per request

`AuthenticationExtensions.AddBookmarkFeederAuthentication` registers a policy scheme that forwards
to the API-key handler when `X-API-Key` is present, and to JwtBearer otherwise. Both produce a
principal; `CurrentUserEndpointFilter` then resolves it to a `User.Id` and publishes it on the
scoped `ICurrentUserContext`.

A key principal already carries its owner (`bmf:owner`), so it needs no provisioning lookup - a key
can only exist for someone who has already signed in.

### JwtBearer settings that are wrong by default

| Setting | Default | Why it must change |
|---|---|---|
| `Authority` with an empty domain | builds `https:///` | Not a valid `Uri`; throws inside `JwtBearerPostConfigureOptions` and breaks `dotnet ef migrations add` and the build-time `GetDocument` OpenAPI generation, both of which run with no tenant configured |
| `MapInboundClaims` | `true` | Renames `sub` to `ClaimTypes.NameIdentifier`, breaking the rate-limit partition key and user provisioning at once |
| `ClockSkew` | 5 minutes | Far more slack than a local clock needs, and makes expiry behaviour hard to test |
| `OnChallenge` | empty body | The API-key filter it replaces returned ProblemDetails, and the SPA parses error bodies |

### Pipeline order

```
UseForwardedHeaders -> UseExceptionHandler -> [dev: OpenAPI/Scalar] -> [opt: UseHttpsRedirection]
-> UseCors -> UseAuthentication -> UseRateLimiter -> UseAuthorization
```

`UseCors` first, because a preflight `OPTIONS` carries no credentials and must be answered rather
than challenged. `UseRateLimiter` sits **between** authentication and authorization: after
authentication so the partition key can see the caller, and before authorization because
`AuthorizationMiddleware` short-circuits - with the limiter last, an unauthenticated caller could
hammer token validation and key lookups entirely unthrottled, which is worse than the model this
replaces.

## Ownership via NAMED query filters

EF Core 10 allows several named filters per entity and `IgnoreQueryFilters(["name"])` to drop one
selectively. That is load-bearing here, not a stylistic choice:

| Call site | Drops | Keeps | Why |
|---|---|---|---|
| `BookmarkService.CreateAsync` dedupe | `SoftDelete` | `Owner` | The unique index still covers soft-deleted rows. Dropping `Owner` too would report a duplicate because *somebody else* saved that URL, leaking that they did |
| `BookmarkService.CreateBatchAsync` dedupe | `SoftDelete` | `Owner` | The worst case: a new user's first sync would skip almost everything as "already present" and silently import nothing |
| `CategoryService.DeleteAsync` reassign | `SoftDelete` | `Owner` | Would otherwise touch other owners' bookmarks |
| `FaviconBackgroundService` (both queries) | `Owner` | `SoftDelete` | The worker runs on its own scope with no HTTP request, so there is no current user and the owner filter matches nothing - enrichment would die silently for everyone |
| `SeedData` idempotence guard | `Owner` | - | Runs in the startup scope. With the filter active the guard is always false, the seeder re-runs, and the second `dotnet run` dies on the unique index |

A single anonymous filter joining both conditions would have made every one of these
all-or-nothing.

### Getting the owner into the DbContext

`BookmarkDbContext` takes `ICurrentUserContext` as a constructor parameter and snapshots
`_ownerId`. EF resolves extra constructor parameters from the provider the **factory** was resolved
from, so `AddDbContextFactory` must be registered with `ServiceLifetime.Scoped` - the default
singleton factory holds the root provider and cannot resolve a scoped service.

⚠️ **`AddPooledDbContextFactory` must never be used here.** Pooled instances are reused across
scopes and would carry another user's `_ownerId` - a cross-tenant read. There is a comment at the
registration site saying so.

`Guid.Empty` is the unresolved default and matches no rows: **fail closed**, never "see everything".

### Writes

Query filters only read. `BookmarkDbContext.SaveChanges(Async)` stamps `OwnerId` on any `Added`
entity implementing `IOwnedEntity`, so no create path has to remember it and a forgotten one cannot
silently produce an unreachable row. This removed the need to touch all 21 service methods.

### Indexes

Two global unique indexes became per-owner (`(OwnerId, Url)`, `(OwnerId, NormalizedName)`) and the
filter/sort indexes gained `OwnerId` as the **leading** column, since every list query now starts
with a predicate on it.

The GIN index on `SearchVector` stays single-column. Prefixing it with `OwnerId` needs the
`btree_gin` extension and raw SQL EF cannot represent in the model snapshot; at personal-deployment
scale the planner GIN-scans then filters by owner. Revisit only with many users *and* large
collections.

## API keys

`bmf_` plus base64url of 32 random bytes. The first 8 characters of the secret are stored in clear
as `Prefix` (indexed, and the display identity in the UI); `KeyHash` is SHA-256 hex with a **unique
index**, which is what makes a key globally unique and therefore attributable to exactly one owner.

Validation narrows by prefix, then compares with `CryptographicOperations.FixedTimeEquals`. Unknown,
revoked and expired keys are all reported identically, so a 401 never confirms that a key existed.
`LastUsedAt` is throttled to one write per hour, or every sync would cost a database write.

**SHA-256, not bcrypt/argon2.** A 256-bit random token has no guessable structure, so an attacker
cannot do better than brute force regardless of hash cost, while a slow KDF would add latency to
every request. That reasoning applies to random tokens, not to passwords - there is a comment saying
so, because this is exactly the kind of thing that gets "fixed" later.

### Scope enforcement is fail-closed

`ApiKeyScopeFilter` denies a key-authenticated principal with 403 unless the endpoint carries
`AllowApiKeyAttribute` metadata. The allow-list is `POST /api/bookmarks/batch`, `GET /api/tags` and
`GET /api/categories`. An endpoint added later is closed automatically; `ApiKeyScopeTests` asserts
this across the whole route table rather than trusting anyone to remember.

Note that `/api/keys` needs no rule of its own - a key cannot mint keys purely because of the
default deny, which is the privilege-escalation case worth calling out.

## Rate limiting

Partitioned per *credential*, not per user: `key:{keyId}`, `user:{sub}`, or `ip:{address}`. A
runaway extension therefore cannot exhaust the bucket the web app is using. The `sub` is read from
the **validated** principal, never from a raw header - otherwise anyone could forge a partition key
and evict a real user's bucket.

## Runtime configuration for the SPA

`Dockerfile` runs `npm run build` inside the image, so `import.meta.env` is frozen at build time,
and one published image serves every self-hoster's tenant. `Auth0Provider` needs the domain and
client id at *first render*, so fetching them would gate the whole tree behind an async boot, add a
flash, race auth0-react's consumption of the redirect query parameters, and make the login screen
unreachable whenever the API is down.

Instead `docker-entrypoint.d/40-runtime-config.sh` rewrites `config.js` at container start from
environment variables; `index.html` loads it as a **classic** script before the module script (so
it evaluates first); `nginx.conf` serves it `no-store` so a tenant change is not pinned by a cache.

> **Successor to the "Public-image safety" note in the nas-deploy-pipeline spec.** That audit's
> argument was that no `VITE_` value is ever baked into the public image. That no longer holds, and
> is now correct: the Auth0 domain, client id and audience **are** shipped in the image, because an
> OAuth public client id is public by design. What must never appear is a `POSTGRES_PASSWORD` or an
> API key, and the build-time grep is still worth re-running after this change.

## The migration

`AddUsersOwnershipAndApiKeys`, hand-ordered after scaffolding - the scaffolder emits
`AddColumn(nullable: false)` with an empty-Guid default and wires the foreign keys immediately,
which fails on any non-empty database.

1. Create `Users` and `ApiKeys` with their indexes.
2. Add **nullable** `OwnerId` to the four tables. Metadata-only on PostgreSQL 11+ because there is
   no default, so no table rewrite and no recompute of the stored `SearchVector` column.
3. Insert the fixed-Guid placeholder user (`...f01d`) with a NULL `Auth0Sub`.
4. Backfill.
5. Set NOT NULL.
6. Swap the unique indexes for their per-owner replacements.
7. Rebuild the filter/sort indexes with `OwnerId` leading.
8. Add the foreign keys last.

Verified with `dotnet ef migrations script`; the whole thing runs in one transaction and does not
touch `SearchVector`.

## Testing

`TestTokens` signs HS256 with a known symmetric key, and both factories override `JwtBearerOptions`
with a null `Authority`, `MetadataAddress` and `ConfigurationManager` plus an explicit
`IssuerSigningKey`. That keeps the **real** handler, `RequireAuthorization`, the 401 path and
just-in-time provisioning in the pipeline while never touching the network.

Tests that need `ExecuteUpdate`/`ExecuteDelete` or the real unique indexes live in the Postgres
suite - EF InMemory supports neither, which is why the mark-all-read isolation test is in
`UserIsolationPostgresTests` rather than alongside its siblings.

## ⚠️ Verify

- `router.navigate` inside `onRedirectCallback` assumes `RouterProvider` has mounted by the time
  auth0-react fires it. If a deep link ever lands on `/` after login, stash `returnTo` in a module
  variable for `RequireAuth` to consume instead.
- `Authorization` header forwarding through YARP is untested in production - `X-API-Key` is the
  header this deployment has actually proven. Curl through the gateway before trusting it; the fix,
  if needed, is a `RequestHeader` transform on the `api` route.
- JWKS egress from the container becomes a hard runtime dependency for the web path. The extension
  is unaffected, since key validation is entirely local.
