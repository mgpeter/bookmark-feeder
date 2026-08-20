using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookmarkFeeder.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersOwnershipAndApiKeys : Migration
    {
        /// <summary>The owner of everything that existed before multi-user. Must match UserConfiguration.</summary>
        private const string PlaceholderUserId = "00000000-0000-0000-0000-00000000f01d";

        private static readonly string[] OwnedTables = ["Bookmarks", "Tags", "Categories", "SavedSearches"];

        /// <summary>
        /// Hand-ordered. The scaffolded version added OwnerId as NOT NULL with a Guid.Empty default
        /// and wired the foreign keys immediately, which fails on any database that already holds
        /// rows: nothing owns them, and Guid.Empty matches no user. The order below adds the column
        /// nullable, creates an owner for the existing collection, backfills, and only then
        /// tightens the constraints.
        ///
        /// The generated SearchVector column is deliberately untouched - its expression references
        /// only Title/Description/Url, so adding OwnerId neither changes it nor forces a recompute
        /// of the whole table and its GIN index.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Identity tables.
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Auth0Sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            // Unique but nullable: PostgreSQL allows many NULLs under a UNIQUE index, which is what
            // lets the unclaimed placeholder below sit here until somebody signs in.
            migrationBuilder.CreateIndex(
                name: "IX_Users_Auth0Sub",
                table: "Users",
                column: "Auth0Sub",
                unique: true);

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKeys_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // THE uniqueness guarantee: one key value can never map to two owners.
            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_OwnerId",
                table: "ApiKeys",
                column: "OwnerId");

            // Non-unique: validation narrows by prefix, then verifies the hash.
            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_Prefix",
                table: "ApiKeys",
                column: "Prefix");

            // 2. OwnerId, nullable for now. Metadata-only on PostgreSQL 11+ because there is no
            //    default, so no table rewrite and no recompute of the stored SearchVector column.
            foreach (var table in OwnedTables)
            {
                migrationBuilder.AddColumn<Guid>(
                    name: "OwnerId",
                    table: table,
                    type: "uuid",
                    nullable: true);
            }

            // 3. An owner for everything that already exists. Auth0Sub NULL marks it unclaimed:
            //    the first person to sign in claims this row (UserProvisioningService) rather than
            //    getting a new empty account, so the existing collection is not orphaned.
            migrationBuilder.Sql(
                "INSERT INTO \"Users\" (\"Id\", \"Auth0Sub\", \"Email\", \"DisplayName\", \"CreatedAt\", \"LastSeenAt\") " +
                "VALUES ('" + PlaceholderUserId + "', NULL, NULL, 'Pre-multi-user collection', " +
                "NOW() AT TIME ZONE 'utc', NULL) ON CONFLICT (\"Id\") DO NOTHING;");

            // 4. Backfill.
            foreach (var table in OwnedTables)
            {
                migrationBuilder.Sql(
                    "UPDATE \"" + table + "\" SET \"OwnerId\" = '" + PlaceholderUserId + "' " +
                    "WHERE \"OwnerId\" IS NULL;");
            }

            // 5. Now it can be NOT NULL.
            foreach (var table in OwnedTables)
            {
                migrationBuilder.AlterColumn<Guid>(
                    name: "OwnerId",
                    table: table,
                    type: "uuid",
                    nullable: false,
                    oldClrType: typeof(Guid),
                    oldType: "uuid",
                    oldNullable: true);
            }

            // 6. Global uniqueness becomes per-owner. Dropped before the replacements are created:
            //    the old IX_Bookmarks_Url would reject a second user saving a URL the first has.
            migrationBuilder.DropIndex(name: "IX_Bookmarks_Url", table: "Bookmarks");
            migrationBuilder.DropIndex(name: "IX_Tags_NormalizedName", table: "Tags");

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_OwnerId_Url",
                table: "Bookmarks",
                columns: ["OwnerId", "Url"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_OwnerId_NormalizedName",
                table: "Tags",
                columns: ["OwnerId", "NormalizedName"],
                unique: true);

            // 7. Filter/sort indexes gain OwnerId as the LEADING column. Every list query now
            //    starts with "WHERE OwnerId = @p", and an index not leading with it cannot serve
            //    that predicate.
            migrationBuilder.DropIndex(name: "IX_Bookmarks_DateAdded", table: "Bookmarks");
            migrationBuilder.DropIndex(name: "IX_Bookmarks_IsRead", table: "Bookmarks");
            migrationBuilder.DropIndex(name: "IX_Bookmarks_SourceFolder", table: "Bookmarks");
            migrationBuilder.DropIndex(name: "IX_SavedSearches_DateCreated", table: "SavedSearches");

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_OwnerId_DateAdded",
                table: "Bookmarks",
                columns: ["OwnerId", "DateAdded"]);

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_OwnerId_IsRead",
                table: "Bookmarks",
                columns: ["OwnerId", "IsRead"]);

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_OwnerId_SourceFolder",
                table: "Bookmarks",
                columns: ["OwnerId", "SourceFolder"]);

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearches_OwnerId_DateCreated",
                table: "SavedSearches",
                columns: ["OwnerId", "DateCreated"]);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_OwnerId",
                table: "Categories",
                column: "OwnerId");

            // 8. Foreign keys last - safe only now that every OwnerId is non-null and points at a
            //    row that exists.
            foreach (var table in OwnedTables)
            {
                migrationBuilder.AddForeignKey(
                    name: "FK_" + table + "_Users_OwnerId",
                    table: table,
                    column: "OwnerId",
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            }
        }

        /// <summary>
        /// Reverses the schema. Note the owner assignment itself cannot be recovered - going down
        /// and up again re-backfills everything to the placeholder, collapsing any accounts that
        /// were created in between.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in OwnedTables)
            {
                migrationBuilder.DropForeignKey(name: "FK_" + table + "_Users_OwnerId", table: table);
            }

            migrationBuilder.DropTable(name: "ApiKeys");
            migrationBuilder.DropTable(name: "Users");

            migrationBuilder.DropIndex(name: "IX_Tags_OwnerId_NormalizedName", table: "Tags");
            migrationBuilder.DropIndex(name: "IX_SavedSearches_OwnerId_DateCreated", table: "SavedSearches");
            migrationBuilder.DropIndex(name: "IX_Categories_OwnerId", table: "Categories");
            migrationBuilder.DropIndex(name: "IX_Bookmarks_OwnerId_DateAdded", table: "Bookmarks");
            migrationBuilder.DropIndex(name: "IX_Bookmarks_OwnerId_IsRead", table: "Bookmarks");
            migrationBuilder.DropIndex(name: "IX_Bookmarks_OwnerId_SourceFolder", table: "Bookmarks");
            migrationBuilder.DropIndex(name: "IX_Bookmarks_OwnerId_Url", table: "Bookmarks");

            foreach (var table in OwnedTables)
            {
                migrationBuilder.DropColumn(name: "OwnerId", table: table);
            }

            migrationBuilder.CreateIndex(
                name: "IX_Tags_NormalizedName",
                table: "Tags",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearches_DateCreated",
                table: "SavedSearches",
                column: "DateCreated");

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_DateAdded",
                table: "Bookmarks",
                column: "DateAdded");

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_IsRead",
                table: "Bookmarks",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_SourceFolder",
                table: "Bookmarks",
                column: "SourceFolder");

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_Url",
                table: "Bookmarks",
                column: "Url",
                unique: true);
        }
    }
}
