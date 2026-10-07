using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Migrations
{
    /// <summary>
    /// A person's bookings between two times (calendar, own-clash check) were found through
    /// (UserId, StartsAt), which can only bound StartsAt from above, so every past booking was
    /// read. (UserId, EndsAt) seeks straight to the bookings that end after the window starts.
    /// A plain CREATE INDEX: about a quarter of a second per 400k bookings, writes wait meanwhile.
    /// </summary>
    public partial class Index_UserBookings_By_EndsAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppBookings_UserId_StartsAt",
                table: "AppBookings");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookings_UserId_EndsAt",
                table: "AppBookings",
                columns: new[] { "UserId", "EndsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppBookings_UserId_EndsAt",
                table: "AppBookings");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookings_UserId_StartsAt",
                table: "AppBookings",
                columns: new[] { "UserId", "StartsAt" });
        }
    }
}
