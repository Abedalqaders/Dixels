using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Migrations
{
    /// <inheritdoc />
    public partial class Add_Hierarchy_Idempotency_Keys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "AppSpaces",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "AppFloors",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "AppBuildings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaces_CreatorId_IdempotencyKey",
                table: "AppSpaces",
                columns: new[] { "CreatorId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppFloors_CreatorId_IdempotencyKey",
                table: "AppFloors",
                columns: new[] { "CreatorId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppBuildings_CreatorId_IdempotencyKey",
                table: "AppBuildings",
                columns: new[] { "CreatorId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppSpaces_CreatorId_IdempotencyKey",
                table: "AppSpaces");

            migrationBuilder.DropIndex(
                name: "IX_AppFloors_CreatorId_IdempotencyKey",
                table: "AppFloors");

            migrationBuilder.DropIndex(
                name: "IX_AppBuildings_CreatorId_IdempotencyKey",
                table: "AppBuildings");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "AppSpaces");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "AppFloors");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "AppBuildings");
        }
    }
}
