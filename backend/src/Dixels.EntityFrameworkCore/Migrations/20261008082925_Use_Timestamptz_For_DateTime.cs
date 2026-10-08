using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Migrations
{
    /// <summary>
    /// Every DateTime column (ABP's audit times, audit logs, background jobs, Identity, OpenIddict, ours)
    /// moves from "timestamp" (no zone) to "timestamptz", now that Npgsql's legacy timestamp mode is off.
    /// The old values are UTC (IClock is UTC), so each is read AT TIME ZONE 'UTC'. EF's generated
    /// ALTER COLUMN would read them in the session's time zone instead (Asia/Amman on a +03:00 server)
    /// and move every row by hours. One ALTER TABLE per table, so each table is rewritten once.
    /// </summary>
    public partial class Use_Timestamptz_For_DateTime : Migration
    {
        private static readonly Dictionary<string, string[]> Columns = new()
        {
            ["AbpAuditLogActions"] = new[] { "ExecutionTime" },
            ["AbpAuditLogExcelFiles"] = new[] { "CreationTime" },
            ["AbpAuditLogs"] = new[] { "ExecutionTime" },
            ["AbpBackgroundJobs"] = new[] { "CompletionTime", "CreationTime", "LastTryTime", "NextTryTime" },
            ["AbpClaimTypes"] = new[] { "CreationTime" },
            ["AbpEntityChanges"] = new[] { "ChangeTime" },
            ["AbpOrganizationUnitRoles"] = new[] { "CreationTime" },
            ["AbpOrganizationUnits"] = new[] { "CreationTime", "DeletionTime", "LastModificationTime" },
            ["AbpRoles"] = new[] { "CreationTime" },
            ["AbpSecurityLogs"] = new[] { "CreationTime" },
            ["AbpSessions"] = new[] { "LastAccessed", "SignedIn" },
            ["AbpTenants"] = new[] { "CreationTime", "DeletionTime", "LastModificationTime" },
            ["AbpUserDelegations"] = new[] { "EndTime", "StartTime" },
            ["AbpUserOrganizationUnits"] = new[] { "CreationTime" },
            ["AbpUsers"] = new[] { "CreationTime", "DeletionTime", "LastModificationTime" },
            ["AppAvailabilityOverrides"] = new[] { "CreationTime", "LastModificationTime" },
            ["AppBookingSeries"] = new[] { "CreationTime", "LastModificationTime" },
            ["AppBookings"] = new[] { "CreationTime", "LastModificationTime" },
            ["AppBuildings"] = new[] { "CreationTime", "DeletionTime", "LastModificationTime" },
            ["AppFloors"] = new[] { "CreationTime", "DeletionTime", "LastModificationTime" },
            ["AppSpaceTypes"] = new[] { "CreationTime", "DeletionTime", "LastModificationTime" },
            ["AppSpaces"] = new[] { "CreationTime", "DeletionTime", "LastModificationTime" },
            ["OpenIddictApplications"] = new[] { "CreationTime", "DeletionTime", "LastModificationTime" },
            ["OpenIddictAuthorizations"] = new[] { "CreationDate" },
            ["OpenIddictScopes"] = new[] { "CreationTime", "DeletionTime", "LastModificationTime" },
            ["OpenIddictTokens"] = new[] { "CreationDate", "ExpirationDate", "RedemptionDate" },
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AlterAll(migrationBuilder, "timestamp with time zone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            AlterAll(migrationBuilder, "timestamp without time zone");
        }

        // "x AT TIME ZONE 'UTC'" works both ways: timestamp → the instant at that UTC wall time;
        // timestamptz → the UTC wall time of that instant.
        private static void AlterAll(MigrationBuilder migrationBuilder, string type)
        {
            foreach (var (table, columns) in Columns)
            {
                var alters = columns.Select(c => $"ALTER COLUMN \"{c}\" TYPE {type} USING \"{c}\" AT TIME ZONE 'UTC'");
                migrationBuilder.Sql($"ALTER TABLE \"{table}\" {string.Join(", ", alters)};");
            }
        }
    }
}
