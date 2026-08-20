using BookmarkFeeder.WebApi.Auth;
using BookmarkFeeder.WebApi.Data.Configurations;
using BookmarkFeeder.WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BookmarkFeeder.WebApi.Data;

public class BookmarkDbContext : DbContext
{
    private readonly Guid _ownerId;

    /// <summary>
    /// The owner is snapshotted at construction and drives the "Owner" global query filter on
    /// every tenant-owned entity.
    ///
    /// This is why Program.cs registers the factory with ServiceLifetime.Scoped: EF resolves extra
    /// constructor parameters from the provider the FACTORY was resolved from, and the default
    /// singleton factory holds the root provider, which cannot hand out a scoped service.
    ///
    /// It is also why AddPooledDbContextFactory must never be used here - pooled instances are
    /// reused across scopes and would carry another user's owner id, i.e. a cross-tenant read.
    /// </summary>
    public BookmarkDbContext(DbContextOptions<BookmarkDbContext> options, ICurrentUserContext currentUser)
        : base(options)
    {
        _ownerId = currentUser.UserId;
    }

    public DbSet<Bookmark> Bookmarks { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<BookmarkTag> BookmarkTags { get; set; }
    public DbSet<SavedSearch> SavedSearches { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Bookmark configuration
        modelBuilder.Entity<Bookmark>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Url).HasMaxLength(2048).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.FaviconUrl).HasMaxLength(2048);
            entity.Property(e => e.SourceFolder).HasMaxLength(1024);
            entity.Property(e => e.IsRead).HasDefaultValue(false);

            // Every index leads with OwnerId, because every list query now starts with
            // "WHERE OwnerId = @p" - an index not leading with it cannot serve that predicate.
            // Url uniqueness is PER OWNER: two people may each save the same URL.
            entity.HasIndex(e => new { e.OwnerId, e.Url }).IsUnique();
            entity.HasIndex(e => new { e.OwnerId, e.DateAdded });
            entity.HasIndex(e => new { e.OwnerId, e.SourceFolder });
            entity.HasIndex(e => new { e.OwnerId, e.IsRead });

            // NAMED filters (EF Core 10), not one &&-joined expression. The names are what let
            // individual call sites drop exactly one: the duplicate-URL pre-checks need to see
            // soft-deleted rows but must stay owner-scoped, and the favicon worker needs the
            // opposite. A single anonymous filter forces all-or-nothing and leaks across tenants.
            entity.HasQueryFilter("SoftDelete", e => !e.IsDeleted);
            entity.HasQueryFilter("Owner", e => e.OwnerId == _ownerId);

            entity.HasOne(e => e.Owner)
                  .WithMany(u => u.Bookmarks)
                  .HasForeignKey(e => e.OwnerId)
                  .OnDelete(DeleteBehavior.Cascade);

            // tsvector is a PostgreSQL type: the InMemory provider used by the unit tests cannot
            // map it and would fail model validation, so the column only exists on Npgsql. The
            // Testcontainers suite is what actually exercises it.
            if (Database.IsNpgsql())
            {
                // Weighted full-text vector, maintained by PostgreSQL. Written as explicit SQL
                // because Npgsql's HasGeneratedTsVectorColumn produces an UNWEIGHTED to_tsvector
                // over the columns and offers no way to setweight - and the weights are what make
                // a title hit outrank a url hit. Ranking depends on the A/B/C letters below.
                //
                // The Url is split into words first: to_tsvector's parser would otherwise treat a
                // URL as host/url_path tokens ('wolverine.netlify.app', '/docs'), so searching
                // "wolverine" would not match https://wolverine.netlify.app - a regression against
                // the ILIKE substring search this replaces. Punctuation -> spaces makes each
                // segment its own lexeme.
                entity.Property(e => e.SearchVector)
                      .HasColumnType("tsvector")
                      .HasComputedColumnSql(
                          """
                          setweight(to_tsvector('english', coalesce("Title", '')), 'A') ||
                          setweight(to_tsvector('english', coalesce("Description", '')), 'B') ||
                          setweight(to_tsvector('english', regexp_replace(coalesce("Url", ''), '[^a-zA-Z0-9]+', ' ', 'g')), 'C')
                          """,
                          stored: true);

                // Left single-column on purpose. Prefixing it with OwnerId needs the btree_gin
                // extension and raw SQL EF cannot represent in the model snapshot; at
                // personal-deployment scale the planner GIN-scans then filters by owner, which is
                // cheap. Revisit only with many users AND large collections.
                entity.HasIndex(e => e.SearchVector).HasMethod("gin");
            }
            else
            {
                entity.Ignore(e => e.SearchVector);
            }

            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Bookmarks)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Tag configuration
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.NormalizedName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Color).HasMaxLength(32);
            // Per owner: two users may both have a "dotnet" tag.
            entity.HasIndex(e => new { e.OwnerId, e.NormalizedName }).IsUnique();
            entity.HasQueryFilter("Owner", e => e.OwnerId == _ownerId);

            entity.HasOne(e => e.Owner)
                  .WithMany(u => u.Tags)
                  .HasForeignKey(e => e.OwnerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Category configuration
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasIndex(e => e.OwnerId);
            entity.HasQueryFilter("Owner", e => e.OwnerId == _ownerId);

            entity.HasOne(e => e.Owner)
                  .WithMany(u => u.Categories)
                  .HasForeignKey(e => e.OwnerId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ParentCategory)
                  .WithMany(e => e.SubCategories)
                  .HasForeignKey(e => e.ParentCategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // SavedSearch configuration
        modelBuilder.Entity<SavedSearch>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            // Opaque serialized param string; 2048 mirrors the url length limit.
            entity.Property(e => e.Query).HasMaxLength(2048).IsRequired();
            entity.HasIndex(e => new { e.OwnerId, e.DateCreated });
            entity.HasQueryFilter("Owner", e => e.OwnerId == _ownerId);

            entity.HasOne(e => e.Owner)
                  .WithMany(u => u.SavedSearches)
                  .HasForeignKey(e => e.OwnerId)
                  .OnDelete(DeleteBehavior.Cascade);
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
            // Match the Bookmark soft-delete filter so join rows for deleted bookmarks are hidden.
            entity.HasQueryFilter("SoftDelete", e => !e.Bookmark.IsDeleted);
            // Ownership travels through the navigation. Effectively free: the join to Bookmarks is
            // already generated by the SoftDelete filter above.
            entity.HasQueryFilter("Owner", e => e.Bookmark.OwnerId == _ownerId);
        });

        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new ApiKeyConfiguration());

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampOwner();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampOwner();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Query filters only read; they never populate a write. Stamping here means no create path
    /// has to remember OwnerId, and a forgotten one cannot silently produce an unreachable row.
    /// An OwnerId already set (the seeder, the key-minting service) is left alone.
    /// </summary>
    private void StampOwner()
    {
        if (_ownerId == Guid.Empty)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<IOwnedEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.OwnerId == Guid.Empty)
            {
                entry.Entity.OwnerId = _ownerId;
            }
        }
    }
}
