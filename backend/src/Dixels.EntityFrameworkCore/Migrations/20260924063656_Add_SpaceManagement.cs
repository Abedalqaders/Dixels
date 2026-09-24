using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Migrations
{
    /// <inheritdoc />
    public partial class Add_SpaceManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAvailabilityOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Effect = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReasonCategory = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReasonDetail = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAvailabilityOverrides", x => x.Id);
                    table.CheckConstraint("CK_AppAvailabilityOverrides_EndsAfterStarts", "\"EndsAt\" > \"StartsAt\"");
                });

            migrationBuilder.CreateTable(
                name: "AppBuildings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BuildingNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    Hours_IsOpen24Hours = table.Column<bool>(type: "boolean", nullable: false),
                    Hours_Open = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Hours_Close = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    MaxDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxHorizonDays = table.Column<int>(type: "integer", nullable: false),
                    MinLeadMinutes = table.Column<int>(type: "integer", nullable: false),
                    DeletionBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBuildings", x => x.Id);
                    table.CheckConstraint("CK_AppBuildings_MaxHorizonDaysPositive", "\"MaxHorizonDays\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "AppSpaceTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IconKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSpaceTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppFloors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FloorNumber = table.Column<int>(type: "integer", nullable: true),
                    Days = table.Column<int>(type: "integer", nullable: true),
                    Hours_IsOpen24Hours = table.Column<bool>(type: "boolean", nullable: true),
                    Hours_Open = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Hours_Close = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    MaxDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    DeletionBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppFloors", x => x.Id);
                    table.CheckConstraint("CK_AppFloors_HoursOpenCloseTogether", "(\"Hours_Open\" IS NULL) = (\"Hours_Close\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_AppFloors_AppBuildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "AppBuildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppSpaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FloorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SpaceTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: true),
                    Hours_IsOpen24Hours = table.Column<bool>(type: "boolean", nullable: true),
                    Hours_Open = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Hours_Close = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    MaxDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    MinAttendees = table.Column<int>(type: "integer", nullable: true),
                    DeletionBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSpaces", x => x.Id);
                    table.CheckConstraint("CK_AppSpaces_CapacityPositive", "\"Capacity\" > 0");
                    table.CheckConstraint("CK_AppSpaces_HoursOpenCloseTogether", "(\"Hours_Open\" IS NULL) = (\"Hours_Close\" IS NULL)");
                    table.CheckConstraint("CK_AppSpaces_MinAttendeesWithinCapacity", "\"MinAttendees\" IS NULL OR \"MinAttendees\" <= \"Capacity\"");
                    table.ForeignKey(
                        name: "FK_AppSpaces_AppFloors_FloorId",
                        column: x => x.FloorId,
                        principalTable: "AppFloors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppSpaces_AppSpaceTypes_SpaceTypeId",
                        column: x => x.SpaceTypeId,
                        principalTable: "AppSpaceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAvailabilityOverrides_Scope_ScopeId_StartsAt_EndsAt",
                table: "AppAvailabilityOverrides",
                columns: new[] { "Scope", "ScopeId", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppFloors_BuildingId",
                table: "AppFloors",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaces_FloorId",
                table: "AppSpaces",
                column: "FloorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaces_SpaceTypeId",
                table: "AppSpaces",
                column: "SpaceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaceTypes_Name",
                table: "AppSpaceTypes",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAvailabilityOverrides");

            migrationBuilder.DropTable(
                name: "AppSpaces");

            migrationBuilder.DropTable(
                name: "AppFloors");

            migrationBuilder.DropTable(
                name: "AppSpaceTypes");

            migrationBuilder.DropTable(
                name: "AppBuildings");
        }
    }
}
