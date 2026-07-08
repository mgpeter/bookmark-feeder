# Technical Specification

This is the technical specification for the spec detailed in @docs/specs/2025-08-15-database-infrastructure/spec.md

## Technical Requirements

### EF Core Entity Configuration

- **Bookmark Entity**: Id (Guid), Url (string, max 2048, unique index), Title (string, max 500), Description (string, max 2000), DateAdded (DateTime), DateModified (DateTime), IsDeleted (bool, soft delete)
- **Tag Entity**: Id (Guid), Name (string, max 100, unique index), NormalizedName (string, max 100, computed), DateCreated (DateTime)
- **Category Entity**: Id (Guid), Name (string, max 200), ParentCategoryId (Guid?, self-referencing FK), Description (string, max 1000), DateCreated (DateTime)
- **BookmarkTag Entity**: BookmarkId (Guid, FK), TagId (Guid, FK), DateAssigned (DateTime), composite primary key on BookmarkId + TagId

### Database Configuration

- **Connection String**: Configured via .NET Aspire with environment-specific overrides
- **Connection Pooling**: EF Core default pooling with max pool size 100
- **Command Timeout**: 30 seconds default, configurable via appsettings
- **Retry Policy**: Exponential backoff for transient failures (3 retries, 2-8 second delays)

### DbContext Factory Implementation

- **Registration**: `services.AddDbContextFactory<BookmarkDbContext>(options => options.UseNpgsql(connectionString))`
- **Scope Management**: Service classes create scoped DbContext instances via factory
- **Dispose Pattern**: Using statements ensure proper DbContext disposal in service methods
- **Thread Safety**: Factory is thread-safe, individual DbContext instances are not shared

### Migration Strategy

- **Code-First Approach**: All schema changes via EF Core migrations
- **Naming Convention**: Migration names include timestamp and descriptive action (e.g., "20250815120000_InitialBookmarkSchema")
- **Index Strategy**: Composite indexes on frequently queried columns (Url, NormalizedTagName, DateAdded)
- **Seed Data**: Development seed includes 20 sample bookmarks with 15 tags across 5 categories

### Performance Optimization

- **Indexing**: Primary keys (clustered), unique constraints on Url and NormalizedName, composite index on BookmarkTag
- **Query Optimization**: Navigation properties configured for optimal loading strategies
- **Connection Management**: DbContext factory handles connection lifecycle automatically

## External Dependencies

- **Npgsql.EntityFrameworkCore.PostgreSQL** (8.0.0+) - PostgreSQL provider for EF Core
- **Microsoft.EntityFrameworkCore.Design** (9.0.0+) - Design-time tools for migrations
- **Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore** (9.0.0+) - Database health checks