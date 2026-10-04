using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Migrations
{
    /// <inheritdoc />
    public partial class BookingReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReminderSentAt",
                table: "AppBookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppBookings_ReminderDue",
                table: "AppBookings",
                column: "StartsAt",
                filter: "\"ReminderSentAt\" IS NULL AND \"Status\" = 'Confirmed'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppBookings_ReminderDue",
                table: "AppBookings");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "AppBookings");
        }
    }
}
