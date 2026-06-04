using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Horafy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellationPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "cancellation_policy_allow_customer_cancellation",
                schema: "public",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cancellation_policy_cancellation_fee_percent",
                schema: "public",
                table: "tenants",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "cancellation_policy_min_cancellation_hours",
                schema: "public",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancellation_policy_allow_customer_cancellation",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "cancellation_policy_cancellation_fee_percent",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "cancellation_policy_min_cancellation_hours",
                schema: "public",
                table: "tenants");
        }
    }
}
