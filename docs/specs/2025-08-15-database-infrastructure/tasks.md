# Spec Tasks

## Tasks

- [x] 1. EF Core Entity Implementation
  - [x] 1.1 Write tests for entity definitions and relationships
  - [x] 1.2 Create Bookmark entity with properties and constraints
  - [x] 1.3 Create Tag entity with normalization logic
  - [x] 1.4 Create Category entity with self-referencing hierarchy
  - [x] 1.5 Create BookmarkTag many-to-many join entity
  - [x] 1.6 Verify all entity tests pass

- [x] 2. PostgreSQL Integration with Aspire
  - [x] 2.1 Write tests for database connection and health checks
  - [x] 2.2 Add PostgreSQL NuGet packages to WebApi project
  - [x] 2.3 Configure PostgreSQL service in Aspire AppHost
  - [x] 2.4 Set up connection string configuration
  - [x] 2.5 Implement database health checks
  - [x] 2.6 Verify PostgreSQL integration tests pass

- [x] 3. DbContext Factory Configuration
  - [x] 3.1 Write tests for DbContext factory and entity configurations
  - [x] 3.2 Create BookmarkDbContext with entity configurations
  - [x] 3.3 Configure EF Core entity relationships and constraints
  - [x] 3.4 Register DbContext factory in dependency injection
  - [x] 3.5 Implement soft delete query filters
  - [x] 3.6 Verify all DbContext tests pass

- [x] 4. Database Migration and Seed Data
  - [x] 4.1 Write tests for migration and seed data functionality
  - [x] 4.2 Create initial database migration
  - [x] 4.3 Implement development seed data configuration
  - [x] 4.4 Add database initialization on startup
  - [x] 4.5 Create database reset utility for development
  - [x] 4.6 Verify all migration and seed tests pass