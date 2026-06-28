using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Horafy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHoliday : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_id",
                table: "bookings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "bookings",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "holidays",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_recurring_annually = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_holidays", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_external_id",
                table: "bookings",
                column: "external_id",
                unique: true,
                filter: "external_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_holidays_date",
                table: "holidays",
                column: "date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "holidays");

            migrationBuilder.DropIndex(
                name: "ix_bookings_external_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "external_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "source",
                table: "bookings");
        }
    }
}
