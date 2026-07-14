# Spec Requirements Document

> Spec: Database Infrastructure Setup
> Created: 2025-08-15
> Status: Completed
> Completed: 2026-07-08

## Overview

Implement EF Core database infrastructure with PostgreSQL integration for the BookmarkFeeder platform, establishing the foundational data layer with entities, DbContext factory pattern, and Aspire orchestration. This infrastructure will support the core bookmark management functionality and enable subsequent API development phases.

## User Stories

### Database Foundation for Bookmark Management

As a **developer**, I want to establish a robust database infrastructure with EF Core entities and PostgreSQL integration, so that I can build reliable bookmark storage and retrieval functionality.

**Detailed Workflow:**
- EF Core entities (Bookmark, Tag, Category, BookmarkTag) are defined with proper relationships
- PostgreSQL database is integrated with .NET Aspire for development and deployment
- DbContext factory pattern enables efficient database access in service classes
- Code-first migrations create and maintain the database schema
- Seed data provides realistic test scenarios for development

### Scalable Data Access Pattern

As a **developer**, I want to implement the DbContext factory pattern without generic repositories, so that data service classes have direct, efficient access to the database context.

**Detailed Workflow:**
- IDbContextFactory<BookmarkDbContext> is registered in dependency injection
- Service classes receive the factory via constructor injection
- Each service method creates a scoped DbContext for database operations
- Connection pooling and performance optimization are handled by EF Core
- Unit testing is simplified with in-memory database providers

## Spec Scope

1. **EF Core Entity Design** - Define Bookmark, Tag, Category, and BookmarkTag entities with appropriate relationships and constraints
2. **PostgreSQL Integration** - Configure PostgreSQL with .NET Aspire for development and deployment scenarios
3. **DbContext Configuration** - Implement BookmarkDbContext with entity configurations and DbContext factory registration
4. **Migration System** - Create initial migration with proper indexing strategy and seed data for development
5. **Health Checks** - Add database connectivity health checks for monitoring and diagnostics

## Out of Scope

- API endpoint implementation (covered in subsequent specifications)
- Authentication and authorization setup
- Advanced database features (stored procedures, triggers)
- Database backup and recovery procedures
- Production deployment configuration

## Expected Deliverable

1. **Functional Database Schema** - PostgreSQL database created via EF Core migrations with all entities and relationships
2. **Working DbContext Factory** - Service classes can successfully inject and use IDbContextFactory<BookmarkDbContext>
3. **Aspire Integration** - Database runs as part of .NET Aspire orchestration with proper connection strings and health checks