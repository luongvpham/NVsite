using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vsite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSitePublication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "current_publication_id",
                table: "website",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "site_publication",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    website_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_publication", x => x.id);
                    table.UniqueConstraint("ak_site_publication_id_website_id", x => new { x.id, x.website_id });
                    table.CheckConstraint("ck_site_publication_version_positive", "version > 0");
                    table.ForeignKey(
                        name: "fk_site_publication_app_user_published_by_user_id",
                        column: x => x.published_by_user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_site_publication_website",
                        columns: x => new { x.website_id, x.shop_id },
                        principalTable: "website",
                        principalColumns: new[] { "id", "shop_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_website_current_publication_id_id",
                table: "website",
                columns: new[] { "current_publication_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_site_publication_published_by_user_id",
                table: "site_publication",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_site_publication_website_id_shop_id",
                table: "site_publication",
                columns: new[] { "website_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ux_site_publication_website_version",
                table: "site_publication",
                columns: new[] { "website_id", "version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_website_current_publication",
                table: "website",
                columns: new[] { "current_publication_id", "id" },
                principalTable: "site_publication",
                principalColumns: new[] { "id", "website_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_website_current_publication",
                table: "website");

            migrationBuilder.DropTable(
                name: "site_publication");

            migrationBuilder.DropIndex(
                name: "ix_website_current_publication_id_id",
                table: "website");

            migrationBuilder.DropColumn(
                name: "current_publication_id",
                table: "website");
        }
    }
}
