# Database Schema

This is the database schema implementation for the spec detailed in @docs/specs/2025-08-15-database-infrastructure/spec.md

## Entity Definitions

### Bookmark Entity

```csharp
public class Bookmark
{
    public Guid Id { get; set; }
    public string Url { get; set; } // max 2048, required, unique
    public string Title { get; set; } // max 500, required
    public string? Description { get; set; } // max 2000, nullable
    public DateTime DateAdded { get; set; }
    public DateTime DateModified { get; set; }
    public bool IsDeleted { get; set; } // soft delete
    
    // Navigation properties
    public ICollection<BookmarkTag> BookmarkTags { get; set; } = new List<BookmarkTag>();
    public ICollection<Tag> Tags => BookmarkTags.Select(bt => bt.Tag).ToList();
}
```

### Tag Entity

```csharp
public class Tag
{
    public Guid Id { get; set; }
    public string Name { get; set; } // max 100, required
    public string NormalizedName { get; set; } // computed, max 100
    public DateTime DateCreated { get; set; }
    
    // Navigation properties
    public ICollection<BookmarkTag> BookmarkTags { get; set; } = new List<BookmarkTag>();
    public ICollection<Bookmark> Bookmarks => BookmarkTags.Select(bt => bt.Bookmark).ToList();
}
```

### Category Entity

```csharp
public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } // max 200, required
    public Guid? ParentCategoryId { get; set; } // self-referencing FK
    public string? Description { get; set; } // max 1000, nullable
    public DateTime DateCreated { get; set; }
    
    // Navigation properties
    public Category? ParentCategory { get; set; }
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
}
```

### BookmarkTag Entity (Join Table)

```csharp
public class BookmarkTag
{
    public Guid BookmarkId { get; set; }
    public Guid TagId { get; set; }
    public DateTime DateAssigned { get; set; }
    
    // Navigation properties
    public Bookmark Bookmark { get; set; }
    public Tag Tag { get; set; }
}
```

## EF Core Configuration

### DbContext Configuration

```csharp
public class BookmarkDbContext : DbContext
{
    public BookmarkDbContext(DbContextOptions<BookmarkDbContext> options) : base(options) { }
    
    public DbSet<Bookmark> Bookmarks { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<BookmarkTag> BookmarkTags { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Bookmark configuration
        modelBuilder.Entity<Bookmark>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Url).HasMaxLength(2048).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.HasIndex(e => e.Url).IsUnique();
            entity.HasIndex(e => e.DateAdded);
            entity.HasQueryFilter(e => !e.IsDeleted); // Global query filter for soft delete
        });
        
        // Tag configuration
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.NormalizedName).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.NormalizedName).IsUnique();
        });
        
        // Category configuration
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasOne(e => e.ParentCategory)
                  .WithMany(e => e.SubCategories)
                  .HasForeignKey(e => e.ParentCategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
        
        // BookmarkTag configuration (many-to-many)
        modelBuilder.Entity<BookmarkTag>(entity =>
        {
            entity.HasKey(e => new { e.BookmarkId, e.TagId });
            entity.HasOne(e => e.Bookmark)
                  .WithMany(e => e.BookmarkTags)
                  .HasForeignKey(e => e.BookmarkId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Tag)
                  .WithMany(e => e.BookmarkTags)
                  .HasForeignKey(e => e.TagId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.DateAssigned);
        });
    }
}
```

## Migration Scripts

### Initial Migration SQL (Generated)

```sql
-- Create Tags table
CREATE TABLE "Tags" (
    "Id" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "NormalizedName" character varying(100) NOT NULL,
    "DateCreated" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Tags" PRIMARY KEY ("Id")
);

-- Create Categories table
CREATE TABLE "Categories" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "ParentCategoryId" uuid,
    "Description" character varying(1000),
    "DateCreated" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Categories" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Categories_Categories_ParentCategoryId" 
        FOREIGN KEY ("ParentCategoryId") REFERENCES "Categories" ("Id") ON DELETE RESTRICT
);

-- Create Bookmarks table
CREATE TABLE "Bookmarks" (
    "Id" uuid NOT NULL,
    "Url" character varying(2048) NOT NULL,
    "Title" character varying(500) NOT NULL,
    "Description" character varying(2000),
    "DateAdded" timestamp with time zone NOT NULL,
    "DateModified" timestamp with time zone NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_Bookmarks" PRIMARY KEY ("Id")
);

-- Create BookmarkTags join table
CREATE TABLE "BookmarkTags" (
    "BookmarkId" uuid NOT NULL,
    "TagId" uuid NOT NULL,
    "DateAssigned" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_BookmarkTags" PRIMARY KEY ("BookmarkId", "TagId"),
    CONSTRAINT "FK_BookmarkTags_Bookmarks_BookmarkId" 
        FOREIGN KEY ("BookmarkId") REFERENCES "Bookmarks" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_BookmarkTags_Tags_TagId" 
        FOREIGN KEY ("TagId") REFERENCES "Tags" ("TagId") ON DELETE CASCADE
);

-- Create indexes
CREATE UNIQUE INDEX "IX_Bookmarks_Url" ON "Bookmarks" ("Url");
CREATE INDEX "IX_Bookmarks_DateAdded" ON "Bookmarks" ("DateAdded");
CREATE UNIQUE INDEX "IX_Tags_NormalizedName" ON "Tags" ("NormalizedName");
CREATE INDEX "IX_BookmarkTags_DateAssigned" ON "BookmarkTags" ("DateAssigned");
CREATE INDEX "IX_Categories_ParentCategoryId" ON "Categories" ("ParentCategoryId");
```

## Seed Data Configuration

### Development Seed Data

```csharp
public static class SeedData
{
    public static void SeedDevelopmentData(BookmarkDbContext context)
    {
        if (context.Bookmarks.Any()) return; // Already seeded
        
        // Seed Categories
        var techCategory = new Category { Id = Guid.NewGuid(), Name = "Technology", DateCreated = DateTime.UtcNow };
        var newsCategory = new Category { Id = Guid.NewGuid(), Name = "News", DateCreated = DateTime.UtcNow };
        
        // Seed Tags
        var csharpTag = new Tag { Id = Guid.NewGuid(), Name = "C#", NormalizedName = "c#", DateCreated = DateTime.UtcNow };
        var dotnetTag = new Tag { Id = Guid.NewGuid(), Name = ".NET", NormalizedName = ".net", DateCreated = DateTime.UtcNow };
        
        // Seed Bookmarks with relationships
        var bookmark1 = new Bookmark 
        { 
            Id = Guid.NewGuid(), 
            Url = "https://docs.microsoft.com/dotnet", 
            Title = ".NET Documentation",
            Description = "Official Microsoft .NET documentation",
            DateAdded = DateTime.UtcNow,
            DateModified = DateTime.UtcNow
        };
        
        context.Categories.AddRange(techCategory, newsCategory);
        context.Tags.AddRange(csharpTag, dotnetTag);
        context.Bookmarks.Add(bookmark1);
        context.BookmarkTags.Add(new BookmarkTag 
        { 
            BookmarkId = bookmark1.Id, 
            TagId = dotnetTag.Id, 
            DateAssigned = DateTime.UtcNow 
        });
        
        context.SaveChanges();
    }
}
```

## Rationale

### Entity Design Decisions

- **Guid Primary Keys**: Ensures globally unique identifiers, better for distributed systems and security
- **Soft Delete on Bookmarks**: Preserves data integrity and enables audit trails without breaking foreign key relationships  
- **URL Uniqueness**: Prevents duplicate bookmarks while allowing title/description updates
- **Tag Normalization**: NormalizedName field enables case-insensitive searching and prevents duplicate tags with different casing
- **Self-Referencing Categories**: Supports hierarchical organization without complex tree structures

### Performance Considerations

- **Composite Primary Key on BookmarkTag**: Optimizes many-to-many relationship queries
- **Strategic Indexing**: Indexes on frequently queried columns (URL, dates, normalized names)
- **Query Filters**: Global soft delete filter reduces query complexity in application code
- **Navigation Properties**: Configured for efficient loading strategies with EF Core