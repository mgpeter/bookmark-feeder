# Technical Specification

This is the technical specification for the spec detailed in @docs/specs/2025-08-01-api-endpoints-backend-setup/spec.md

## Technical Requirements

### Database Entity Models
- **Bookmark Entity**: URL (required, unique index), Title, Description, DateAdded, DateModified, SourceFolder properties with appropriate validation attributes
- **Tag Entity**: Name (required, unique case-insensitive), NormalizedName, DateCreated properties with string length constraints
- **BookmarkTag Entity**: Many-to-many join table with BookmarkId and TagId foreign keys and composite primary key
- **Audit Properties**: CreatedAt, UpdatedAt timestamps on all entities using EF Core value converters

### Entity Framework Configuration
- **DbContext Configuration**: PostgreSQL provider with connection string from Aspire service discovery
- **Entity Configurations**: Fluent API configurations in separate configuration classes implementing IEntityTypeConfiguration
- **Migration Strategy**: Code-first migrations with initial migration creating complete schema
- **Seeding Data**: Optional development seed data for testing via DbContext OnModelCreating

### API Controller Architecture
- **BookmarksController**: Full CRUD operations with proper HTTP verbs (GET, POST, PUT, DELETE)
- **TagsController**: CRUD operations with additional endpoint for tag search/filtering
- **DTOs**: Request/Response models separate from entity models with validation attributes
- **Mapping Configuration**: Static converter classes with extension methods to map entities and DTOs for clean separation

### PostgreSQL Container Integration
- **Aspire Service Registration**: PostgreSQL container configured in AppHost with proper connection string management
- **Health Checks**: Database connectivity health checks registered in ServiceDefaults
- **Development Configuration**: Container auto-start with development database initialization
- **Connection Resilience**: Retry policies and connection pooling for database operations

### API Documentation and Testing
- **OpenAPI Integration**: Swagger/Scalar documentation with detailed endpoint descriptions
- **Response Models**: Consistent API response format with success/error handling
- **Status Codes**: Proper HTTP status codes (200, 201, 400, 404, 409 for duplicates)
- **Validation**: Model validation with detailed error responses using Problem Details

### Performance and Optimization
- **Database Indexing**: Indexes on URL for uniqueness, Tag.NormalizedName for case-insensitive lookups
- **Query Optimization**: Include statements for related data to prevent N+1 queries
- **Pagination Support**: Query parameters for page size and page number on list endpoints
- **Async Operations**: All database operations using async/await pattern

## External Dependencies

**No new external dependencies required** - All functionality can be implemented using existing packages:
- Microsoft.EntityFrameworkCore (already referenced)
- Npgsql.EntityFrameworkCore.PostgreSQL (already referenced)