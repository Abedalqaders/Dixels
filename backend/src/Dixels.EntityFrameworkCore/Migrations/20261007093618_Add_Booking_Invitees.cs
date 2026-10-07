using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Migrations
{
    /// <inheritdoc />
    public partial class Add_Booking_Invitees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppBookingAttendees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ResponseStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBookingAttendees", x => x.Id);
                    table.CheckConstraint("CK_AppBookingAttendees_UserOrEmail", "(\"UserId\" IS NULL) <> (\"Email\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_AppBookingAttendees_AbpUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppBookingAttendees_AppBookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "AppBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppBookingSeriesAttendees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ResponseStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBookingSeriesAttendees", x => x.Id);
                    table.CheckConstraint("CK_AppBookingSeriesAttendees_UserOrEmail", "(\"UserId\" IS NULL) <> (\"Email\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_AppBookingSeriesAttendees_AbpUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppBookingSeriesAttendees_AppBookingSeries_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "AppBookingSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppBookingAttendees_BookingId",
                table: "AppBookingAttendees",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookingAttendees_BookingId_UserId",
                table: "AppBookingAttendees",
                columns: new[] { "BookingId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookingAttendees_UserId_EndsAt",
                table: "AppBookingAttendees",
                columns: new[] { "UserId", "EndsAt" },
                filter: "\"UserId\" IS NOT NULL")
                .Annotation("Npgsql:IndexInclude", new[] { "BookingId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppBookingSeriesAttendees_SeriesId",
                table: "AppBookingSeriesAttendees",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookingSeriesAttendees_SeriesId_UserId",
                table: "AppBookingSeriesAttendees",
                columns: new[] { "SeriesId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookingSeriesAttendees_UserId",
                table: "AppBookingSeriesAttendees",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppBookingAttendees");

            migrationBuilder.DropTable(
                name: "AppBookingSeriesAttendees");
        }
    }
}
