using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Migrations
{
    /// <inheritdoc />
    public partial class SpaceTypeNamesToTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSpaceTypeTranslations",
                columns: table => new
                {
                    SpaceTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSpaceTypeTranslations", x => new { x.SpaceTypeId, x.Language });
                    table.ForeignKey(
                        name: "FK_AppSpaceTypeTranslations_AppSpaceTypes_SpaceTypeId",
                        column: x => x.SpaceTypeId,
                        principalTable: "AppSpaceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Every existing name becomes the type's English row (trimmed, like new names), with
            // the type's deleted flag copied. The old index was case-sensitive, so two live types
            // could be "Desk" and "desk" — the same name now: the later one gets " (2)" rather
            // than the new unique index failing the deploy.
            migrationBuilder.Sql(
                """
                INSERT INTO "AppSpaceTypeTranslations" ("SpaceTypeId", "Language", "Name", "NormalizedName", "IsDeleted")
                SELECT "Id", 'en', "NewName", upper("NewName"), "IsDeleted"
                FROM (
                    SELECT "Id", "IsDeleted",
                        CASE WHEN "IsDeleted" OR "Rank" = 1 THEN "Trimmed"
                             ELSE left("Trimmed", 120) || ' (' || "Rank" || ')' END AS "NewName"
                    FROM (
                        SELECT "Id", "IsDeleted", btrim("Name") AS "Trimmed",
                            row_number() OVER (PARTITION BY upper(btrim("Name")), "IsDeleted" ORDER BY "CreationTime", "Id") AS "Rank"
                        FROM "AppSpaceTypes"
                    ) AS ranked
                ) AS named;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaceTypeTranslations_Language_NormalizedName",
                table: "AppSpaceTypeTranslations",
                columns: new[] { "Language", "NormalizedName" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.DropIndex(
                name: "IX_AppSpaceTypes_Name",
                table: "AppSpaceTypes");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "AppSpaceTypes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "AppSpaceTypes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            // Back to one name per type: the English one (always there — it's required). Names
            // in other languages are lost.
            migrationBuilder.Sql(
                """
                UPDATE "AppSpaceTypes" AS t SET "Name" = n."Name"
                FROM "AppSpaceTypeTranslations" AS n
                WHERE n."SpaceTypeId" = t."Id" AND n."Language" = 'en';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaceTypes_Name",
                table: "AppSpaceTypes",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.DropTable(
                name: "AppSpaceTypeTranslations");
        }
    }
}
