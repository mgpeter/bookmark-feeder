# Database Schema

This is the database schema implementation for the spec detailed in @docs/specs/2025-08-01-api-endpoints-backend-setup/spec.md

## Entity Models

### Bookmark Entity
```csharp
public class Bookmark
{
    public int Id { get; set; }
    public string Url { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? SourceFolder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public ICollection<BookmarkTag> BookmarkTags { get; set; } = new List<BookmarkTag>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
```

### Tag Entity
```csharp
public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string NormalizedName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public ICollection<BookmarkTag> BookmarkTags { get; set; } = new List<BookmarkTag>();
    public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
}
```

### BookmarkTag Join Entity
```csharp
public class BookmarkTag
{
    public int BookmarkId { get; set; }
    public int TagId { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public Bookmark Bookmark { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
```

## Entity Configurations

### BookmarkConfiguration
```csharp
public class BookmarkConfiguration : IEntityTypeConfiguration<Bookmark>
{
    public void Configure(EntityTypeBuilder<Bookmark> builder)
    {
        builder.HasKey(b => b.Id);
        
        builder.Property(b => b.Url)
            .IsRequired()
            .HasMaxLength(2000);
            
        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(500);
            
        builder.Property(b => b.Description)
            .HasMaxLength(2000);
            
        builder.Property(b => b.SourceFolder)
            .HasMaxLength(200);
            
        builder.Property(b => b.CreatedAt)
            .IsRequired();
            
        builder.Property(b => b.UpdatedAt)
            .IsRequired();
        
        // Indexes
        builder.HasIndex(b => b.Url)
            .IsUnique()
            .HasDatabaseName("IX_Bookmarks_Url");
            
        builder.HasIndex(b => b.CreatedAt)
            .HasDatabaseName("IX_Bookmarks_CreatedAt");
            
        builder.HasIndex(b => b.SourceFolder)
            .HasDatabaseName("IX_Bookmarks_SourceFolder");
        
        // Many-to-many relationship
        builder.HasMany(b => b.Tags)
            .WithMany(t => t.Bookmarks)
            .UsingEntity<BookmarkTag>(
                j => j.HasOne(bt => bt.Tag)
                      .WithMany(t => t.BookmarkTags)
                      .HasForeignKey(bt => bt.TagId),
                j => j.HasOne(bt => bt.Bookmark)
                      .WithMany(b => b.BookmarkTags)
                      .HasForeignKey(bt => bt.BookmarkId),
                j => {
                    j.HasKey(bt => new { bt.BookmarkId, bt.TagId });
                    j.Property(bt => bt.CreatedAt).IsRequired();
                    j.ToTable("BookmarkTags");
                });
    }
}
```

### TagConfiguration
```csharp
public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(t => t.Id);
        
        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(t => t.NormalizedName)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(t => t.CreatedAt)
            .IsRequired();
        
        // Indexes
        builder.HasIndex(t => t.NormalizedName)
            .IsUnique()
            .HasDatabaseName("IX_Tags_NormalizedName");
    }
}
```

## Database Migrations

### Initial Migration Commands
```bash
# Add initial migration
dotnet ef migrations add InitialCreate --project BookmarkFeeder.Data --startup-project BookmarkFeeder.AppHost

# Update database (handled automatically by Aspire in development)
dotnet ef database update --project BookmarkFeeder.Data --startup-project BookmarkFeeder.AppHost
```

### Migration SQL Preview
```sql
-- Bookmarks table
CREATE TABLE "Bookmarks" (
    "Id" serial NOT NULL,
    "Url" character varying(2000) NOT NULL,
    "Title" character varying(500) NOT NULL,
    "Description" character varying(2000),
    "SourceFolder" character varying(200),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Bookmarks" PRIMARY KEY ("Id")
);

-- Tags table
CREATE TABLE "Tags" (
    "Id" serial NOT NULL,
    "Name" character varying(100) NOT NULL,
    "NormalizedName" character varying(100) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Tags" PRIMARY KEY ("Id")
);

-- BookmarkTags junction table
CREATE TABLE "BookmarkTags" (
    "BookmarkId" integer NOT NULL,
    "TagId" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_BookmarkTags" PRIMARY KEY ("BookmarkId", "TagId"),
    CONSTRAINT "FK_BookmarkTags_Bookmarks_BookmarkId" FOREIGN KEY ("BookmarkId") REFERENCES "Bookmarks" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_BookmarkTags_Tags_TagId" FOREIGN KEY ("TagId") REFERENCES "Tags" ("Id") ON DELETE CASCADE
);

-- Indexes
CREATE UNIQUE INDEX "IX_Bookmarks_Url" ON "Bookmarks" ("Url");
CREATE INDEX "IX_Bookmarks_CreatedAt" ON "Bookmarks" ("CreatedAt");
CREATE INDEX "IX_Bookmarks_SourceFolder" ON "Bookmarks" ("SourceFolder");
CREATE UNIQUE INDEX "IX_Tags_NormalizedName" ON "Tags" ("NormalizedName");
CREATE INDEX "IX_BookmarkTags_TagId" ON "BookmarkTags" ("TagId");
```

## Aspire PostgreSQL Configuration

### AppHost Configuration
```csharp
var postgres = builder.AddPostgreSQL("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var database = postgres.AddDatabase("BookmarkFeederDb");

var api = builder.AddProject<Projects.BookmarkFeeder_WebApi>("webapi")
    .WithReference(database);
```

### Connection String Management
- Connection string automatically provided by Aspire service discovery
- Development: `Host=localhost;Port=5432;Database=BookmarkFeederDb;Username=postgres;Password=postgres`
- Container networking handled automatically by Aspire
- Health checks configured for database connectivity

## Data Integrity and Performance

### Constraints and Validation
- URL uniqueness enforced at database level
- Tag name normalization prevents case-sensitive duplicates
- Foreign key constraints ensure referential integrity
- NOT NULL constraints on required fields

### Performance Optimizations
- Clustered index on primary keys (Id columns)
- Non-clustered index on Bookmark.Url for duplicate detection
- Index on Tag.NormalizedName for case-insensitive searches
- Index on Bookmark.CreatedAt for chronological queries
- Composite primary key on BookmarkTags for optimal join performance

### Rationale
This schema design prioritizes data integrity, performance, and scalability while maintaining simplicity for the MVP. The many-to-many relationship through BookmarkTag allows flexible tagging without data duplication, and the normalized tag names enable case-insensitive matching while preserving the original user input.