using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vsite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNavigationConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "navigation_config",
                columns: table => new
                {
                    website_id = table.Column<Guid>(type: "uuid", nullable: false),
                    items = table.Column<string>(type: "jsonb", nullable: false),
                    is_dirty = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_navigation_config", x => x.website_id);
                    table.ForeignKey(
                        name: "fk_navigation_config_website",
                        columns: x => new { x.website_id, x.shop_id },
                        principalTable: "website",
                        principalColumns: new[] { "id", "shop_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_navigation_config_website_id_shop_id",
                table: "navigation_config",
                columns: new[] { "website_id", "shop_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "navigation_config");
        }
    }
}
