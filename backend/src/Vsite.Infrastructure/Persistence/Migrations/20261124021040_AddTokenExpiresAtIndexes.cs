using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vsite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenExpiresAtIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_expires_at",
                table: "refresh_token",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_token_expires_at",
                table: "password_reset_token",
                column: "expires_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_refresh_token_expires_at",
                table: "refresh_token");

            migrationBuilder.DropIndex(
                name: "ix_password_reset_token_expires_at",
                table: "password_reset_token");
        }
    }
}
