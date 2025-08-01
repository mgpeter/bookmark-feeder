using BookmarkFeeder.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookmarkFeeder.Data.Configuration;

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