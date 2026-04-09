using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMEFLOWSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollFieldsAndStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WorkingDays",
                table: "Payrolls",
                newName: "TotalLateMinutes");

            migrationBuilder.AlterColumn<decimal>(
                name: "Deduction",
                table: "Payrolls",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true,
                oldDefaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "AbsentDays",
                table: "Payrolls",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ActualWorkingDays",
                table: "Payrolls",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "BasePay",
                table: "Payrolls",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "StandardWorkingDays",
                table: "Payrolls",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalEarlyLeaveMinutes",
                table: "Payrolls",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbsentDays",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "ActualWorkingDays",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "BasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "StandardWorkingDays",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "TotalEarlyLeaveMinutes",
                table: "Payrolls");

            migrationBuilder.RenameColumn(
                name: "TotalLateMinutes",
                table: "Payrolls",
                newName: "WorkingDays");

            migrationBuilder.AlterColumn<decimal>(
                name: "Deduction",
                table: "Payrolls",
                type: "decimal(18,2)",
                nullable: true,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldDefaultValue: 0m);
        }
    }
}
