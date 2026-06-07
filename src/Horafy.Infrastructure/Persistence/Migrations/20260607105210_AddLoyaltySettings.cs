using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Horafy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "phone",
                schema: "public",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "loyalty_settings_credit_rate_percent",
                schema: "public",
                table: "tenants",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "loyalty_settings_is_enabled",
                schema: "public",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "loyalty_settings_min_booking_amount",
                schema: "public",
                table: "tenants",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "phone",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "loyalty_settings_credit_rate_percent",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "loyalty_settings_is_enabled",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "loyalty_settings_min_booking_amount",
                schema: "public",
                table: "tenants");
        }
    }
}
