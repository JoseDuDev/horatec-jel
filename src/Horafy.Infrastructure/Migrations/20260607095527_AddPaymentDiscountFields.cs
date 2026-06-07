using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Horafy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentDiscountFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "voucher_code",
                table: "payments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "voucher_discount_amount",
                table: "payments",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "wallet_amount",
                table: "payments",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "voucher_code",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "voucher_discount_amount",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "wallet_amount",
                table: "payments");
        }
    }
}
