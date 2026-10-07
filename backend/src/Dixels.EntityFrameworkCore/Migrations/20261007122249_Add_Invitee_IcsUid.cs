using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Migrations
{
    /// <inheritdoc />
    public partial class Add_Invitee_IcsUid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IcsSequence",
                table: "AppBookingSeriesAttendees",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "IcsUid",
                table: "AppBookingSeriesAttendees",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "IcsSequence",
                table: "AppBookingAttendees",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "IcsUid",
                table: "AppBookingAttendees",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Guests saved before this get their own random UID too (43 hex characters from two
            // random UUIDs, as long as the app's own), before the unique index goes on.
            foreach (var table in new[] { "AppBookingAttendees", "AppBookingSeriesAttendees" })
            {
                migrationBuilder.Sql(
                    $"UPDATE \"{table}\" SET \"IcsUid\" = left(replace(gen_random_uuid()::text || gen_random_uuid()::text, '-', ''), 43) || '@dixels';");
            }

            migrationBuilder.CreateIndex(
                name: "IX_AppBookingSeriesAttendees_IcsUid",
                table: "AppBookingSeriesAttendees",
                column: "IcsUid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppBookingAttendees_IcsUid",
                table: "AppBookingAttendees",
                column: "IcsUid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppBookingSeriesAttendees_IcsUid",
                table: "AppBookingSeriesAttendees");

            migrationBuilder.DropIndex(
                name: "IX_AppBookingAttendees_IcsUid",
                table: "AppBookingAttendees");

            migrationBuilder.DropColumn(
                name: "IcsSequence",
                table: "AppBookingSeriesAttendees");

            migrationBuilder.DropColumn(
                name: "IcsUid",
                table: "AppBookingSeriesAttendees");

            migrationBuilder.DropColumn(
                name: "IcsSequence",
                table: "AppBookingAttendees");

            migrationBuilder.DropColumn(
                name: "IcsUid",
                table: "AppBookingAttendees");
        }
    }
}
