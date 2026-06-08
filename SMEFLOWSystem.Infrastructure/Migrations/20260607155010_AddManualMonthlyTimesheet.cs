using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMEFLOWSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManualMonthlyTimesheet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManualMonthlyTimesheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    ActualWorkingDays = table.Column<int>(type: "int", nullable: false),
                    AbsentDays = table.Column<int>(type: "int", nullable: false),
                    TotalLateMinutes = table.Column<int>(type: "int", nullable: false),
                    TotalEarlyLeaveMinutes = table.Column<int>(type: "int", nullable: false),
                    TotalOTHours = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualMonthlyTimesheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManualMonthlyTimesheets_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManualMonthlyTimesheets_EmployeeId",
                table: "ManualMonthlyTimesheets",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualMonthlyTimesheets_TenantId_EmployeeId_Month_Year",
                table: "ManualMonthlyTimesheets",
                columns: new[] { "TenantId", "EmployeeId", "Month", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManualMonthlyTimesheets");
        }
    }
}
