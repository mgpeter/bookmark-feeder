using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookmarkFeeder.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class AddFaviconFetchedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FaviconFetchedAt",
                table: "Bookmarks",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FaviconFetchedAt",
                table: "Bookmarks");
        }
    }
}
