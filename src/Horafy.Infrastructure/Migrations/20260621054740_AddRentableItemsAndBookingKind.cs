using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Horafy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRentableItemsAndBookingKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "bookings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Appointment");

            migrationBuilder.CreateTable(
                name: "rentable_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    daily_rate = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    security_deposit = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    buffer_days = table.Column<int>(type: "integer", nullable: false),
                    image_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rentable_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rentable_items_is_active",
                table: "rentable_items",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_rentable_items_name",
                table: "rentable_items",
                column: "name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rentable_items");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "bookings");
        }
    }
}
