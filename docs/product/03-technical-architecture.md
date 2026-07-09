# BookmarkFeeder - Technical Architecture

## System Overview

BookmarkFeeder follows a modern microservices-inspired architecture with clear separation of concerns across four main components:

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  Browser        │    │  Web Frontend   │    │  External APIs  │
│  Extension      │    │  (React 19)     │    │  (OpenAI GPT)   │
│                 │    │                 │    │                 │
│  - Bookmark API │    │  - User Interface│   │  - AI Services  │
│  - Sync Logic   │    │  - Search/Filter│    │  - Categorization│
│  - Settings     │    │  - Management   │    │                 │
└─────────┬───────┘    └─────────┬───────┘    └─────────┬───────┘
          │                      │                      │
          │              ┌───────┴────────┐             │
          │              │                │             │
          └──────────────┤   Web API      ├─────────────┘
                         │  (.NET 9)      │
                         │                │
                         │  - REST APIs   │
                         │  - Business    │
                         │    Logic       │
                         │  - Data Access │
                         └─────────┬──────┘
                                   │
                         ┌─────────┴──────┐
                         │  PostgreSQL    │
                         │  Database      │
                         │                │
                         │  - Bookmarks   │
                         │  - Tags        │
                         │  - Categories  │
                         │  - Users       │
                         └────────────────┘
```

## Component Architecture

### 1. Browser Extension (BookmarkFeeder.BrowserExtension)
**Technology**: Manifest V3, Vanilla JavaScript, Tailwind CSS v4

#### Architecture Patterns
- **Event-driven UI**: Popup interface responds to user actions
- **Storage Management**: Chrome storage API for configuration persistence
- **API Communication**: Fetch-based HTTP client for backend integration

#### Key Components
```
Extension/
├── manifest.json           # Extension configuration
├── popup.html             # Main UI interface
├── popup.js               # UI logic and API communication
├── background.js          # Service worker (future)
├── css/
│   └── tailwind.css       # Styling framework
└── icons/                 # Extension branding assets
```

#### Security Considerations
- Content Security Policy (CSP) compliance
- Minimal permissions scope (bookmarks, storage only)
- HTTPS-only communication with backend
- Input sanitization for user-provided server URLs

### 2. Web API Backend (BookmarkFeeder.WebApi)
**Technology**: ASP.NET Core 9, Entity Framework Core, PostgreSQL

#### Architecture Patterns
- **Clean Architecture**: Separation of concerns with clear dependencies
- **Service Layer Pattern**: Data services with DbContext factory injection (no generic repositories)
- **Service Layer**: Business logic encapsulation
- **Dependency Injection**: IoC container for loose coupling

#### Project Structure
```
BookmarkFeeder.WebApi/
├── Program.cs                    # Application entry point
├── Controllers/
│   ├── BookmarksController.cs    # Bookmark CRUD operations
│   ├── TagsController.cs         # Tag management
│   └── CategoriesController.cs   # Category management
├── Services/
│   ├── BookmarkService.cs        # Business logic
│   ├── AiCategorizationService.cs# OpenAI integration
│   └── DuplicateDetectionService.cs
├── Models/
│   ├── Entities/                 # EF Core entities
│   ├── DTOs/                     # Data transfer objects
│   └── Requests/                 # API request models
├── Data/
│   ├── BookmarkContext.cs        # EF Core DbContext
│   └── Migrations/               # Database schema changes
└── Configuration/
    ├── DatabaseConfiguration.cs  # EF Core setup
    └── OpenAiConfiguration.cs    # AI service setup
```

#### Data Layer Architecture
```csharp
// Entity Framework Code-First approach
public class BookmarkContext : DbContext
{
    public DbSet<Bookmark> Bookmarks { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<BookmarkTag> BookmarkTags { get; set; }
}

// Service layer with business logic
public class BookmarkService
{
    private readonly BookmarkContext _context;
    private readonly IAiCategorizationService _aiService;
    private readonly IDuplicateDetectionService _duplicateService;
}
```

#### API Design Principles
- RESTful conventions with standard HTTP methods
- Consistent JSON response format with error handling
- Pagination for large data sets
- API versioning strategy (header-based)
- Comprehensive input validation with FluentValidation

### 3. Web Frontend (BookmarkFeeder.Web)
**Technology**: React 19, Vite, Tailwind CSS v4, shadcn/ui, TanStack Query, React Router
**Status**: Planned for Phase 3

#### Architecture Patterns
- **Component-based Architecture**: Reusable UI components
- **Service-oriented Design**: Separation of data access and UI logic
- **Server state via TanStack Query**: Data fetching, caching, and mutations
- **Lazy Loading**: Route-based code splitting for performance

#### Planned Structure
```
BookmarkFeeder.Web/
├── index.html
├── vite.config.ts
└── src/
    ├── lib/                     # api-client (fetch wrapper), helpers
    ├── config/                  # App/runtime configuration
    ├── api/                     # TanStack Query hooks
    ├── types/                   # Shared TypeScript types
    ├── components/
    │   ├── ui/                  # shadcn/ui components
    │   └── ...                  # Shared components
    ├── layout/                  # Page layouts
    ├── features/                # Feature folders
    │   ├── dashboard/
    │   ├── bookmarks/
    │   ├── tags/
    │   ├── categories/
    │   └── settings/
    └── routes.tsx               # React Router route definitions
```

### 4. Database Design
**Technology**: PostgreSQL with Entity Framework Core

#### Schema Design
```sql
-- Core bookmark entity
CREATE TABLE bookmarks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    url TEXT NOT NULL,
    title TEXT NOT NULL,
    description TEXT,
    favicon_url TEXT,
    source_folder TEXT,
    is_read BOOLEAN DEFAULT FALSE,
    date_added TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    date_modified TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    user_id UUID REFERENCES users(id)
);

-- Tag system for flexible categorization
CREATE TABLE tags (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL UNIQUE,
    color TEXT,
    created_date TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Many-to-many relationship for bookmark tags
CREATE TABLE bookmark_tags (
    bookmark_id UUID REFERENCES bookmarks(id) ON DELETE CASCADE,
    tag_id UUID REFERENCES tags(id) ON DELETE CASCADE,
    PRIMARY KEY (bookmark_id, tag_id)
);

-- Hierarchical categories
CREATE TABLE categories (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    parent_id UUID REFERENCES categories(id),
    path TEXT, -- Materialized path for efficient queries
    level INTEGER DEFAULT 0
);
```

#### Performance Optimizations
- **Indexes**: Optimized for common query patterns
  ```sql
  CREATE INDEX idx_bookmarks_url ON bookmarks(url);
  CREATE INDEX idx_bookmarks_date_added ON bookmarks(date_added DESC);
  CREATE INDEX idx_bookmarks_user_id ON bookmarks(user_id);
  CREATE INDEX idx_bookmarks_full_text ON bookmarks USING GIN(to_tsvector('english', title || ' ' || COALESCE(description, '')));
  ```
- **Connection Pooling**: Configured in EF Core for efficient resource usage
- **Query Optimization**: Use of Include() for related data, projection for large datasets

## Infrastructure Architecture

### 1. .NET Aspire Orchestration
**Current Implementation**: Basic Aspire AppHost setup

```csharp
// BookmarkFeeder.AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
                     .WithPgAdmin();

var apiService = builder.AddProject<Projects.BookmarkFeeder_WebApi>("webapi")
                        .WithReference(postgres);

var web = builder.AddViteApp("web", "../BookmarkFeeder.Web")
                 .WithReference(apiService)
                 .WithExternalHttpEndpoints();

builder.Build().Run();
```

### 2. Docker Deployment Architecture
**Target Deployment**: Docker Compose for self-hosted environments

```yaml
# docker-compose.yml (planned)
version: '3.8'
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: bookmarkfeeder
      POSTGRES_USER: bookmarkfeeder
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    
  webapi:
    build: ./BookmarkFeeder.WebApi
    environment:
      ConnectionStrings__DefaultConnection: ${CONNECTION_STRING}
      OpenAI__ApiKey: ${OPENAI_API_KEY}
    depends_on:
      - postgres
    
  frontend:
    build: ./BookmarkFeeder.Web
    environment:
      API_BASE_URL: http://webapi:8080
    depends_on:
      - webapi
    ports:
      - "80:80"
    # Production alternative: the API serves the built SPA (dist/) from its
    # wwwroot, so the frontend and API share a single origin and this
    # separate service can be omitted.

volumes:
  postgres_data:
```

### 3. Service Defaults and Configuration
**Current Implementation**: Shared configuration library

```csharp
// BookmarkFeeder.ServiceDefaults/Extensions.cs
public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();
        return builder;
    }
}
```

## Security Architecture

### 1. Authentication Strategy
**Planned Implementation**: Flexible authentication approach

#### Option 1: ASP.NET Core Identity (Recommended for self-hosted)
```csharp
public class BookmarkFeederUser : IdentityUser
{
    public string DisplayName { get; set; }
    public DateTime CreatedDate { get; set; }
    public UserPreferences Preferences { get; set; }
}
```

#### Option 2: JWT + External Provider (Auth0)
- Backend-for-Frontend (BFF) pattern for browser extension
- Secure token storage and refresh mechanisms
- Social login integration options

### 2. API Security
- **HTTPS Enforcement**: TLS termination at reverse proxy level
- **CORS Configuration**: Strict origin policies for browser extension
- **Rate Limiting**: Per-endpoint throttling to prevent abuse
- **Input Validation**: Comprehensive validation with FluentValidation
- **SQL Injection Prevention**: Parameterized queries through EF Core

### 3. Data Protection
- **Encryption at Rest**: Database-level encryption for sensitive data
- **API Key Security**: Secure storage of OpenAI API keys
- **Audit Logging**: Change tracking for bookmark modifications
- **Backup Strategy**: Automated database backups with encryption

## Integration Architecture

### 1. OpenAI Integration
```csharp
public class AiCategorizationService : IAiCategorizationService
{
    private readonly OpenAIClient _openAiClient;
    
    public async Task<CategorySuggestion[]> SuggestCategoriesAsync(Bookmark bookmark)
    {
        var prompt = BuildCategorizationPrompt(bookmark);
        var response = await _openAiClient.GetChatCompletionsAsync(
            deploymentOrModelName: "gpt-4",
            chatCompletionsOptions: new ChatCompletionsOptions
            {
                Messages = { new ChatRequestUserMessage(prompt) },
                MaxTokens = 200,
                Temperature = 0.3f
            }
        );
        
        return ParseCategoryResponse(response.Value.Choices[0].Message.Content);
    }
}
```

### 2. Browser Extension Communication
- **REST API**: Standard HTTP/JSON communication
- **Authentication**: Bearer token or API key-based
- **Error Handling**: Graceful degradation with retry logic
- **Batching**: Efficient bulk operations for large bookmark sets

## Scalability Considerations

### 1. Database Scaling
- **Read Replicas**: For read-heavy workloads
- **Partitioning**: Date-based partitioning for large bookmark datasets
- **Caching**: Redis for frequently accessed data
- **Connection Pooling**: Optimized connection management

### 2. Application Scaling
- **Horizontal Scaling**: Stateless API design for load balancing
- **Background Processing**: Queue-based processing for AI operations
- **CDN Integration**: Static asset distribution
- **Monitoring**: Application Performance Monitoring (APM) integration

### 3. Performance Optimization
- **Database Indexes**: Query-specific optimization
- **API Response Caching**: Strategic caching with appropriate TTL
- **Lazy Loading**: Deferred loading of related entities
- **Compression**: Response compression for large payloads

This architecture provides a solid foundation for a scalable, maintainable, and secure bookmark management platform while maintaining the flexibility needed for self-hosted deployment scenarios.