using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMEFLOWSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDailyTimesheet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "OTBlockMinutes",
                table: "TenantAttendanceSettings",
                type: "int",
                nullable: false,
                defaultValue: 30,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "MinimumOTMinutes",
                table: "TenantAttendanceSettings",
                type: "int",
                nullable: false,
                defaultValue: 30,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<decimal>(
                name: "ActualWorkHours",
                table: "DailyTimesheets",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "EarlyLeaveMinutes",
                table: "DailyTimesheets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LateMinutes",
                table: "DailyTimesheets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "OTHours",
                table: "DailyTimesheets",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "DailyTimesheets",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualWorkHours",
                table: "DailyTimesheets");

            migrationBuilder.DropColumn(
                name: "EarlyLeaveMinutes",
                table: "DailyTimesheets");

            migrationBuilder.DropColumn(
                name: "LateMinutes",
                table: "DailyTimesheets");

            migrationBuilder.DropColumn(
                name: "OTHours",
                table: "DailyTimesheets");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "DailyTimesheets");

            migrationBuilder.AlterColumn<int>(
                name: "OTBlockMinutes",
                table: "TenantAttendanceSettings",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 30);

            migrationBuilder.AlterColumn<int>(
                name: "MinimumOTMinutes",
                table: "TenantAttendanceSettings",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 30);
        }
    }
}
