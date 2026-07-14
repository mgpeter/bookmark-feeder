using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace BookmarkFeeder.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class AddBookmarkSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Bookmarks",
                type: "tsvector",
                nullable: false,
                computedColumnSql: "setweight(to_tsvector('english', coalesce(\"Title\", '')), 'A') ||\r\nsetweight(to_tsvector('english', coalesce(\"Description\", '')), 'B') ||\r\nsetweight(to_tsvector('english', regexp_replace(coalesce(\"Url\", ''), '[^a-zA-Z0-9]+', ' ', 'g')), 'C')",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_SearchVector",
                table: "Bookmarks",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookmarks_SearchVector",
                table: "Bookmarks");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Bookmarks");
        }
    }
}
