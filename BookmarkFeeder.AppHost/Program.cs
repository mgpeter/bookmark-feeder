var builder = DistributedApplication.CreateBuilder(args);

// Publishing target: `aspire publish` emits a Docker Compose project for this environment.
builder.AddDockerComposeEnvironment("compose");

// Shared API key for the browser extension -> WebApi. Value comes from the "api-key" parameter
// (see appsettings.Development.json for the local default; override via user-secrets in production).
var apiKey = builder.AddParameter("api-key", secret: true);

// Pinned explicitly: unpinned, this floats on whatever Aspire's default happens to be, so an
// Aspire upgrade could change the database's MAJOR version under a live data volume. 18.x is
// what the dev volume already holds (PG_VERSION = 18) and what the test suite runs.
// The volume is named explicitly. Left to itself, WithDataVolume() derives a hashed name that
// differs between contexts (dev run emitted ...-c707ae991a-..., publish emitted ...-190750286b-...).
// On a NAS that is a silent data-loss trap: a shifted hash means compose creates a fresh empty
// volume and the collection appears to vanish. A fixed name makes the database's identity
// deterministic across every publish.
var postgres = builder.AddPostgres("postgres")
    .WithImageTag("18.3")
    .WithDataVolume("bookmarkfeeder-postgres-data")
    .AddDatabase("bookmarkfeeder");

var apiService = builder.AddProject<Projects.BookmarkFeeder_WebApi>("webapi")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WithEnvironment("Authentication__ApiKey", apiKey);

// React (Vite) web frontend. Standalone npm app; Aspire runs `npm run dev` and injects PORT.
// Internal only — reached through the gateway, not exposed directly. On publish it is built
// from its Dockerfile (multi-stage: Vite build -> static nginx).
var web = builder.AddViteApp("web", "../BookmarkFeeder.Web")
    .PublishAsDockerFile();

// YARP gateway: the single external entry point. Routes /api -> webapi and / -> web
// via service discovery (destinations "http://api" / "http://web").
builder.AddProject<Projects.BookmarkFeeder_Gateway>("gateway")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithReference(web)
    .WaitFor(web)
    .WithExternalHttpEndpoints();

builder.Build().Run();

