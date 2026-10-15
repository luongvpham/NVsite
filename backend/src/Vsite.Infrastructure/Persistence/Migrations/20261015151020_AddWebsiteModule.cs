using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vsite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWebsiteModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "website",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    default_locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    default_seo_title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    default_seo_description = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    default_og_image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    favicon_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tracking_snippets = table.Column<string>(type: "jsonb", nullable: false),
                    locales = table.Column<List<string>>(type: "text[]", nullable: false),
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
                    table.PrimaryKey("pk_website", x => x.id);
                    table.UniqueConstraint("ak_website_id_shop_id", x => new { x.id, x.shop_id });
                    table.CheckConstraint("ck_website_default_locale", "default_locale = ANY(locales)");
                    table.ForeignKey(
                        name: "fk_website_shop",
                        column: x => x.shop_id,
                        principalTable: "shop",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "page",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    website_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    system_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    seo_title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    seo_description = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    og_image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    no_index = table.Column<bool>(type: "boolean", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_page", x => x.id);
                    table.UniqueConstraint("ak_page_id_shop_id", x => new { x.id, x.shop_id });
                    table.CheckConstraint("ck_page_empty_slug_composable", "slug IS DISTINCT FROM '' OR kind = 'Composable'");
                    table.CheckConstraint("ck_page_kind_system_type", "(kind = 'System') = (system_type IS NOT NULL)");
                    table.CheckConstraint("ck_page_slug_not_found", "(slug IS NULL) = (system_type IS NOT DISTINCT FROM 'NotFound')");
                    table.ForeignKey(
                        name: "fk_page_website",
                        columns: x => new { x.website_id, x.shop_id },
                        principalTable: "website",
                        principalColumns: new[] { "id", "shop_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "theme",
                columns: table => new
                {
                    website_id = table.Column<Guid>(type: "uuid", nullable: false),
                    preset_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tokens = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("pk_theme", x => x.website_id);
                    table.ForeignKey(
                        name: "fk_theme_website",
                        columns: x => new { x.website_id, x.shop_id },
                        principalTable: "website",
                        principalColumns: new[] { "id", "shop_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "page_draft",
                columns: table => new
                {
                    page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tree = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("pk_page_draft", x => x.page_id);
                    table.ForeignKey(
                        name: "fk_page_draft_page",
                        columns: x => new { x.page_id, x.shop_id },
                        principalTable: "page",
                        principalColumns: new[] { "id", "shop_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            // SỬA TAY (T3, WEBSITE-001, #92/#76): EF Core/Npgsql không sinh được `ON DELETE SET NULL (cột)`
            // (Postgres 15+) — DeleteBehavior.SetNull sẽ null cả shop_id NOT NULL. Ba FK ghép tới
            // media_asset (id, shop_id) được thêm bằng SQL thô, chỉ null đúng cột ảnh. Model/snapshot khai
            // Restrict cho cùng FK (cùng tên) nên migration sau không thấy drift. ĐỪNG sinh lại migration mà
            // làm mất đoạn này.
            migrationBuilder.Sql(
                "ALTER TABLE website ADD CONSTRAINT fk_website_media_asset_default_og_image " +
                "FOREIGN KEY (default_og_image_id, shop_id) REFERENCES media_asset (id, shop_id) " +
                "ON DELETE SET NULL (default_og_image_id);");

            migrationBuilder.Sql(
                "ALTER TABLE website ADD CONSTRAINT fk_website_media_asset_favicon " +
                "FOREIGN KEY (favicon_asset_id, shop_id) REFERENCES media_asset (id, shop_id) " +
                "ON DELETE SET NULL (favicon_asset_id);");

            migrationBuilder.Sql(
                "ALTER TABLE page ADD CONSTRAINT fk_page_media_asset_og_image " +
                "FOREIGN KEY (og_image_id, shop_id) REFERENCES media_asset (id, shop_id) " +
                "ON DELETE SET NULL (og_image_id);");

            migrationBuilder.CreateIndex(
                name: "ix_page_og_image_id_shop_id",
                table: "page",
                columns: new[] { "og_image_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_page_website_id_shop_id",
                table: "page",
                columns: new[] { "website_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ux_page_slug",
                table: "page",
                columns: new[] { "website_id", "slug" },
                unique: true,
                filter: "NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "ux_page_system_type",
                table: "page",
                columns: new[] { "website_id", "system_type" },
                unique: true,
                filter: "system_type IS NOT NULL AND NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_page_draft_page_id_shop_id",
                table: "page_draft",
                columns: new[] { "page_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_theme_website_id_shop_id",
                table: "theme",
                columns: new[] { "website_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_website_default_og_image_id_shop_id",
                table: "website",
                columns: new[] { "default_og_image_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_website_favicon_asset_id_shop_id",
                table: "website",
                columns: new[] { "favicon_asset_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ux_website_shop",
                table: "website",
                column: "shop_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "page_draft");

            migrationBuilder.DropTable(
                name: "theme");

            migrationBuilder.DropTable(
                name: "page");

            migrationBuilder.DropTable(
                name: "website");
        }
    }
}
