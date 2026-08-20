using BookmarkFeeder.WebApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookmarkFeeder.WebApi.Data.Configurations;

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Prefix).HasMaxLength(16).IsRequired();
        entity.Property(e => e.KeyHash).HasMaxLength(64).IsRequired();   // SHA-256, hex

        // Non-unique: validation narrows by prefix, then verifies the hash. Collisions are rare
        // but legal, so the lookup iterates candidates.
        entity.HasIndex(e => e.Prefix);

        // THE uniqueness guarantee. One key value can never map to two owners.
        entity.HasIndex(e => e.KeyHash).IsUnique();

        entity.HasIndex(e => e.OwnerId);

        entity.HasOne(e => e.Owner)
              .WithMany(u => u.ApiKeys)
              .HasForeignKey(e => e.OwnerId)
              .OnDelete(DeleteBehavior.Cascade);

        // No "Owner" query filter: the authentication handler must find a key BEFORE the current
        // user exists. The /api/keys endpoints scope by owner explicitly instead.
    }
}
