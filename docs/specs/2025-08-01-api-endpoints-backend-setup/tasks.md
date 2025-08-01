# Spec Tasks

## Tasks

- [ ] 1. Database Entity Models and Configuration
  - [ ] 1.1 Write unit tests for entity models (Bookmark, Tag, BookmarkTag)
  - [ ] 1.2 Create Bookmark entity with validation attributes and navigation properties
  - [ ] 1.3 Create Tag entity with name normalization and validation
  - [ ] 1.4 Create BookmarkTag join entity for many-to-many relationship
  - [ ] 1.5 Implement entity configuration classes using IEntityTypeConfiguration
  - [ ] 1.6 Configure fluent API mappings, indexes, and constraints
  - [ ] 1.7 Update DbContext with DbSets and OnModelCreating configuration
  - [ ] 1.8 Verify all entity tests pass

- [ ] 2. Database Migrations and Aspire Integration  
  - [ ] 2.1 Write integration tests for database connectivity and migrations
  - [ ] 2.2 Configure PostgreSQL container in AppHost with proper connection string
  - [ ] 2.3 Update DbContext registration in WebApi with Aspire service discovery
  - [ ] 2.4 Add initial EF Core migration for complete schema
  - [ ] 2.5 Configure automatic migration application on startup
  - [ ] 2.6 Add database health checks to ServiceDefaults
  - [ ] 2.7 Test complete Aspire startup with database container
  - [ ] 2.8 Verify all database integration tests pass

- [ ] 3. Data Transfer Objects and mapping configuration
  - [ ] 3.1 Write tests for DTO validation and mapping scenarios
  - [ ] 3.2 Create BookmarkCreateDto, BookmarkUpdateDto, BookmarkResponseDto
  - [ ] 3.3 Create TagCreateDto, TagUpdateDto, TagResponseDto  
  - [ ] 3.4 Implement Converter classes (static extension ToDto() methods) for manual mapping from entities to DTOs
  - [ ] 3.7 Implement tag name normalization in mapping profiles
  - [ ] 3.8 Verify all DTO and mapping tests pass

- [ ] 4. Bookmarks API Controller Implementation
  - [ ] 4.1 Write comprehensive API tests for all bookmark endpoints
  - [ ] 4.2 Create BookmarksController with proper routing and dependency injection
  - [ ] 4.3 Implement GetBookmarks with pagination and filtering support
  - [ ] 4.4 Implement GetBookmark with proper 404 handling
  - [ ] 4.5 Implement CreateBookmark with URL duplicate detection and tag association
  - [ ] 4.6 Implement UpdateBookmark with conflict resolution and tag management
  - [ ] 4.7 Implement DeleteBookmark with proper cascade handling
  - [ ] 4.8 Add comprehensive error handling and Problem Details responses
  - [ ] 4.9 Verify all bookmark API tests pass

- [ ] 5. Tags API Controller Implementation
  - [ ] 5.1 Write comprehensive API tests for all tag endpoints
  - [ ] 5.2 Create TagsController with proper routing and dependency injection  
  - [ ] 5.3 Implement GetTags with search filtering and pagination
  - [ ] 5.4 Implement GetTag with bookmark count calculation
  - [ ] 5.5 Implement CreateTag with case-insensitive duplicate detection
  - [ ] 5.6 Implement UpdateTag with normalization and conflict handling
  - [ ] 5.7 Implement DeleteTag with bookmark relationship cleanup
  - [ ] 5.8 Add comprehensive error handling and validation
  - [ ] 5.9 Verify all tag API tests pass

- [ ] 6. API Documentation and Integration Testing
  - [ ] 6.1 Write end-to-end integration tests for complete workflows
  - [ ] 6.2 Configure Swagger/Scalar with detailed endpoint documentation
  - [ ] 6.3 Add XML documentation comments to all controllers and DTOs
  - [ ] 6.4 Configure proper HTTP status codes and response examples
  - [ ] 6.5 Test complete bookmark creation workflow with tags
  - [ ] 6.6 Test pagination and filtering across all endpoints
  - [ ] 6.7 Validate error responses match Problem Details format
  - [ ] 6.8 Verify all integration tests pass and documentation is complete