using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Horafy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint7ReminderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "reminder_settings_enabled",
                schema: "public",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "reminder_settings_first_reminder_hours",
                schema: "public",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<int>(
                name: "reminder_settings_second_reminder_hours",
                schema: "public",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reminder_settings_enabled",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "reminder_settings_first_reminder_hours",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "reminder_settings_second_reminder_hours",
                schema: "public",
                table: "tenants");
        }
    }
}
