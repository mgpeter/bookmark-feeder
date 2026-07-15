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

The **API key** is entered in Settings and kept in the browser's `localStorage`; a `401` sends you
back there. It is never baked into the bundle: `.env.development` sets `VITE_API_KEY` for local
convenience only, and Vite ignores that file in a production build - the shipped image contains no
key. Worth preserving, since the image is public.

## Layout

```
src/
  main.tsx      entry
  routes.tsx    route tree
  api/          TanStack Query hooks - one file per resource
  components/   shared UI + shadcn primitives in components/ui
  config/       runtime config
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
