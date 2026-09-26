using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vsite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LogoId",
                table: "Shop",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MediaAsset",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    AltText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FocalPointX = table.Column<float>(type: "real", nullable: false),
                    FocalPointY = table.Column<float>(type: "real", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Folder = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsInLibrary = table.Column<bool>(type: "boolean", nullable: false),
                    Preset = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SourceAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAsset", x => x.Id);
                    table.UniqueConstraint("AK_MediaAsset_Id_ShopId", x => new { x.Id, x.ShopId });
                    table.CheckConstraint("ck_media_library_preset", "(\"IsInLibrary\" = true AND \"Preset\" IS NULL) OR (\"IsInLibrary\" = false AND \"Preset\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_MediaAsset_MediaAsset_SourceAssetId",
                        column: x => x.SourceAssetId,
                        principalTable: "MediaAsset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MediaAsset_Shop_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shop",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Shop_LogoId_Id",
                table: "Shop",
                columns: new[] { "LogoId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAsset_StorageKey",
                table: "MediaAsset",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_derivative",
                table: "MediaAsset",
                columns: new[] { "SourceAssetId", "Preset" },
                filter: "\"SourceAssetId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_media_library",
                table: "MediaAsset",
                columns: new[] { "ShopId", "CreatedAt" },
                descending: new[] { false, true },
                filter: "\"IsInLibrary\" AND NOT \"IsDeleted\"");

            migrationBuilder.AddForeignKey(
                name: "FK_Shop_MediaAsset_LogoId_Id",
                table: "Shop",
                columns: new[] { "LogoId", "Id" },
                principalTable: "MediaAsset",
                principalColumns: new[] { "Id", "ShopId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shop_MediaAsset_LogoId_Id",
                table: "Shop");

            migrationBuilder.DropTable(
                name: "MediaAsset");

            migrationBuilder.DropIndex(
                name: "IX_Shop_LogoId_Id",
                table: "Shop");

            migrationBuilder.DropColumn(
                name: "LogoId",
                table: "Shop");
        }
    }
}
