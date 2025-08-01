# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BookmarkFeeder is a self-hosted bookmark management platform designed to replace services like Pocket. It consists of three main components:

1. **Chrome/Edge Browser Extension** (`BookmarkFeeder.BrowserExtension/`) - Sends user bookmarks to the backend
2. **.NET 9 Web API** (`BookmarkFeeder.WebApi/`) - Backend service for processing and storing bookmarks  
3. **.NET Aspire AppHost** (`BookmarkFeeder.AppHost/`) - Orchestrates the distributed application

The system is designed to allow users to select specific bookmark folders from their browser, sync them to a backend service, and manage them with AI-powered categorization using OpenAI's API.

## Technology Stack

- **.NET 9** with Aspire for distributed application orchestration
- **Chrome Extension Manifest v3** for browser integration
- **Tailwind CSS v4** for styling
- **PostgreSQL** (planned) for data persistence
- **OpenAI API** integration for bookmark categorization
- **Docker & Docker Compose** for deployment

## Common Development Commands

### .NET Backend Development
```bash
# Build the entire solution
dotnet build BookmarkFeeder.sln

# Run the Aspire AppHost (starts all services)
dotnet run --project BookmarkFeeder.AppHost

# Run only the Web API
dotnet run --project BookmarkFeeder.WebApi

# Restore packages
dotnet restore
```

### Browser Extension Development
```bash
# Navigate to extension directory
cd BookmarkFeeder.BrowserExtension

# Build Tailwind CSS
npm run build:css

# Watch Tailwind CSS for changes
npm run watch:css
```

## Architecture Notes

### .NET Aspire Integration
- The project uses .NET Aspire 9.1.0 for distributed application management
- `BookmarkFeeder.AppHost` orchestrates all services and provides service discovery
- `BookmarkFeeder.ServiceDefaults` contains shared configuration for OpenTelemetry, health checks, and resilience patterns
- Services use `builder.AddServiceDefaults()` to configure common functionality

### Browser Extension Structure
- Manifest v3 extension with permissions for `bookmarks` and `storage`
- Supports both localhost development and HTTPS hosts
- Uses popup-based UI with Tailwind CSS styling
- Designed to read browser bookmarks via `chrome.bookmarks` API

### Web API Structure  
- ASP.NET Core 9.0 with OpenAPI/Scalar documentation
- CORS enabled for cross-origin requests from browser extension
- Currently has placeholder weather forecast endpoint
- Uses service defaults for telemetry and health checks

## Development Workflow

1. **Starting Development**: Run `dotnet run --project BookmarkFeeder.AppHost` to start all services with Aspire orchestration
2. **API Documentation**: Available at `/scalar/v1` when running in development mode
3. **Extension Development**: Use `npm run watch:css` in the extension directory for live CSS compilation
4. **Service Discovery**: Services are configured to use Aspire's built-in service discovery

## Project Structure

```
BookmarkFeeder/
├── BookmarkFeeder.AppHost/          # Aspire application host
├── BookmarkFeeder.BrowserExtension/ # Chrome/Edge extension
├── BookmarkFeeder.ServiceDefaults/  # Shared service configuration
├── BookmarkFeeder.WebApi/           # Backend Web API
├── docs/CONTEXT.md                  # Detailed project requirements
└── BookmarkFeeder.sln              # Solution file
```

## Key Files to Understand

- `docs/CONTEXT.md` - Complete project requirements and feature specifications
- `BookmarkFeeder.AppHost/Program.cs` - Aspire orchestration setup
- `BookmarkFeeder.ServiceDefaults/Extensions.cs` - Shared service configuration
- `BookmarkFeeder.BrowserExtension/manifest.json` - Extension configuration and permissions

## Development Standards

### Solution Structure Standards

**Project Organization:**
- Follow `BookmarkFeeder.{ProjectName}` naming convention
- Each project must be in its own directory matching the project name
- Use these standard project types:
  - `*.AppHost` - Aspire orchestration
  - `*.ServiceDefaults` - Shared Aspire configuration  
  - `*.WebApi` - Backend API services
  - `*.Core` - Core business logic
  - `*.Data` - Data access layer
  - `*.Infrastructure` - Infrastructure and integration services
  - `*.Dto` - Data transfer objects
  - `*.Tests` - Test projects (using xUnit, Shouldly, NSubstitute)

**Project Dependencies:**
- AppHost → All projects (for orchestration)
- WebApi → Dto, Core, Data
- Core → Dto
- Data → Core, Dto
- Tests → {ProjectName} (test project references source project)

### Configuration & Secrets Management

**NEVER do this:**
- Hardcode API keys or secrets in code
- Store secrets in `appsettings.json`
- Use direct `IConfiguration["key"]` access
- Create `new ConfigurationBuilder()`

**ALWAYS do this:**
- Use Options pattern for configuration
- Store secrets in User Secrets for development
- Use Azure Key Vault for production
- Implement configuration validation with Data Annotations
- Use dependency injection for configuration services

**Example:**
```csharp
// Good: Options pattern
public class OpenAiOptions
{
    [Required]
    public string ApiKey { get; set; }
    
    [Range(1, 10)]
    public int MaxTokens { get; set; }
}

// Registration
services.Configure<OpenAiOptions>(configuration.GetSection("OpenAi"))
    .ValidateDataAnnotations();
```

### API Integration Standards

**HTTP Clients:**
- ALWAYS use `IHttpClientFactory` instead of `new HttpClient()`
- Create typed API clients with interfaces
- Implement retry policies and circuit breakers
- Use proper error handling and logging

**Project Organization:**
```
Core/
├── Abstractions/
│   ├── Services/IBookmarkService.cs
│   └── Clients/IOpenAiClient.cs
└── Models/Api/

Integration.OpenAi/
├── Services/OpenAiService.cs
├── Clients/OpenAiClient.cs
└── DependencyInjection.cs
```

**Example:**
```csharp
// Core/Abstractions/Clients/IOpenAiClient.cs
public interface IOpenAiClient
{
    Task<TagSuggestions> GetTagSuggestionsAsync(BookmarkData bookmark);
}

// Integration.OpenAi/DependencyInjection.cs
public static IServiceCollection AddOpenAiServices(
    this IServiceCollection services, IConfiguration configuration)
{
    services.Configure<OpenAiOptions>(configuration.GetSection("OpenAi"));
    
    services.AddHttpClient<IOpenAiClient, OpenAiClient>()
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy());
        
    return services;
}
```
### dotnet code practices

- Do not use AutoMapper in the backend, prefer manual mapping for better control and performance - create static conversion classes with ToDto() extension methods to map from entities to DTOs
- Use `IEnumerable<T>` for collections in method signatures to allow for deferred execution and better

### Frontend Development (Angular)

**When Angular frontend is added:**
- Use Angular 19 with standalone components
- Implement Tailwind CSS v4 with dark mode support
- Use OnPush change detection strategy
- Follow feature-based module organization
- Use NgRx for complex state management
- Implement proper error boundaries and accessibility

**Component Structure:**
```typescript
@Component({
  selector: 'app-bookmark-list',
  templateUrl: './bookmark-list.component.html',
  styleUrls: ['./bookmark-list.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true
})
export class BookmarkListComponent implements OnInit, OnDestroy {
  @Input() bookmarks: Bookmark[];
  @Output() bookmarkSelected = new EventEmitter<Bookmark>();
}
```

### Git Commit Standards

**Commit Message Format:**
- Subject line: Max 72 characters, start with capital, no period
- Use imperative mood ("Add" not "Added")
- Include body with what/why, wrap at 72 characters
- Reference issues when relevant

**Good Examples:**
```
Add bookmark categorization service

Implement OpenAI integration for automatic bookmark tagging.
This enables users to get AI-powered suggestions for organizing
their bookmarks into relevant categories.

- Add OpenAI client with retry logic
- Implement tag suggestion algorithm
- Add configuration for API settings
- Include error handling for API failures

Closes #42
```

**Bad Examples:**
```
feat: add stuff          # Too vague, uses conventional commit format
Added bookmark feature.  # Wrong tense, ends with period
fix                      # Too short, not descriptive
```

### Code Quality Standards

**General Rules:**
- Follow single responsibility principle
- Use dependency injection throughout
- Implement proper error handling and logging
- Write unit tests for all business logic
- Use async/await for I/O operations
- Follow established naming conventions

**Security:**
- Never commit secrets or API keys
- Use HTTPS for all external communications
- Implement proper input validation
- Use parameterized queries for database access
- Apply principle of least privilege

**Performance:**
- Use appropriate caching strategies
- Implement proper database indexing
- Use async operations for I/O bound work
- Optimize API payloads and responses
- Monitor and measure performance metrics