using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Migrations
{
    /// <inheritdoc />
    public partial class Add_External_Guest_Indexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AppBookingSeriesAttendees_External",
                table: "AppBookingSeriesAttendees",
                column: "SeriesId",
                filter: "\"UserId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookingAttendees_External",
                table: "AppBookingAttendees",
                column: "EndsAt",
                filter: "\"UserId\" IS NULL")
                .Annotation("Npgsql:IndexInclude", new[] { "BookingId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppBookingSeriesAttendees_External",
                table: "AppBookingSeriesAttendees");

            migrationBuilder.DropIndex(
                name: "IX_AppBookingAttendees_External",
                table: "AppBookingAttendees");
        }
    }
}
