using BookmarkFeeder.WebApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookmarkFeeder.WebApi.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>
    /// Owner of everything that existed before multi-user. FIXED and hard-coded: the migration
    /// inserts this exact Guid, SeedData assigns it, and UserProvisioningService claims it on the
    /// first login. Changing it orphans the pre-migration collection.
    /// </summary>
    public static readonly Guid PlaceholderUserId = Guid.Parse("00000000-0000-0000-0000-00000000f01d");

    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Auth0Sub).HasMaxLength(255);
        entity.Property(e => e.Email).HasMaxLength(320);        // RFC 5321 maximum
        entity.Property(e => e.DisplayName).HasMaxLength(200);

        // Unique but nullable. PostgreSQL allows many NULLs under a UNIQUE index, which is exactly
        // what the unclaimed placeholder needs — and once claimed there are none left.
        entity.HasIndex(e => e.Auth0Sub).IsUnique();

        // Deliberately NO "Owner" query filter: this is the identity table, and filtering it by the
        // current user would break the very lookup that establishes who the current user is.
    }
}
