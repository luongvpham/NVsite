using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vsite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaAssetOrphanedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "orphaned_at",
                table: "media_asset",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_asset_orphaned",
                table: "media_asset",
                columns: new[] { "shop_id", "orphaned_at" },
                filter: "orphaned_at IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_media_asset_orphaned",
                table: "media_asset");

            migrationBuilder.DropColumn(
                name: "orphaned_at",
                table: "media_asset");
        }
    }
}
