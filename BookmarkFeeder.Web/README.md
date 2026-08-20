# BookmarkFeeder Web

The React web app: browse, search, organise and edit your collection.

A standalone Vite app - no `.csproj`, not in the solution. Aspire runs it as a resource
(`AddViteApp`) in development and builds it into a static nginx image for production.

## Run it

Normally you don't run this directly - start the whole stack instead, so the API and gateway come up
with it:

```bash
dotnet run --project BookmarkFeeder.AppHost     # from the repo root
```

The app is then at <http://localhost:5180>, through the gateway.

Standalone (needs the API running separately - `vite.config.ts` proxies `/api` to it, defaulting to
`https://localhost:7042` when Aspire hasn't injected a service-discovery URL):

```bash
npm install
npm run dev
```

| | |
|---|---|
| `npm run dev` | Vite dev server |
| `npm run build` | `tsc -b && vite build` → `dist/` |
| `npm test` | Vitest + Testing Library |
| `npm run lint` | oxlint |

## How it talks to the API

**Same-origin, always.** Requests go to a relative `/api/…`, which the YARP gateway routes to
the API - in dev and in production alike. There is no API base URL to configure, and no CORS.

**Authentication is Auth0**, Authorization Code + PKCE. `@auth0/auth0-react` holds the token;
`src/lib/api-client.ts` attaches it as `Authorization: Bearer` through a token getter registered by
`src/auth/api-token-bridge.tsx`. That indirection keeps the api-client React-free, because the query
functions in `src/api` import it and must stay mountable-free. A `401` restarts the login rather
than routing anywhere.

## Runtime configuration

The Auth0 domain, client id and audience are read at startup from
`window.__BOOKMARKFEEDER_CONFIG__`, set by `public/config.js`, with `import.meta.env` as the
development fallback. See `src/config/runtime-config.ts`.

This indirection exists because **the image is built once and shared**. `Dockerfile` runs
`npm run build` inside the image, so anything from `import.meta.env` is frozen at build time - and
every self-hoster pulling `mgpeter/bookmarkfeeder-web` has a different Auth0 tenant. At container
start, `docker-entrypoint.d/40-runtime-config.sh` rewrites `config.js` from environment variables,
and `nginx.conf` serves it `no-store` so a tenant change is picked up immediately.

It is a classic `<script>` in `index.html`, deliberately not a module: classic scripts run before
deferred module scripts, so the global exists by the time the bundle evaluates. `Auth0Provider`
needs the domain and client id at *first render*, which is also why these values are not fetched.

None of it is secret. An OAuth public client id, a tenant domain and an API audience are published
in every SPA bundle by design.

Running `npm run dev` standalone needs your own `.env.development` values; under the Aspire AppHost
they arrive as environment variables instead.

## Layout

```
src/
  main.tsx      entry
  routes.tsx    route tree
  api/          TanStack Query hooks - one file per resource
  components/   shared UI + shadcn primitives in components/ui
  auth/         Auth0 provider, route guard, token bridge, splash/error screens
  config/       runtime config resolution (config.js + import.meta.env)
  features/     bookmarks, tags, categories, dashboard, settings
  layout/       app shell, nav
  lib/          api-client, highlight, formatting, filters
  test/         setup + helpers
  types/        DTOs mirroring the API
```

Stack: React 19, Vite 8, TypeScript, Tailwind v4, shadcn/ui (`radix-nova`, Geist), TanStack Query v5,
React Router v7, react-hook-form + zod. The browser extension deliberately shares the same theme, so
the popup and the app look like one product.

## License

[MIT](../LICENSE), same as the rest of the repo.
