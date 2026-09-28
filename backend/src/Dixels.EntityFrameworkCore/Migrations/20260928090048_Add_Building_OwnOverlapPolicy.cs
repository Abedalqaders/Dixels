using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Migrations
{
    /// <inheritdoc />
    public partial class Add_Building_OwnOverlapPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnOverlapPolicy",
                table: "AppBuildings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                // Existing buildings start on the same default as new ones: allowed, with a heads-up.
                defaultValue: "Warn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnOverlapPolicy",
                table: "AppBuildings");
        }
    }
}
