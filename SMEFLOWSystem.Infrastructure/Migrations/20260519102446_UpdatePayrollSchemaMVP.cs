using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMEFLOWSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePayrollSchemaMVP : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalSalary",
                table: "Payrolls");

            migrationBuilder.RenameColumn(
                name: "Deduction",
                table: "Payrolls",
                newName: "TotalOTHours");

            migrationBuilder.RenameColumn(
                name: "Bonus",
                table: "Payrolls",
                newName: "CustomBonus");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Payrolls",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldDefaultValue: "Draft");

            migrationBuilder.AddColumn<decimal>(
                name: "CustomDeduction",
                table: "Payrolls",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetSalary",
                table: "Payrolls",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OTPay",
                table: "Payrolls",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PenaltyFee",
                table: "Payrolls",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomDeduction",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "NetSalary",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "OTPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "PenaltyFee",
                table: "Payrolls");

            migrationBuilder.RenameColumn(
                name: "TotalOTHours",
                table: "Payrolls",
                newName: "Deduction");

            migrationBuilder.RenameColumn(
                name: "CustomBonus",
                table: "Payrolls",
                newName: "Bonus");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Payrolls",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Draft",
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalSalary",
                table: "Payrolls",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
