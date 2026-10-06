using Dixels.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Migrations
{
    /// <summary>
    /// Blank titles used to be saved as the English word "Booking"; they're now saved empty
    /// and each screen shows its own translated default. Data only, so no model snapshot
    /// change. A title someone typed as "Booking" also becomes empty, which reads the same.
    /// </summary>
    [DbContext(typeof(DixelsDbContext))]
    [Migration("20261006120000_Store_Blank_Booking_Titles_Empty")]
    public partial class Store_Blank_Booking_Titles_Empty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "AppBookings" SET "Title" = '' WHERE "Title" = 'Booking';""");
            migrationBuilder.Sql("""UPDATE "AppBookingSeries" SET "Title" = '' WHERE "Title" = 'Booking';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "AppBookings" SET "Title" = 'Booking' WHERE "Title" = '';""");
            migrationBuilder.Sql("""UPDATE "AppBookingSeries" SET "Title" = 'Booking' WHERE "Title" = '';""");
        }
    }
}
