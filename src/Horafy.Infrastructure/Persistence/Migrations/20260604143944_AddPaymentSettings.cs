using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Horafy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "payment_settings_deposit_mode",
                schema: "public",
                table: "tenants",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<decimal>(
                name: "payment_settings_deposit_value",
                schema: "public",
                table: "tenants",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "payment_settings_requires_payment",
                schema: "public",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payment_settings_deposit_mode",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "payment_settings_deposit_value",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "payment_settings_requires_payment",
                schema: "public",
                table: "tenants");
        }
    }
}
