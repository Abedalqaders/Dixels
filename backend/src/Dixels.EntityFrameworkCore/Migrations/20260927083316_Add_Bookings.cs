using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Migrations
{
    /// <inheritdoc />
    public partial class Add_Bookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Attendees = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ResolvedConstraintsJson = table.Column<string>(type: "jsonb", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledById = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CancelledByAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBookings", x => x.Id);
                    table.CheckConstraint("CK_AppBookings_AttendeesPositive", "\"Attendees\" > 0");
                    table.CheckConstraint("CK_AppBookings_EndsAfterStarts", "\"EndsAt\" > \"StartsAt\"");
                    table.ForeignKey(
                        name: "FK_AppBookings_AbpUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppBookings_AppSpaces_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "AppSpaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppBookings_SpaceId_StartsAt_EndsAt",
                table: "AppBookings",
                columns: new[] { "SpaceId", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppBookings_UserId_IdempotencyKey",
                table: "AppBookings",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppBookings_UserId_StartsAt",
                table: "AppBookings",
                columns: new[] { "UserId", "StartsAt" });

            // The database-enforced no-double-booking guarantee (BRS: "application-only
            // checks are insufficient"). Two confirmed bookings on the same space can never
            // have overlapping [StartsAt, EndsAt) ranges — '[)' makes the end exclusive, so
            // back-to-back bookings are allowed. Cancelled rows are excluded, so a
            // cancellation releases its slot. btree_gist lets a GiST index compare the
            // plain uuid SpaceId with '=' alongside the range '&&'. It's a trusted
            // extension (Postgres 13+), so the database owner can create it.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql(
                "ALTER TABLE \"AppBookings\" ADD CONSTRAINT \"EX_AppBookings_NoOverlap\" " +
                "EXCLUDE USING gist (\"SpaceId\" WITH =, tstzrange(\"StartsAt\", \"EndsAt\", '[)') WITH &&) " +
                "WHERE (\"Status\" = 'Confirmed');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppBookings");
        }
    }
}
