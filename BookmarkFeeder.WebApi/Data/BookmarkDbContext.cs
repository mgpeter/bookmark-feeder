using Microsoft.EntityFrameworkCore;
using BookmarkFeeder.WebApi.Models;

namespace BookmarkFeeder.WebApi.Data;

public class BookmarkDbContext : DbContext
{
    public BookmarkDbContext(DbContextOptions<BookmarkDbContext> options) : base(options)
    {
    }

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
            entity.Property(e => e.FaviconUrl).HasMaxLength(2048);
            entity.Property(e => e.SourceFolder).HasMaxLength(1024);
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.HasIndex(e => e.Url).IsUnique();
            entity.HasIndex(e => e.DateAdded);
            entity.HasIndex(e => e.SourceFolder);
            entity.HasIndex(e => e.IsRead);
            entity.HasQueryFilter(e => !e.IsDeleted); // Global query filter for soft delete

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
            // Match the Bookmark soft-delete filter so join rows for deleted bookmarks are hidden.
            entity.HasQueryFilter(e => !e.Bookmark.IsDeleted);
        });

        base.OnModelCreating(modelBuilder);
    }
}