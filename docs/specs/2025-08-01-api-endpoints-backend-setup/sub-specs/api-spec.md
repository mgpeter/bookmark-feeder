# API Specification

This is the API specification for the spec detailed in @docs/specs/2025-08-01-api-endpoints-backend-setup/spec.md

## Base Configuration

**Base URL:** `https://localhost:7443/api` (development)  
**Content-Type:** `application/json`  
**Response Format:** JSON with consistent error handling using Problem Details (RFC 7807)

## Bookmarks Controller

### GET /api/bookmarks

**Purpose:** Retrieve paginated list of bookmarks with optional filtering  
**Parameters:**
- `page` (optional): Page number, default 1
- `pageSize` (optional): Items per page, default 20, max 100
- `sourceFolder` (optional): Filter by source folder
- `tag` (optional): Filter by tag name

**Response:** 200 OK
```json
{
  "data": [
    {
      "id": 1,
      "url": "https://example.com",
      "title": "Example Site",
      "description": "A sample bookmark",
      "sourceFolder": "Development",
      "createdAt": "2025-08-01T10:00:00Z",
      "updatedAt": "2025-08-01T10:00:00Z",
      "tags": ["web", "example"]
    }
  ],
  "pagination": {
    "currentPage": 1,
    "pageSize": 20,
    "totalCount": 50,
    "totalPages": 3
  }
}
```

**Errors:** 400 Bad Request (invalid parameters)

### GET /api/bookmarks/{id}

**Purpose:** Retrieve specific bookmark by ID  
**Parameters:** 
- `id`: Bookmark identifier (path parameter)

**Response:** 200 OK
```json
{
  "id": 1,
  "url": "https://example.com",
  "title": "Example Site",
  "description": "A sample bookmark",
  "sourceFolder": "Development",
  "createdAt": "2025-08-01T10:00:00Z",
  "updatedAt": "2025-08-01T10:00:00Z",
  "tags": ["web", "example"]
}
```

**Errors:** 404 Not Found

### POST /api/bookmarks

**Purpose:** Create new bookmark with duplicate URL detection  
**Request Body:**
```json
{
  "url": "https://example.com",
  "title": "Example Site",
  "description": "A sample bookmark",
  "sourceFolder": "Development",
  "tags": ["web", "example"]
}
```

**Response:** 201 Created (Location header with bookmark URL)
```json
{
  "id": 1,
  "url": "https://example.com",
  "title": "Example Site",
  "description": "A sample bookmark",
  "sourceFolder": "Development",
  "createdAt": "2025-08-01T10:00:00Z",
  "updatedAt": "2025-08-01T10:00:00Z",
  "tags": ["web", "example"]
}
```

**Errors:** 
- 400 Bad Request (validation errors)
- 409 Conflict (duplicate URL)

### PUT /api/bookmarks/{id}

**Purpose:** Update existing bookmark including tags  
**Parameters:** 
- `id`: Bookmark identifier (path parameter)

**Request Body:**
```json
{
  "url": "https://example.com",
  "title": "Updated Example Site",
  "description": "An updated sample bookmark",
  "sourceFolder": "Development",
  "tags": ["web", "example", "updated"]
}
```

**Response:** 200 OK
```json
{
  "id": 1,
  "url": "https://example.com",
  "title": "Updated Example Site",
  "description": "An updated sample bookmark",
  "sourceFolder": "Development",
  "createdAt": "2025-08-01T10:00:00Z",
  "updatedAt": "2025-08-01T11:30:00Z",
  "tags": ["web", "example", "updated"]
}
```

**Errors:**
- 400 Bad Request (validation errors)
- 404 Not Found
- 409 Conflict (duplicate URL)

### DELETE /api/bookmarks/{id}

**Purpose:** Delete bookmark and associated tag relationships  
**Parameters:** 
- `id`: Bookmark identifier (path parameter)

**Response:** 204 No Content

**Errors:** 404 Not Found

## Tags Controller

### GET /api/tags

**Purpose:** Retrieve all tags with optional search filtering  
**Parameters:**
- `search` (optional): Filter tags by name (case-insensitive partial match)
- `page` (optional): Page number, default 1
- `pageSize` (optional): Items per page, default 50, max 200

**Response:** 200 OK
```json
{
  "data": [
    {
      "id": 1,
      "name": "web",
      "bookmarkCount": 15,
      "createdAt": "2025-08-01T10:00:00Z"
    }
  ],
  "pagination": {
    "currentPage": 1,
    "pageSize": 50,
    "totalCount": 25,
    "totalPages": 1
  }
}
```

### GET /api/tags/{id}

**Purpose:** Retrieve specific tag with associated bookmarks count  
**Parameters:** 
- `id`: Tag identifier (path parameter)

**Response:** 200 OK
```json
{
  "id": 1,
  "name": "web",
  "bookmarkCount": 15,
  "createdAt": "2025-08-01T10:00:00Z"
}
```

**Errors:** 404 Not Found

### POST /api/tags

**Purpose:** Create new tag with case-insensitive duplicate detection  
**Request Body:**
```json
{
  "name": "javascript"
}
```

**Response:** 201 Created
```json
{
  "id": 2,
  "name": "javascript",
  "bookmarkCount": 0,
  "createdAt": "2025-08-01T10:30:00Z"
}
```

**Errors:**
- 400 Bad Request (validation errors)
- 409 Conflict (duplicate tag name, case-insensitive)

### PUT /api/tags/{id}

**Purpose:** Update tag name with duplicate detection  
**Parameters:** 
- `id`: Tag identifier (path parameter)

**Request Body:**
```json
{
  "name": "JavaScript"
}
```

**Response:** 200 OK
```json
{
  "id": 2,
  "name": "JavaScript",
  "bookmarkCount": 8,
  "createdAt": "2025-08-01T10:30:00Z"
}
```

**Errors:**
- 400 Bad Request (validation errors)
- 404 Not Found
- 409 Conflict (duplicate tag name)

### DELETE /api/tags/{id}

**Purpose:** Delete tag and remove from all associated bookmarks  
**Parameters:** 
- `id`: Tag identifier (path parameter)

**Response:** 204 No Content

**Errors:** 404 Not Found

## Data Transfer Objects (DTOs)

### BookmarkCreateDto
```csharp
public class BookmarkCreateDto
{
    [Required]
    [Url]
    [MaxLength(2000)]
    public string Url { get; set; } = null!;
    
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = null!;
    
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    [MaxLength(200)]
    public string? SourceFolder { get; set; }
    
    public List<string> Tags { get; set; } = new();
}
```

### BookmarkUpdateDto
```csharp
public class BookmarkUpdateDto
{
    [Required]
    [Url]
    [MaxLength(2000)]
    public string Url { get; set; } = null!;
    
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = null!;
    
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    [MaxLength(200)]
    public string? SourceFolder { get; set; }
    
    public List<string> Tags { get; set; } = new();
}
```

### BookmarkResponseDto
```csharp
public class BookmarkResponseDto
{
    public int Id { get; set; }
    public string Url { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? SourceFolder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> Tags { get; set; } = new();
}
```

### TagCreateDto
```csharp
public class TagCreateDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = null!;
}
```

### TagResponseDto
```csharp
public class TagResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int BookmarkCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

## Error Handling

### Standard Error Response Format (RFC 7807)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "The URL field is required.",
  "instance": "/api/bookmarks",
  "errors": {
    "Url": ["The URL field is required."]
  }
}
```

### HTTP Status Code Usage
- **200 OK**: Successful GET, PUT operations
- **201 Created**: Successful POST operations (includes Location header)
- **204 No Content**: Successful DELETE operations
- **400 Bad Request**: Validation errors, malformed requests
- **404 Not Found**: Resource not found
- **409 Conflict**: Duplicate URL (bookmarks) or duplicate tag name
- **500 Internal Server Error**: Unexpected server errors

## Controller Implementation Notes

### BookmarksController Actions
- `GetBookmarks()`: List with pagination and filtering
- `GetBookmark(int id)`: Single bookmark retrieval
- `CreateBookmark(BookmarkCreateDto dto)`: Create with tag association
- `UpdateBookmark(int id, BookmarkUpdateDto dto)`: Full update including tags
- `DeleteBookmark(int id)`: Delete with cascade tag removal

### TagsController Actions  
- `GetTags()`: List with search and pagination
- `GetTag(int id)`: Single tag with bookmark count
- `CreateTag(TagCreateDto dto)`: Create with normalization
- `UpdateTag(int id, TagUpdateDto dto)`: Update with duplicate check
- `DeleteTag(int id)`: Delete with bookmark relationship cleanup

### Business Logic Integration
- URL normalization and validation
- Tag name normalization (trim, lowercase for comparison)
- Automatic timestamp management (CreatedAt, UpdatedAt)
- Soft delete consideration for future enhancement
- Batch operations consideration for browser extension sync