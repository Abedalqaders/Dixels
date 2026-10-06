using Dixels.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Migrations
{
    /// <summary>
    /// Search matches a name anywhere in it (LIKE '%term%'), which a plain btree index can't
    /// help with: every search read every name. Trigram (pg_trgm) GIN indexes answer it for
    /// terms of three or more characters. One per searched column: the four name tables, and
    /// the user fields the Users page searches — on lower(...), the exact expression that
    /// query compares, or Postgres wouldn't match it to the index. Raw SQL like the bookings'
    /// exclusion constraint (EF can't describe either), so no model snapshot change.
    /// pg_trgm is a trusted extension (Postgres 13+), so the database owner can create it.
    /// </summary>
    [DbContext(typeof(DixelsDbContext))]
    [Migration("20261006150000_Add_Name_Search_Indexes")]
    public partial class Add_Name_Search_Indexes : Migration
    {
        private static readonly string[] NameTables =
        {
            "AppBuildingTranslations", "AppFloorTranslations", "AppSpaceTranslations", "AppSpaceTypeTranslations",
        };

        private static readonly string[] UserColumns = { "UserName", "Name", "Surname", "Email" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            foreach (var table in NameTables)
            {
                migrationBuilder.Sql($"""CREATE INDEX "IX_{table}_NormalizedName_Trgm" ON "{table}" USING gin ("NormalizedName" gin_trgm_ops);""");
            }

            foreach (var column in UserColumns)
            {
                migrationBuilder.Sql($"""CREATE INDEX "IX_AbpUsers_{column}_Trgm" ON "AbpUsers" USING gin (lower("{column}") gin_trgm_ops);""");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in NameTables)
            {
                migrationBuilder.Sql($"""DROP INDEX IF EXISTS "IX_{table}_NormalizedName_Trgm";""");
            }

            foreach (var column in UserColumns)
            {
                migrationBuilder.Sql($"""DROP INDEX IF EXISTS "IX_AbpUsers_{column}_Trgm";""");
            }

            // The extension stays: dropping it could break anything else that came to use it.
        }
    }
}
