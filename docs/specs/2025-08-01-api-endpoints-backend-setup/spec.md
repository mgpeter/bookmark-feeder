# Spec Requirements Document

> Spec: API Endpoints Backend Setup
> Created: 2025-08-01
> Status: Planning

## Overview

Implement the foundational API endpoints for bookmark management with complete backend project initialization and PostgreSQL database setup using EF Core code-first migrations. This establishes the core data layer and API infrastructure needed for the MVP, running on containerized PostgreSQL via Aspire for development.

## User Stories

### Core Bookmark Management API

As a browser extension developer, I want to send bookmarks to a REST API, so that users can store and manage their bookmarks in the self-hosted backend.

This involves creating a complete bookmark management API with endpoints for creating, reading, updating, and deleting bookmarks. The API must handle bookmark data including URL, title, description, tags, and source information, with proper validation and duplicate detection.

### Database-First Development Environment

As a developer, I want a fully configured database environment with migrations, so that I can develop and test bookmark features without manual database setup.

This includes PostgreSQL running in a container via Aspire, Entity Framework Core models and context properly configured, and database migrations that can be applied automatically during development startup.

### Tag Management System

As a bookmark user, I want to organize bookmarks with tags, so that I can categorize and find my bookmarks efficiently.

The system must support creating, reading, updating, and deleting tags, with many-to-many relationships between bookmarks and tags, including tag normalization and case-insensitive handling.

## Spec Scope

1. **Database Entity Models** - Complete bookmark and tag entities with proper relationships and validation
2. **EF Core DbContext Configuration** - Fully configured context with PostgreSQL connection and migration support
3. **Core CRUD API Endpoints** - RESTful endpoints for bookmarks and tags with proper HTTP status codes
4. **PostgreSQL Container Integration** - Aspire-managed PostgreSQL container for development with automatic startup
5. **Database Migrations** - Code-first migrations for initial schema creation and seeding

## Out of Scope

- Authentication and authorization (Phase 3)
- Frontend Angular application
- Browser extension communication logic
- OpenAI integration features
- Import/export functionality
- Advanced search capabilities

## Expected Deliverable

1. Database successfully creates and migrates on application startup via Aspire
2. All CRUD endpoints functional and testable via Swagger/Scalar documentation
3. Bookmark entities can be created, retrieved, updated, and deleted through API calls
4. Tag management fully operational with proper many-to-many relationships
5. Development environment runs completely via `dotnet run --project BookmarkFeeder.AppHost`