# BookmarkFeeder - API Specification

## Overview

The BookmarkFeeder Web API provides RESTful endpoints for managing bookmarks, tags, categories, and AI-powered categorization. The API follows standard HTTP conventions and returns JSON responses with consistent error handling.

**Base URL**: `https://your-domain.com/api` or `http://localhost:5000/api` (development)
**API Version**: v1
**Authentication**: Bearer token (JWT) or API Key
**Content Type**: `application/json`

## Authentication

### Authentication Methods

#### Bearer Token (Recommended)
```http
Authorization: Bearer <jwt_token>
```

#### API Key (Alternative)
```http
X-API-Key: <api_key>
```

### Authentication Endpoints

#### POST /auth/login
Authenticate user and receive JWT token.

**Request Body**:
```json
{
  "email": "user@example.com",
  "password": "password123"
}
```

**Response**:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expires": "2024-12-31T23:59:59Z",
  "user": {
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "email": "user@example.com",
    "displayName": "John Doe"
  }
}
```

#### POST /auth/refresh
Refresh expired JWT token.

**Request Body**:
```json
{
  "refreshToken": "refresh_token_here"
}
```

## Core Entities

### Bookmark Model
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "url": "https://example.com/article",
  "title": "Example Article Title",
  "description": "Article description or excerpt",
  "faviconUrl": "https://example.com/favicon.ico",
  "sourceFolder": "Research/Technology",
  "isRead": false,
  "dateAdded": "2024-01-15T10:30:00Z",
  "dateModified": "2024-01-15T10:30:00Z",
  "userId": "user_id_here",
  "tags": [
    {
      "id": "tag_id_1",
      "name": "javascript",
      "color": "#3b82f6"
    }
  ],
  "categories": [
    {
      "id": "category_id_1",
      "name": "Web Development",
      "path": "Technology/Web Development",
      "level": 1
    }
  ]
}
```

### Tag Model
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "name": "javascript",
  "color": "#3b82f6",
  "createdDate": "2024-01-15T10:30:00Z",
  "bookmarkCount": 42
}
```

### Category Model
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "name": "Web Development",
  "parentId": "parent_category_id",
  "path": "Technology/Web Development",
  "level": 1,
  "children": [],
  "bookmarkCount": 156
}
```

## Bookmark Endpoints

### GET /bookmarks
Retrieve paginated list of bookmarks with optional filtering.

**Query Parameters**:
- `page` (integer, default: 1): Page number
- `pageSize` (integer, default: 20, max: 100): Items per page
- `search` (string): Full-text search query
- `tags` (string, comma-separated): Filter by tag names
- `categories` (string, comma-separated): Filter by category IDs
- `sourceFolder` (string): Filter by source folder
- `isRead` (boolean): Filter by read status
- `dateFrom` (ISO 8601): Filter bookmarks added after date
- `dateTo` (ISO 8601): Filter bookmarks added before date
- `sortBy` (string): Sort field (dateAdded, dateModified, title, url)
- `sortOrder` (string): Sort direction (asc, desc)

**Example Request**:
```http
GET /api/bookmarks?page=1&pageSize=20&search=javascript&tags=tutorial,guide&sortBy=dateAdded&sortOrder=desc
```

**Response**:
```json
{
  "data": [
    {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "url": "https://example.com/article",
      "title": "JavaScript Tutorial",
      "description": "Learn JavaScript basics",
      "faviconUrl": "https://example.com/favicon.ico",
      "sourceFolder": "Learning",
      "isRead": false,
      "dateAdded": "2024-01-15T10:30:00Z",
      "dateModified": "2024-01-15T10:30:00Z",
      "tags": [{"id": "tag1", "name": "javascript", "color": "#3b82f6"}],
      "categories": [{"id": "cat1", "name": "Programming", "path": "Technology/Programming", "level": 1}]
    }
  ],
  "pagination": {
    "currentPage": 1,
    "pageSize": 20,
    "totalItems": 156,
    "totalPages": 8,
    "hasNext": true,
    "hasPrevious": false
  },
  "filters": {
    "search": "javascript",
    "tags": ["tutorial", "guide"],
    "appliedFilters": 2
  }
}
```

### GET /bookmarks/{id}
Retrieve single bookmark by ID.

**Response**:
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "url": "https://example.com/article",
  "title": "Example Article",
  "description": "Article description",
  "faviconUrl": "https://example.com/favicon.ico",
  "sourceFolder": "Research",
  "isRead": false,
  "dateAdded": "2024-01-15T10:30:00Z",
  "dateModified": "2024-01-15T10:30:00Z",
  "tags": [],
  "categories": []
}
```

### POST /bookmarks
Create new bookmark.

**Request Body**:
```json
{
  "url": "https://example.com/new-article",
  "title": "New Article Title",
  "description": "Optional description",
  "sourceFolder": "Research/Technology",
  "tags": ["javascript", "tutorial"],
  "categories": ["category_id_1"]
}
```

**Response** (201 Created):
```json
{
  "id": "new_bookmark_id",
  "url": "https://example.com/new-article",
  "title": "New Article Title",
  "description": "Optional description",
  "faviconUrl": null,
  "sourceFolder": "Research/Technology",
  "isRead": false,
  "dateAdded": "2024-01-15T10:30:00Z",
  "dateModified": "2024-01-15T10:30:00Z",
  "tags": [{"id": "tag1", "name": "javascript", "color": "#3b82f6"}],
  "categories": [{"id": "cat1", "name": "Programming", "path": "Technology/Programming", "level": 1}]
}
```

### POST /bookmarks/batch
Create multiple bookmarks in a single request.

**Request Body**:
```json
{
  "bookmarks": [
    {
      "url": "https://example1.com",
      "title": "Article 1",
      "sourceFolder": "Research"
    },
    {
      "url": "https://example2.com",
      "title": "Article 2",
      "sourceFolder": "Research"
    }
  ],
  "defaultTags": ["imported"],
  "skipDuplicates": true
}
```

**Response** (201 Created):
```json
{
  "created": [
    {"id": "bookmark1_id", "url": "https://example1.com", "status": "created"},
    {"id": "bookmark2_id", "url": "https://example2.com", "status": "created"}
  ],
  "skipped": [],
  "errors": [],
  "summary": {
    "total": 2,
    "created": 2,
    "skipped": 0,
    "errors": 0
  }
}
```

### PUT /bookmarks/{id}
Update existing bookmark.

**Request Body**:
```json
{
  "title": "Updated Title",
  "description": "Updated description",
  "isRead": true,
  "tags": ["updated", "javascript"],
  "categories": ["new_category_id"]
}
```

**Response**:
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "url": "https://example.com/article",
  "title": "Updated Title",
  "description": "Updated description",
  "faviconUrl": "https://example.com/favicon.ico",
  "sourceFolder": "Research",
  "isRead": true,
  "dateAdded": "2024-01-15T10:30:00Z",
  "dateModified": "2024-01-15T14:45:00Z",
  "tags": [
    {"id": "tag1", "name": "updated", "color": "#10b981"},
    {"id": "tag2", "name": "javascript", "color": "#3b82f6"}
  ],
  "categories": [{"id": "cat1", "name": "New Category", "path": "Technology/New Category", "level": 1}]
}
```

### DELETE /bookmarks/{id}
Delete bookmark.

**Response** (204 No Content):
```json
{
  "message": "Bookmark deleted successfully"
}
```

### PATCH /bookmarks/{id}/read
Mark bookmark as read/unread.

**Request Body**:
```json
{
  "isRead": true
}
```

**Response**:
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "isRead": true,
  "dateModified": "2024-01-15T14:45:00Z"
}
```

## Tag Endpoints

### GET /tags
Retrieve all tags with optional filtering.

**Query Parameters**:
- `search` (string): Filter tags by name
- `sortBy` (string): Sort field (name, createdDate, bookmarkCount)
- `sortOrder` (string): Sort direction (asc, desc)

**Response**:
```json
{
  "data": [
    {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "name": "javascript",
      "color": "#3b82f6",
      "createdDate": "2024-01-15T10:30:00Z",
      "bookmarkCount": 42
    }
  ],
  "totalCount": 156
}
```

### POST /tags
Create new tag.

**Request Body**:
```json
{
  "name": "new-tag",
  "color": "#ef4444"
}
```

**Response** (201 Created):
```json
{
  "id": "new_tag_id",
  "name": "new-tag",
  "color": "#ef4444",
  "createdDate": "2024-01-15T10:30:00Z",
  "bookmarkCount": 0
}
```

### PUT /tags/{id}
Update existing tag.

**Request Body**:
```json
{
  "name": "updated-tag-name",
  "color": "#8b5cf6"
}
```

### DELETE /tags/{id}
Delete tag and remove from all bookmarks.

**Response** (204 No Content)

## Category Endpoints

### GET /categories
Retrieve hierarchical category structure.

**Query Parameters**:
- `includeEmpty` (boolean, default: true): Include categories with no bookmarks
- `maxDepth` (integer): Limit hierarchy depth

**Response**:
```json
{
  "data": [
    {
      "id": "root_category_id",
      "name": "Technology",
      "parentId": null,
      "path": "Technology",
      "level": 0,
      "bookmarkCount": 234,
      "children": [
        {
          "id": "child_category_id",
          "name": "Web Development",
          "parentId": "root_category_id",
          "path": "Technology/Web Development",
          "level": 1,
          "bookmarkCount": 89,
          "children": []
        }
      ]
    }
  ]
}
```

### POST /categories
Create new category.

**Request Body**:
```json
{
  "name": "New Category",
  "parentId": "parent_category_id"
}
```

### PUT /categories/{id}
Update category.

**Request Body**:
```json
{
  "name": "Updated Category Name",
  "parentId": "new_parent_id"
}
```

### DELETE /categories/{id}
Delete category and optionally reassign bookmarks.

**Query Parameters**:
- `reassignTo` (string): Category ID to reassign bookmarks to

## AI Categorization Endpoints

### POST /ai/categorize
Trigger AI categorization for specific bookmarks.

**Request Body**:
```json
{
  "bookmarkIds": ["bookmark_id_1", "bookmark_id_2"],
  "options": {
    "autoApprove": false,
    "confidenceThreshold": 0.8,
    "maxSuggestions": 5
  }
}
```

**Response** (202 Accepted):
```json
{
  "jobId": "categorization_job_id",
  "status": "processing",
  "bookmarkCount": 2,
  "estimatedCompletionTime": "2024-01-15T10:35:00Z"
}
```

### GET /ai/categorize/{jobId}
Check categorization job status.

**Response**:
```json
{
  "jobId": "categorization_job_id",
  "status": "completed",
  "progress": {
    "total": 2,
    "processed": 2,
    "successful": 2,
    "failed": 0
  },
  "results": [
    {
      "bookmarkId": "bookmark_id_1",
      "suggestions": [
        {
          "type": "tag",
          "value": "javascript",
          "confidence": 0.95
        },
        {
          "type": "category",
          "value": "Web Development",
          "confidence": 0.88
        }
      ]
    }
  ],
  "completedAt": "2024-01-15T10:34:23Z"
}
```

### POST /ai/suggestions/approve
Approve AI categorization suggestions.

**Request Body**:
```json
{
  "approvals": [
    {
      "bookmarkId": "bookmark_id_1",
      "suggestions": [
        {
          "type": "tag",
          "value": "javascript",
          "approved": true
        },
        {
          "type": "category",
          "value": "Web Development",
          "approved": false
        }
      ]
    }
  ]
}
```

## Search Endpoints

### GET /search
Perform advanced search across bookmarks.

**Query Parameters**:
- `q` (string): Search query
- `fields` (string, comma-separated): Fields to search (title, url, description, tags)
- `operator` (string): Search operator (and, or)
- `exact` (boolean): Exact phrase matching
- `page` (integer): Page number
- `pageSize` (integer): Items per page

**Response**:
```json
{
  "query": "javascript tutorial",
  "results": [
    {
      "bookmark": {
        "id": "bookmark_id",
        "title": "JavaScript Tutorial",
        "url": "https://example.com",
        "description": "Learn JavaScript basics"
      },
      "score": 0.95,
      "highlights": {
        "title": ["<mark>JavaScript</mark> <mark>Tutorial</mark>"],
        "description": ["Learn <mark>JavaScript</mark> basics"]
      }
    }
  ],
  "pagination": {
    "currentPage": 1,
    "pageSize": 20,
    "totalItems": 15,
    "totalPages": 1
  },
  "facets": {
    "tags": [
      {"name": "javascript", "count": 8},
      {"name": "tutorial", "count": 5}
    ],
    "categories": [
      {"name": "Programming", "count": 10}
    ]
  }
}
```

### POST /search/saved
Create saved search.

**Request Body**:
```json
{
  "name": "JavaScript Resources",
  "query": "javascript",
  "filters": {
    "tags": ["tutorial", "guide"],
    "categories": ["programming"]
  },
  "notifications": true
}
```

### GET /search/saved
Retrieve user's saved searches.

## Error Handling

### Standard Error Response
```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid request data",
    "details": [
      {
        "field": "url",
        "message": "URL is required"
      }
    ],
    "timestamp": "2024-01-15T10:30:00Z",
    "requestId": "req_123456"
  }
}
```

### HTTP Status Codes
- `200 OK`: Successful request
- `201 Created`: Resource created successfully
- `204 No Content`: Successful request with no response body
- `400 Bad Request`: Invalid request data
- `401 Unauthorized`: Authentication required
- `403 Forbidden`: Insufficient permissions
- `404 Not Found`: Resource not found
- `409 Conflict`: Resource conflict (duplicate)
- `422 Unprocessable Entity`: Validation error
- `429 Too Many Requests`: Rate limit exceeded
- `500 Internal Server Error`: Server error

### Error Codes
- `VALIDATION_ERROR`: Request validation failed
- `AUTHENTICATION_REQUIRED`: Authentication token missing or invalid
- `PERMISSION_DENIED`: Insufficient permissions for operation
- `RESOURCE_NOT_FOUND`: Requested resource does not exist
- `DUPLICATE_RESOURCE`: Resource already exists
- `RATE_LIMIT_EXCEEDED`: Too many requests
- `AI_SERVICE_UNAVAILABLE`: AI categorization service temporarily unavailable
- `EXTERNAL_SERVICE_ERROR`: External service integration failed

## Rate Limiting

### Rate Limit Headers
```http
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 999
X-RateLimit-Reset: 1642248000
X-RateLimit-Window: 3600
```

### Rate Limits by Endpoint
- **Authentication**: 10 requests/minute
- **Bookmark CRUD**: 100 requests/minute
- **Search**: 60 requests/minute
- **AI Categorization**: 20 requests/minute
- **Bulk Operations**: 5 requests/minute

## Webhooks (Future Feature)

### Webhook Events
- `bookmark.created`
- `bookmark.updated`
- `bookmark.deleted`
- `categorization.completed`
- `tag.created`

### Webhook Payload Example
```json
{
  "event": "bookmark.created",
  "timestamp": "2024-01-15T10:30:00Z",
  "data": {
    "bookmark": {
      "id": "bookmark_id",
      "url": "https://example.com",
      "title": "New Bookmark"
    }
  }
}
```

This API specification provides comprehensive coverage of all BookmarkFeeder functionality while maintaining RESTful conventions and clear documentation for developers and integrators.