var builder = DistributedApplication.CreateBuilder(args);

// Shared API key for the browser extension -> WebApi. Value comes from the "api-key" parameter
// (see appsettings.Development.json for the local default; override via user-secrets in production).
var apiKey = builder.AddParameter("api-key", secret: true);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("bookmarkfeeder");

var apiService = builder.AddProject<Projects.BookmarkFeeder_WebApi>("webapi")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WithEnvironment("Authentication__ApiKey", apiKey);

// React (Vite) web frontend. Standalone npm app; Aspire runs `npm run dev` and injects PORT.
builder.AddViteApp("web", "../BookmarkFeeder.Web")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithExternalHttpEndpoints();

builder.Build().Run();

