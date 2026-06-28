using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Horafy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingReminderIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_bookings_kind_rentalstatus_ends",
                table: "bookings",
                columns: new[] { "kind", "rental_status", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_status_scheduled",
                table: "bookings",
                columns: new[] { "status", "scheduled_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_bookings_kind_rentalstatus_ends",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "ix_bookings_status_scheduled",
                table: "bookings");
        }
    }
}
