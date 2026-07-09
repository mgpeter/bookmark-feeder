---
description: Frontend React UI Rules
globs: BookmarkFeeder.Web/**/*.{ts,tsx,css}
alwaysApply: false
---
# React UI Development Rules

Rules for maintaining consistent React development in `BookmarkFeeder.Web` (React 19 + Vite).

## Components

1. **Functional components + hooks**
   - Use function components with hooks only; no class components.
   - Keep components focused and single-responsibility.
   - Group related components in feature folders under `src/features`.

2. **TypeScript**
   - `strict` mode is on. Type all props, hooks, and API models.
   - Prefer `type`/`interface` over `any`; share domain types from `src/types`.

3. **Build tooling**
   - Vite is the dev server and bundler (`@vitejs/plugin-react`).
   - Reference env via `import.meta.env`; never hardcode secrets or URLs.

## Styling

1. **Tailwind CSS v4** (`@tailwindcss/vite`)
   - Utility-first; avoid custom CSS unless a utility does not exist.
   - Support dark mode with `dark:` variants.
   - Use CSS custom properties for theme values.

2. **shadcn/ui**
   - Add primitives with `npx shadcn@latest add <component>`; they live in `src/components/ui`.
   - Compose feature UI from `ui` primitives rather than restyling from scratch.
   - Accessibility comes from Radix under shadcn/ui — keep the ARIA/behavior contracts intact.

## Data / Server State

1. **TanStack Query v5** owns all server state.
   - Fetch, cache, and mutate through `useQuery`/`useMutation` hooks in `src/api`.
   - Do NOT fetch in `useEffect`.
   - Invalidate the relevant query keys after mutations to keep caches fresh.

2. **api-client**
   - All requests go through the `src/lib` `api-client` fetch wrapper.
   - The wrapper injects the shared `X-API-Key` header and the API base URL.
   - Never hardcode the API key or secrets in components.

## Routing

- **React Router v7** for routing; route definitions live in `src/routes.tsx`.
- Drive list filters/sorting/pagination from URL search params (shareable, back-button friendly).

## Forms

- **react-hook-form + zod** for all forms.
- Define a zod schema per form and validate with the resolver; surface field-level errors.

## Structure

```
BookmarkFeeder.Web/src/
├── lib/          # api-client fetch wrapper, helpers
├── config/       # app/runtime configuration
├── api/          # TanStack Query hooks
├── types/        # shared TypeScript types
├── components/
│   ├── ui/       # shadcn/ui components
│   └── ...       # shared components
├── layout/       # page layouts
├── features/     # dashboard, bookmarks, tags, categories, settings
└── routes.tsx
```

## Accessibility

- Prefer Radix-based shadcn/ui primitives for accessible behavior.
- Provide ARIA labels, keyboard navigation, and proper heading hierarchy.

## Testing

- Use **Vitest** + **React Testing Library**.
- Test behavior via user-facing queries; mock the `api-client`/network layer.
- Cover both success and error states.
