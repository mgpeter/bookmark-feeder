using Aspire.Hosting.Docker.Resources.ComposeNodes;
using Aspire.Hosting.Docker.Resources.ServiceNodes;

var builder = DistributedApplication.CreateBuilder(args);

// Publishing target: `aspire publish` emits a Docker Compose project for this environment.
//
// Image names are NOT configured here. Aspire's registry/push/build-options APIs
// (AddContainerRegistry, WithImagePushOptions, WithContainerBuildOptions) are all gated behind
// ASPIRECOMPUTE003/ASPIREPIPELINES003 — "for evaluation purposes only and subject to change or
// removal" — and they would buy nothing: the emitted compose already references ${WEBAPI_IMAGE}
// etc., so the image refs live in the NAS .env, which is filled once and preserved on every
// republish (EnvFile.Add uses onlyIfMissing). The build commands pin linux/amd64 themselves.
// See docs/deployment.md.
//
// Publish only ever writes the .env KEYS (Save(includeValues: false)); values are never emitted.
builder.AddDockerComposeEnvironment("compose")
    .ConfigureComposeFile(file =>
    {
        // Without this, compose derives the project name from the DIRECTORY it runs in — "docker"
        // here, something else on the NAS. That name labels the stack and, worse, prefixes the
        // data volume (docker_bookmarkfeeder-postgres-data), so the database's identity would
        // depend on what the folder happens to be called. Naming it makes the stack and its
        // volume deterministic on every host.
        file.Name = "bookmark-feeder";

        // postgres and the dashboard are generated for us, so they are customised here rather
        // than through PublishAsDockerComposeService.
        if (file.Services.TryGetValue("postgres", out var postgresService))
        {
            postgresService.Restart = "unless-stopped";

            // Gives webapi's service_healthy condition something real to wait on: without a
            // healthcheck, "started" only means the container exists, not that it accepts
            // connections, and the API would race it on a cold NAS boot.
            postgresService.Healthcheck = new Healthcheck
            {
                Test = ["CMD-SHELL", "pg_isready -U postgres -d bookmarkfeeder"],
                Interval = "10s",
                Timeout = "5s",
                Retries = 5,
                // Required by the type. Generous: a NAS cold-starting several containers at once
                // is slower than a dev box, and a false "unhealthy" here blocks the API.
                StartPeriod = "30s",
            };
        }

        if (file.Services.TryGetValue("compose-dashboard", out var dashboardService))
        {
            dashboardService.Restart = "unless-stopped";

            // Aspire emits the NIGHTLY repo on a floating :latest — a preview channel that can
            // change under the NAS at any pull. Same version line, stable repo, pinned.
            // Telemetry only: if it ever misbehaves it cannot affect serving the app.
            dashboardService.Image = "mcr.microsoft.com/dotnet/aspire-dashboard:9.5.2";

            // "18888" alone publishes to a RANDOM host port; pin it so the URL is stable.
            // NOTE: this UI is unauthenticated — keep it on the LAN, never port-forward it.
            dashboardService.Ports.Clear();
            dashboardService.Ports.Add("18888:18888");
        }
    });

// Shared API key for the browser extension and web app -> WebApi.
var apiKey = builder.AddParameter("api-key", secret: true);

// Supplied explicitly rather than letting AddPostgres generate one: a generated password is a
// new value on every publish, so the NAS volume would keep the first password while a later .env
// carried a different one, and the API could no longer connect to its own database.
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

// Pinned explicitly: unpinned, this floats on whatever Aspire's default happens to be, so an
// Aspire upgrade could change the database's MAJOR version under a live data volume. 18.x is
// what the dev volume already holds (PG_VERSION = 18) and what the test suite runs.
//
// The volume is named explicitly too. Left to itself, WithDataVolume() derives a hashed name that
// differs between contexts (dev run emitted ...-c707ae991a-..., publish emitted ...-190750286b-...).
// On a NAS that is a silent data-loss trap: a shifted hash means compose creates a fresh empty
// volume and the collection appears to vanish. A fixed name makes the database's identity
// deterministic across every publish.
var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    .WithImageTag("18.3")
    .WithDataVolume("bookmarkfeeder-postgres-data")
    .AddDatabase("bookmarkfeeder");

var apiService = builder.AddProject<Projects.BookmarkFeeder_WebApi>("webapi")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WithEnvironment("Authentication__ApiKey", apiKey)
    .PublishAsDockerComposeService((_, service) =>
    {
        service.Restart = "unless-stopped";
        // Wait for Postgres to accept connections, not merely to exist.
        service.DependsOn["postgres"] = new ServiceDependency { Condition = "service_healthy" };
    });

// React (Vite) web frontend. Standalone npm app; Aspire runs `npm run dev` and injects PORT.
// Internal only — reached through the gateway, not exposed directly. On publish it is built
// from its Dockerfile (multi-stage: Vite build -> static nginx).
var web = builder.AddViteApp("web", "../BookmarkFeeder.Web")
    .PublishAsDockerFile()
    .PublishAsDockerComposeService((_, service) => service.Restart = "unless-stopped");

// YARP gateway: the single external entry point. Routes /api -> webapi and / -> web
// via service discovery (destinations "http://webapi" / "http://web").
builder.AddProject<Projects.BookmarkFeeder_Gateway>("gateway")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithReference(web)
    .WaitFor(web)
    .WithExternalHttpEndpoints()
    .PublishAsDockerComposeService((_, service) =>
    {
        service.Restart = "unless-stopped";

        // A bare "${GATEWAY_PORT}" publishes to a RANDOM host port, which is useless for a NAS
        // people bookmark. Both sides come from the same variable so one .env knob controls the
        // published port and the port the gateway listens on (HTTP_PORTS), and they cannot drift.
        service.Ports.Clear();
        service.Ports.Add("${GATEWAY_PORT}:${GATEWAY_PORT}");
    });

builder.Build().Run();
