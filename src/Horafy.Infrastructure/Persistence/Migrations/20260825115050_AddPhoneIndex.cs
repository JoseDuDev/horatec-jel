using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Horafy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_users_phone",
                schema: "public",
                table: "users",
                column: "phone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_phone",
                schema: "public",
                table: "users");
        }
    }
}
