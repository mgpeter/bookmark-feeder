# BookmarkFeeder - Product Documentation

## Overview

This directory contains comprehensive product documentation for BookmarkFeeder, a self-hosted bookmark management platform that replaces services like Pocket with privacy-first, AI-powered organization capabilities.

## Documentation Structure

### 📋 [01. Product Overview](./01-product-overview.md)
- **Vision Statement**: Privacy-first bookmark management with AI intelligence
- **Problem Statement**: Limitations of existing cloud-based bookmark services
- **Solution Overview**: Self-hosted platform with intelligent organization
- **Target Users**: Privacy-conscious professionals, self-hosting enthusiasts, heavy bookmark users
- **Value Proposition**: Complete data ownership with AI-powered organization
- **Key Differentiators**: AI categorization, privacy-first design, browser integration
- **Success Metrics**: User adoption, feature utilization, performance benchmarks

### 🔧 [02. Feature Specifications](./02-feature-specifications.md)
- **Browser Extension Integration**: Folder selection, synchronization, server configuration
- **Bookmark Storage**: Core data model, duplicate detection, bulk operations
- **AI-Powered Categorization**: Automatic tagging, category suggestions, batch processing
- **Search and Discovery**: Full-text search, advanced filtering, saved searches
- **Web Interface**: Bookmark browsing, management, dashboard analytics
- **Technical Features**: Authentication, data export/import, performance optimization
- **Quality Attributes**: Performance, reliability, usability, security requirements

### 🏗️ [03. Technical Architecture](./03-technical-architecture.md)
- **System Overview**: Component architecture and technology stack
- **Browser Extension**: Manifest V3, JavaScript, Tailwind CSS architecture
- **Web API Backend**: ASP.NET Core 9, Entity Framework, clean architecture patterns
- **Database Design**: PostgreSQL schema, performance optimizations, indexing strategy
- **Infrastructure**: .NET Aspire orchestration, Docker deployment, service configuration
- **Security Architecture**: Authentication strategies, API security, data protection
- **Integration**: OpenAI integration, browser communication, external services
- **Scalability**: Database scaling, application performance, monitoring considerations

### 🗺️ [04. Roadmap](./04-roadmap.md)
- **Phase 0**: ✅ Foundation Infrastructure (COMPLETED)
  - .NET Aspire AppHost, Web API scaffolding, Browser extension foundation, Service defaults
- **Phase 1**: 🔄 Core Data Layer & API Foundation (IN PLANNING)
  - Database infrastructure, bookmark management endpoints, tag/category API
- **Phase 2**: 📋 Browser Extension Integration (PLANNED)
  - API communication, folder selection, synchronization features
- **Phase 3**: 🤖 AI-Powered Categorization (PLANNED)
  - OpenAI integration, batch processing, approval workflows
- **Phase 4**: 🔍 Search and Discovery (PLANNED)
  - Full-text search, filtering, saved searches, analytics
- **Phase 5**: 🌐 Web Frontend Interface (PLANNED)
  - Angular 19 application, responsive design, management interface
- **Phase 6**: 🚀 Production Ready & Advanced Features (PLANNED)
  - Docker deployment, performance optimization, advanced features

### 👥 [05. User Personas and Use Cases](./05-user-personas-and-use-cases.md)
- **Primary Personas**:
  - **Alex Chen**: Privacy-conscious developer with home lab infrastructure
  - **Dr. Sarah Martinez**: Academic researcher needing organized research materials
  - **Michael Thompson**: Digital marketing consultant managing client resources
- **Secondary Personas**: Knowledge workers, content creators
- **Core Use Cases**: Setup/migration, daily collection, research/retrieval, collaboration, maintenance
- **User Journey Maps**: New user onboarding, power user optimization workflows

### 🔌 [06. API Specification](./06-api-specification.md)
- **Authentication**: JWT tokens, API keys, refresh mechanisms
- **Core Entities**: Bookmark, Tag, Category data models with relationships
- **Bookmark Endpoints**: CRUD operations, batch processing, search functionality
- **Tag/Category Management**: Hierarchical categories, tag operations
- **AI Categorization**: Async processing, job status, suggestion approval
- **Search Capabilities**: Advanced search, saved searches, faceted results
- **Error Handling**: Standard responses, HTTP status codes, rate limiting
- **Integration Patterns**: Webhooks, bulk operations, performance considerations

## Quick Start Guide

### For Product Managers
1. Start with [Product Overview](./01-product-overview.md) for vision and market positioning
2. Review [User Personas](./05-user-personas-and-use-cases.md) for target audience understanding
3. Examine [Roadmap](./04-roadmap.md) for development timeline and priorities
4. Check [Feature Specifications](./02-feature-specifications.md) for detailed requirements

### For Developers
1. Begin with [Technical Architecture](./03-technical-architecture.md) for system design
2. Review [API Specification](./06-api-specification.md) for implementation details
3. Follow [Roadmap](./04-roadmap.md) for development phases and priorities
4. Reference [Feature Specifications](./02-feature-specifications.md) for implementation requirements

### For Stakeholders
1. Read [Product Overview](./01-product-overview.md) for business case and value proposition
2. Review [User Personas](./05-user-personas-and-use-cases.md) for market fit validation
3. Examine [Roadmap](./04-roadmap.md) for timeline and milestone planning
4. Consider [Technical Architecture](./03-technical-architecture.md) for infrastructure requirements

## Key Project Characteristics

### Technology Stack
- **Backend**: .NET 9, ASP.NET Core Web API, Entity Framework Core, PostgreSQL
- **Frontend**: Angular 19 (planned), Tailwind CSS v4, Shadcn UI
- **Browser Extension**: Chrome/Edge Manifest V3, Vanilla JavaScript
- **AI Integration**: OpenAI GPT API via dotnet OpenAI package
- **Infrastructure**: .NET Aspire, Docker, Docker Compose
- **Architecture**: Clean architecture, DbContext factory pattern, service layer design

### Development Priorities
1. **WebAPI First**: Core backend functionality takes priority
2. **Privacy by Design**: Self-hosted, no data sharing with third parties
3. **AI-Enhanced UX**: Intelligent categorization without vendor lock-in
4. **Browser Integration**: Seamless workflow with existing bookmark habits
5. **Scalable Architecture**: Foundation for future enhancements and team growth

### Current Status (Phase 0 Complete)
✅ **Infrastructure Foundation**
- .NET Aspire AppHost orchestration operational
- Web API project scaffolded with service defaults
- Browser extension basic structure implemented
- Development environment fully configured
- Project documentation and context established

🔄 **Next Phase (Phase 1)**
- Database schema design and EF Core implementation
- Core bookmark CRUD API endpoints
- Tag and category management system
- PostgreSQL integration with Aspire

## Related Documentation

### Technical Documentation
- [Context Documentation](../CONTEXT.md) - Original project context and requirements
- [API Documentation](./06-api-specification.md) - Complete API reference
- [Architecture Documentation](./03-technical-architecture.md) - System design details

### Project Files
- [Solution Structure](../../BookmarkFeeder.sln) - Visual Studio solution
- [Browser Extension](../../BookmarkFeeder.BrowserExtension/) - Chrome/Edge extension code
- [Web API](../../BookmarkFeeder.WebApi/) - ASP.NET Core backend
- [Aspire Host](../../BookmarkFeeder.AppHost/) - Application orchestration

## Contributing to Documentation

When updating product documentation:

1. **Maintain Consistency**: Follow established formatting and structure patterns
2. **Update Cross-References**: Ensure links between documents remain accurate
3. **Version Changes**: Document significant changes in roadmap and feature specs
4. **Technical Accuracy**: Verify technical details match implementation
5. **User Focus**: Keep user needs and personas in mind for all documentation

## Questions and Feedback

For questions about product direction, feature requirements, or documentation clarifications:

1. Review existing documentation for answers
2. Check current roadmap for planned features
3. Consider user personas and use cases for context
4. Propose changes through standard project channels

This documentation serves as the single source of truth for BookmarkFeeder product direction, technical architecture, and development planning.