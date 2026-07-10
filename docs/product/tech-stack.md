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
browser_extension: Chrome/Edge Manifest V3, vanilla JavaScript + Tailwind
ai_integration: LLM categorization (provider TBD) — planned
application_hosting: Self-hosted Docker (target: Synology NAS)
web_frontend_hosting: Standalone Vite app (Aspire AddViteApp in dev); static nginx container in prod
reverse_proxy_gateway: YARP (.NET) — single external origin routing /api → api and / → web (dev + prod)
database_hosting: Self-hosted PostgreSQL (Docker container)
asset_hosting: Served by the API (same origin/container)
deployment_solution: Docker Compose
code_repository_url: git@github.com:mgpeter/bookmark-feeder.git
