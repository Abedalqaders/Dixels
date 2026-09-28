using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Migrations
{
    /// <inheritdoc />
    public partial class Add_BookingSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxSeriesHorizonDays",
                table: "AppBuildings",
                type: "integer",
                nullable: false,
                defaultValue: 90);

            // Existing buildings: 90 days, or their normal horizon if that's already longer —
            // the series horizon may never be the shorter of the two (check constraint below).
            migrationBuilder.Sql(
                "UPDATE \"AppBuildings\" SET \"MaxSeriesHorizonDays\" = GREATEST(90, \"MaxHorizonDays\");");

            migrationBuilder.CreateTable(
                name: "AppBookingSeries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Attendees = table.Column<int>(type: "integer", nullable: false),
                    FirstDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Frequency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Interval = table.Column<int>(type: "integer", nullable: false),
                    WeekdaysMask = table.Column<int>(type: "integer", nullable: false),
                    MonthlyRepeat = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBookingSeries", x => x.Id);
                    table.CheckConstraint("CK_AppBookingSeries_DurationPositive", "\"DurationMinutes\" > 0");
                    table.CheckConstraint("CK_AppBookingSeries_IntervalPositive", "\"Interval\" > 0");
                    table.ForeignKey(
                        name: "FK_AppBookingSeries_AbpUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppBookingSeries_AppSpaces_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "AppSpaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AppBuildings_SeriesHorizonAtLeastHorizon",
                table: "AppBuildings",
                sql: "\"MaxSeriesHorizonDays\" >= \"MaxHorizonDays\"");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookings_SeriesId",
                table: "AppBookings",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookingSeries_SpaceId",
                table: "AppBookingSeries",
                column: "SpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookingSeries_UserId_IdempotencyKey",
                table: "AppBookingSeries",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppBookings_AppBookingSeries_SeriesId",
                table: "AppBookings",
                column: "SeriesId",
                principalTable: "AppBookingSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppBookings_AppBookingSeries_SeriesId",
                table: "AppBookings");

            migrationBuilder.DropTable(
                name: "AppBookingSeries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AppBuildings_SeriesHorizonAtLeastHorizon",
                table: "AppBuildings");

            migrationBuilder.DropIndex(
                name: "IX_AppBookings_SeriesId",
                table: "AppBookings");

            migrationBuilder.DropColumn(
                name: "MaxSeriesHorizonDays",
                table: "AppBuildings");
        }
    }
}
