using Scalar.AspNetCore;
using BookmarkFeeder.WebApi.Data;
using BookmarkFeeder.WebApi.Endpoints;
using BookmarkFeeder.WebApi.Filters;
using BookmarkFeeder.WebApi.Services;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Register the DbContext through a single factory-owned pattern. Services resolve
// IDbContextFactory<BookmarkDbContext> (docs: factory pattern, no generic repositories);
// a scoped context sourced from the factory keeps health checks and scoped consumers working.
builder.Services.AddDbContextFactory<BookmarkDbContext>((_, options) =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("bookmarkfeeder"),
        npgsql => npgsql.EnableRetryOnFailure()));

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IDbContextFactory<BookmarkDbContext>>().CreateDbContext());

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BookmarkDbContext>("database");

builder.Services.AddProblemDetails();

// Trust X-Forwarded-For/Proto from the gateway so the API sees the real scheme/client IP.
// KnownProxies/Networks are cleared because the gateway sits on an internal container network.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Application services (consume IDbContextFactory<BookmarkDbContext>).
builder.Services.AddScoped<IBookmarkService, BookmarkService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

// FluentValidation validators.
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
// Declare the X-API-Key security scheme so Scalar/OpenAPI clients show an auth input.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApiKeyEndpointFilter.HeaderName,
            Description = "Shared API key sent as the X-API-Key header."
        };

        document.Security ??= [];
        document.Security.Add(
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("ApiKey", document, null)] = []
            });

        return Task.CompletedTask;
    });
});

// CORS: the extension authenticates with a custom X-API-Key header (not cookies), so no
// credentials are needed. AllowAnyOrigin keeps self-hosted deployments flexible.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Must run first so downstream middleware sees the gateway-forwarded scheme/IP.
app.UseForwardedHeaders();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        // Pre-fill the dev API key so endpoints are testable with one click.
        options.AddApiKeyAuthentication("ApiKey", scheme => scheme.Value = app.Configuration["Authentication:ApiKey"]);
        options.AddPreferredSecuritySchemes("ApiKey");
    });
}

// HTTPS redirect is opt-in (config `Https:Redirect`, default off): behind the gateway the API
// runs plain HTTP, and forcing HTTPS there would break requests. Enable only when TLS is
// terminated at the API itself.
if (app.Configuration.GetValue<bool>("Https:Redirect"))
{
    app.UseHttpsRedirection();
}

// CORS is retained solely for the browser extension (cross-origin). The web app is same-origin
// via the gateway and does not rely on it.
app.UseCors();

app.MapDefaultEndpoints();

// Fail fast if the API is exposed without an API key outside development. Skipped during
// design-time tooling (EF migrations, OpenAPI GetDocument) which run with no configured key.
if (!IsDesignTimeBuild() && !EF.IsDesignTime && !app.Environment.IsDevelopment() &&
    string.IsNullOrEmpty(app.Configuration["Authentication:ApiKey"]))
{
    throw new InvalidOperationException(
        "Authentication:ApiKey must be configured (user-secrets, environment, or Aspire parameter) outside Development.");
}

// All /api endpoints require the X-API-Key header.
var api = app.MapGroup("/api").AddEndpointFilter<ApiKeyEndpointFilter>();
api.MapGroup("/bookmarks").MapBookmarkEndpoints().WithTags("Bookmarks");
api.MapGroup("/tags").MapTagEndpoints().WithTags("Tags");
api.MapGroup("/categories").MapCategoryEndpoints().WithTags("Categories");

// Initialize database (skip during design-time builds)
if (!IsDesignTimeBuild())
{
    await InitializeDatabaseAsync(app);
}

app.Run();

static bool IsDesignTimeBuild()
{
    return Environment.GetEnvironmentVariable("EF_DESIGN_MODE") == "True" ||
           Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ||
           Environment.GetCommandLineArgs().Any(arg => arg.Contains("GetDocument"));
}

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();

    using var context = factory.CreateDbContext();

    try
    {
        // Apply any pending migrations
        await context.Database.MigrateAsync();

        // Seed development data if in development environment
        if (app.Environment.IsDevelopment())
        {
            await SeedData.SeedDevelopmentDataAsync(context);
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database");
        throw;
    }
}

// Exposed so WebApplicationFactory<Program> can bootstrap the app in integration tests.
public partial class Program;