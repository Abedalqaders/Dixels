using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Migrations
{
    /// <inheritdoc />
    public partial class Move_EmployeeBuilding_To_User : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BuildingId",
                table: "AbpUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbpUsers_BuildingId",
                table: "AbpUsers",
                column: "BuildingId");

            // Carry existing assignments over before the table goes — hand-added; EF only
            // scaffolds the schema change and would otherwise drop them.
            migrationBuilder.Sql(
                """
                UPDATE "AbpUsers" AS u
                SET "BuildingId" = a."BuildingId"
                FROM "AppEmployeeBuildingAssignments" AS a
                WHERE a."EmployeeUserId" = u."Id";
                """);

            migrationBuilder.DropTable(
                name: "AppEmployeeBuildingAssignments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppEmployeeBuildingAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEmployeeBuildingAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEmployeeBuildingAssignments_AbpUsers_EmployeeUserId",
                        column: x => x.EmployeeUserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppEmployeeBuildingAssignments_AppBuildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "AppBuildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployeeBuildingAssignments_BuildingId",
                table: "AppEmployeeBuildingAssignments",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployeeBuildingAssignments_EmployeeUserId",
                table: "AppEmployeeBuildingAssignments",
                column: "EmployeeUserId",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "AppEmployeeBuildingAssignments"
                    ("Id", "EmployeeUserId", "BuildingId", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
                SELECT gen_random_uuid(), u."Id", u."BuildingId", '{}', replace(gen_random_uuid()::text, '-', ''), now()
                FROM "AbpUsers" AS u
                JOIN "AppBuildings" AS b ON b."Id" = u."BuildingId";
                """);

            migrationBuilder.DropIndex(
                name: "IX_AbpUsers_BuildingId",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "BuildingId",
                table: "AbpUsers");
        }
    }
}
