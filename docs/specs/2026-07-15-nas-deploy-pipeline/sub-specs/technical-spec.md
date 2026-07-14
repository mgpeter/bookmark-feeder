# Technical Specification

This is the technical specification for the spec detailed in @docs/specs/2026-07-15-nas-deploy-pipeline/spec.md

## How Aspire's compose publishing actually behaves

Established by decompiling `Aspire.Hosting.Docker` 13.4.6 — not from documentation, which is what
produced the current mess. **Verify each of these by running the command at execution time.**

- **`aspire publish` emits a key manifest, deliberately.** `PublishAsync` builds the `.env` with
  `envFile.Add(key, null, description)` and calls `Save(includeValues: false)` → writes `KEY=`.
  The blank `publish/.env` is therefore *by design*, not a bug. It is not runnable as-is.
- **`aspire deploy` is a different pipeline.** Steps: `publish-compose` → `RequiredBy("publish")`;
  `prepare-compose` → `DependsOn("publish")` **and** `DependsOn("build")`; `docker-compose-up-compose`
  → `RequiredBy("deploy")`. So image **build hangs off `deploy`, never off `publish`**. `PrepareAsync`
  resolves real values (including secrets) into **`.env.{EnvironmentName}`** — a *different file* from
  `.env`.
- **`aspire deploy` is not our deploy command.** `DockerComposeUpAsync` logs *"is now running with
  Docker Compose **locally**"* — it would start the whole stack on the dev box, not the NAS.
- **`EnvFile.Add(key, value, comment, onlyIfMissing: true)`** and publish loads the existing file
  first. **Re-running `aspire publish` preserves values already present in `.env`.** This is the
  mechanism that makes the flow repeatable: fill the NAS `.env` once; republishing only adds new keys.
- **Registry API lives in `Aspire.Hosting` core**, not the Docker package: `AddContainerRegistry`,
  `WithContainerRegistry`, `WithImagePushOptions`, `WithContainerBuildOptions`
  (`ContainerTargetPlatform.LinuxAmd64`). `GetContainerRegistry` falls back to
  `LocalContainerRegistry.Instance` (endpoint `""` → bare `webapi:latest`) when none is configured —
  which is what happens today.
- ⚠️ **Verify:** `WithImagePushOptions` is marked `[Experimental]` and may require
  `<NoWarn>$(NoWarn);ASPIREPIPELINES003</NoWarn>`; the exact `.env.{EnvironmentName}` filename; and
  whether publish warns on an unresolvable `ContainerImageReference`.

## 1. Pin what production runs

- `AddPostgres("postgres")` is unpinned → compose emits Aspire's default (`postgres:18.3` today).
  The test suite runs `postgres:17-alpine` (`PostgresApiFactory`). **Production runs a major version
  nothing has tested, and an Aspire bump could change it silently.** Pin explicitly with
  `.WithImageTag(...)` and make prod and tests agree on one version.
- Pin the dashboard off `mcr.microsoft.com/dotnet/nightly/aspire-dashboard:latest` to a fixed,
  non-nightly tag, and give it a fixed host port (it currently publishes `18888` to a *random* host
  port). The dashboard is kept by choice; it is a second exposed port with an unauthenticated UI, so
  it should be reachable only on the LAN.

## 2. AppHost configuration (`BookmarkFeeder.AppHost/Program.cs`)

The only code file this spec changes. Shape:

- `builder.AddContainerRegistry("dockerhub", "docker.io", "mgpeter")` +
  `.WithContainerRegistry(registry)` on the compose environment.
- Per service: `.WithContainerBuildOptions(ctx => ctx.TargetPlatform = ContainerTargetPlatform.LinuxAmd64)`
  and `.WithImagePushOptions(...)` naming `mgpeter/bookmarkfeeder-{webapi,gateway,web}`.
- `PublishAsDockerComposeService<T>((r, svc) => ...)` to set `svc.Restart = "unless-stopped"`, a
  **fixed** `svc.Ports` entry (`"8080:8080"`, replacing the random-port `"${GATEWAY_PORT}"`), and
  `svc.DependsOn["postgres"] = new ServiceDependency { Condition = "service_healthy" }` for `webapi`
  (today it is `service_started`, so the API can start before Postgres is ready).
- `ConfigureComposeFile(...)` for the generated-only `compose-dashboard` and `postgres` services
  (pin dashboard image/port; add a `pg_isready` healthcheck so the `service_healthy` gate has
  something to wait on).
- Verified node shapes: `Service.Image`, `.Ports` (`List<string>`), `.DependsOn`
  (`Dictionary<string, ServiceDependency>`), `.Restart` (`string?`), `.Healthcheck`;
  `Healthcheck.Interval`/`.Timeout` are `required`.
- ⚠️ **Verify:** the gateway's container port is currently derived from `${GATEWAY_PORT}`; pinning
  `"8080:8080"` assumes `HTTP_PORTS` resolves to 8080. Keep `GATEWAY_PORT=8080` in `.env` so both
  halves agree, and read the emitted YAML to confirm.

## 3. Secrets

- **`AddPostgres` auto-generates a password.** Left alone, each publish yields a *different* value —
  the NAS volume would keep the first password while the next `.env` carries a new one, and the API
  would fail to connect. Pass an explicit `builder.AddParameter("postgres-password", secret: true)`
  so the value is stable and supplied, not generated.
- Dev-box values via user-secrets (the AppHost already has `UserSecretsId be72b26f-…`):
  `dotnet user-secrets --project BookmarkFeeder.AppHost set Parameters:api-key "…"`.
- **The NAS `.env` is the source of truth for the deployment and is filled once.** `onlyIfMissing`
  means republishing never overwrites it. Keep it `chmod 600`; `publish/` stays gitignored.

## 4. Images

Tag scheme: `mgpeter/bookmarkfeeder-{webapi,gateway,web}:<git-short-sha>` plus a moving `:latest`.
**Deploy by sha, never by `latest`** — otherwise "which build is on the NAS?" has no answer.

`webapi` and `gateway` are `Microsoft.NET.Sdk.Web` on `net10.0`, so no Dockerfile is needed:

```
dotnet publish BookmarkFeeder.WebApi/BookmarkFeeder.WebApi.csproj -c Release \
  --os linux --arch x64 /t:PublishContainer \
  -p:ContainerRegistry=docker.io -p:ContainerRepository=mgpeter/bookmarkfeeder-webapi \
  -p:ContainerImageTags='"<sha>;latest"'
```

`web` uses its existing Dockerfile (node build → static nginx):

```
docker buildx build --platform linux/amd64 \
  -t mgpeter/bookmarkfeeder-web:<sha> -t mgpeter/bookmarkfeeder-web:latest \
  --push BookmarkFeeder.Web
```

From an amd64 dev box `--os linux --arch x64` is a straight cross-target; the SDK pushes to the
registry directly, so no buildx/QEMU is needed for the two .NET services.

**Public-image safety (audited 2026-07-15):** safe. `BookmarkFeeder.Web/.env.development` holds
`VITE_API_KEY`, but Vite only loads `.env.development` in dev mode — `npm run build` runs production
mode and bakes nothing from it, and the final nginx stage copies only `/app/dist`. In production
`import.meta.env.VITE_API_KEY` resolves to `''` and the key is entered in Settings. The API key and
DB password reach `webapi` as runtime env, never baked. **Confirm by grepping the built bundle for
the key before the first public push** — the cost of being wrong is a world-readable secret.

## 5. The flow

**Dev box:** build+push 3 images → `aspire publish -o ./publish` (PowerShell; the CLI is a `.cmd`
shim Git Bash cannot invoke) → copy `publish/docker-compose.yaml` to the NAS → bump the three
`*_IMAGE` tags in the NAS `.env`.

**NAS (SSH):** `docker compose up -d`.

Only the compose YAML is copied; `.env` is NAS-resident and never overwritten. Stop the AppHost
before building — a running AppHost locks `BookmarkFeeder.WebApi.exe` and fails the build.

## External Dependencies

None. `AddContainerRegistry`/`WithImagePushOptions`/`WithContainerBuildOptions` are in the
already-referenced `Aspire.Hosting`; `Aspire.Hosting.Docker` 13.4.6 is already referenced.
