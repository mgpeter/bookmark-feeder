using BookmarkFeeder.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookmarkFeeder.Data.Configuration;

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