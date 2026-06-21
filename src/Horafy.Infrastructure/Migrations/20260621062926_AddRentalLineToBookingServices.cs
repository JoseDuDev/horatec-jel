using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Horafy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalLineToBookingServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "quantity",
                table: "booking_services",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "rentable_item_id",
                table: "booking_services",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_booking_services_rentable_item",
                table: "booking_services",
                column: "rentable_item_id",
                filter: "rentable_item_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_booking_services_rentable_item",
                table: "booking_services");

            migrationBuilder.DropColumn(
                name: "quantity",
                table: "booking_services");

            migrationBuilder.DropColumn(
                name: "rentable_item_id",
                table: "booking_services");
        }
    }
}
