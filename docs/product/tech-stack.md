# Technical Stack

application_framework: ASP.NET Core Minimal APIs (.NET 10)
orchestration: .NET Aspire 13.4.6 (AppHost + ServiceDefaults)
orm: Entity Framework Core 10 (DbContext factory pattern, no generic repositories)
database_system: PostgreSQL (Npgsql provider 10)
api_documentation: OpenAPI (Microsoft.AspNetCore.OpenApi 10) + Scalar
api_authentication: Single shared X-API-Key header (endpoint filter)
input_validation: FluentValidation
backend_testing: xUnit + FluentAssertions + WebApplicationFactory
javascript_framework: React 19
build_tool: Vite 8
language: TypeScript
import_strategy: node
css_framework: Tailwind CSS v4 (@tailwindcss/vite)
ui_component_library: shadcn/ui (Radix primitives)
server_state: TanStack Query v5
routing: React Router v7
forms: react-hook-form + zod
frontend_testing: Vitest + React Testing Library
fonts_provider: Geist (self-hosted via @fontsource-variable)
icon_library: lucide-react
browser_extension: Chrome/Edge Manifest V3 — React 19 + Vite + shadcn/ui (@crxjs/vite-plugin)
ai_integration: LLM categorization (provider TBD) — planned
application_hosting: Self-hosted Docker Compose on a Synology NAS (deployed)
web_frontend_hosting: Standalone Vite app (Aspire AddViteApp in dev); static nginx container in prod
reverse_proxy_gateway: YARP (.NET) — single external origin routing /api → api and / → web (dev + prod)
database_hosting: Self-hosted PostgreSQL (Docker container)
asset_hosting: Static nginx container behind the gateway (same origin) — the API does not serve assets (DEC-007)
deployment_solution: Docker Compose, generated from the Aspire AppHost (docker/docker-compose.yaml, never hand-edited)
code_repository_url: git@github.com:mgpeter/bookmark-feeder.git
container_registry: Docker Hub — mgpeter/bookmarkfeeder-{webapi,gateway,web}, public, linux/amd64
image_versioning: semver in /VERSION; scripts/docker-release.* bumps, builds and pushes :<version> + :latest
database_storage: bind mount (POSTGRES_DATA_PATH, default ./data/postgres beside the compose file)
