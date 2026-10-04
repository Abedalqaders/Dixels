using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Migrations
{
    /// <inheritdoc />
    public partial class HierarchyNamesToTranslations : Migration
    {
        // (entity table, translation table, key column)
        private static readonly (string Entities, string Translations, string Key)[] Named =
        [
            ("AppBuildings", "AppBuildingTranslations", "BuildingId"),
            ("AppFloors", "AppFloorTranslations", "FloorId"),
            ("AppSpaces", "AppSpaceTranslations", "SpaceId"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (entities, translations, key) in Named)
            {
                migrationBuilder.CreateTable(
                    name: translations,
                    columns: table => new
                    {
                        Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                        Id = table.Column<Guid>(type: "uuid", nullable: false, name: key),
                        Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                        NormalizedName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey($"PK_{translations}", x => new { x.Id, x.Language });
                        table.ForeignKey(
                            name: $"FK_{translations}_{entities}_{key}",
                            column: x => x.Id,
                            principalTable: entities,
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Cascade);
                    });

                // Every existing name becomes its English row (trimmed, like new names) —
                // deleted rows included, so a restored building, floor or space keeps its name.
                migrationBuilder.Sql(
                    $"""
                    INSERT INTO "{translations}" ("Language", "{key}", "Name", "NormalizedName")
                    SELECT 'en', "Id", btrim("Name"), upper(btrim("Name"))
                    FROM "{entities}";
                    """);

                migrationBuilder.DropColumn(
                    name: "Name",
                    table: entities);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (entities, translations, key) in Named)
            {
                migrationBuilder.AddColumn<string>(
                    name: "Name",
                    table: entities,
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false,
                    defaultValue: "");

                // Back to one name each: the English one (always there — it's required).
                // Names in other languages are lost.
                migrationBuilder.Sql(
                    $"""
                    UPDATE "{entities}" AS e SET "Name" = t."Name"
                    FROM "{translations}" AS t
                    WHERE t."{key}" = e."Id" AND t."Language" = 'en';
                    """);

                migrationBuilder.DropTable(
                    name: translations);
            }
        }
    }
}
