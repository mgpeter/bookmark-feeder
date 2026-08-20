# Auth0 Multi-User (lite)

Authentication splits by client. The web app signs in through Auth0 - OIDC, Authorization Code +
PKCE, RS256 bearer tokens validated against the tenant's JWKS - with open signup and just-in-time
user provisioning. The browser extension keeps its `X-API-Key` transport, but the key is now minted
per-user in the web app's Settings: SHA-256 hashed with a unique index (which is what makes keys
globally unique and attributable to exactly one owner), shown in plaintext once, named, individually
revocable, optionally expiring, and confined by a fail-closed endpoint filter to three sync routes.
The API becomes multi-tenant via a `User` entity and `OwnerId` on the four aggregates, scoped by EF
Core 10 *named* query filters so soft-delete and ownership can be dropped independently - which is
what lets the URL-dedupe pre-checks stay owner-scoped and the favicon worker stay cross-owner. The
shared key is removed entirely. TLS stops being optional, because `auth0-spa-js` needs
`crypto.subtle` and browsers expose it only on secure origins; `tailscale serve` supplies it.
