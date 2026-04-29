using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMEFLOWSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAttendanceHRLogic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attendances");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "DayStartCutOffTime",
                table: "TenantAttendanceSettings",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "OvertimeRequests",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "OvertimeRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "SystemAnomalyFlag",
                table: "DailyTimesheets",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ExpectedShiftSource",
                table: "DailyTimesheets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "TotalActualWorkedMinutes",
                table: "DailyTimesheets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "RawPunchLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PunchType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    SelfieUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeviceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawPunchLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawPunchLogs_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftPatternDays_ScheduledShiftId",
                table: "ShiftPatternDays",
                column: "ScheduledShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_ApprovedByUserId",
                table: "OvertimeRequests",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_EmployeeId",
                table: "OvertimeRequests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequestSegments_TargetShiftSegmentId",
                table: "LeaveRequestSegments",
                column: "TargetShiftSegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_ApprovedByUserId",
                table: "LeaveRequests",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_EmployeeId",
                table: "LeaveRequests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftPatterns_EmployeeId",
                table: "EmployeeShiftPatterns",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftPatterns_ShiftPatternId",
                table: "EmployeeShiftPatterns",
                column: "ShiftPatternId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyTimesheets_EmployeeId",
                table: "DailyTimesheets",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyTimesheets_ExpectedShiftId",
                table: "DailyTimesheets",
                column: "ExpectedShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_RawPunchLogs_Employee_Timestamp",
                table: "RawPunchLogs",
                columns: new[] { "EmployeeId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_RawPunchLogs_IsProcessed_Timestamp",
                table: "RawPunchLogs",
                columns: new[] { "IsProcessed", "Timestamp" });

            migrationBuilder.AddForeignKey(
                name: "FK_DailyTimesheets_Employees_EmployeeId",
                table: "DailyTimesheets",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyTimesheets_Shifts_ExpectedShiftId",
                table: "DailyTimesheets",
                column: "ExpectedShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeShiftPatterns_Employees_EmployeeId",
                table: "EmployeeShiftPatterns",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeShiftPatterns_ShiftPatterns_ShiftPatternId",
                table: "EmployeeShiftPatterns",
                column: "ShiftPatternId",
                principalTable: "ShiftPatterns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequests_Employees_EmployeeId",
                table: "LeaveRequests",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequests_Users_ApprovedByUserId",
                table: "LeaveRequests",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequestSegments_ShiftSegments_TargetShiftSegmentId",
                table: "LeaveRequestSegments",
                column: "TargetShiftSegmentId",
                principalTable: "ShiftSegments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OvertimeRequests_Employees_EmployeeId",
                table: "OvertimeRequests",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OvertimeRequests_Users_ApprovedByUserId",
                table: "OvertimeRequests",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftPatternDays_Shifts_ScheduledShiftId",
                table: "ShiftPatternDays",
                column: "ScheduledShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyTimesheets_Employees_EmployeeId",
                table: "DailyTimesheets");

            migrationBuilder.DropForeignKey(
                name: "FK_DailyTimesheets_Shifts_ExpectedShiftId",
                table: "DailyTimesheets");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeShiftPatterns_Employees_EmployeeId",
                table: "EmployeeShiftPatterns");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeShiftPatterns_ShiftPatterns_ShiftPatternId",
                table: "EmployeeShiftPatterns");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequests_Employees_EmployeeId",
                table: "LeaveRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequests_Users_ApprovedByUserId",
                table: "LeaveRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequestSegments_ShiftSegments_TargetShiftSegmentId",
                table: "LeaveRequestSegments");

            migrationBuilder.DropForeignKey(
                name: "FK_OvertimeRequests_Employees_EmployeeId",
                table: "OvertimeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_OvertimeRequests_Users_ApprovedByUserId",
                table: "OvertimeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftPatternDays_Shifts_ScheduledShiftId",
                table: "ShiftPatternDays");

            migrationBuilder.DropTable(
                name: "RawPunchLogs");

            migrationBuilder.DropIndex(
                name: "IX_ShiftPatternDays_ScheduledShiftId",
                table: "ShiftPatternDays");

            migrationBuilder.DropIndex(
                name: "IX_OvertimeRequests_ApprovedByUserId",
                table: "OvertimeRequests");

            migrationBuilder.DropIndex(
                name: "IX_OvertimeRequests_EmployeeId",
                table: "OvertimeRequests");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequestSegments_TargetShiftSegmentId",
                table: "LeaveRequestSegments");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_ApprovedByUserId",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_EmployeeId",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeShiftPatterns_EmployeeId",
                table: "EmployeeShiftPatterns");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeShiftPatterns_ShiftPatternId",
                table: "EmployeeShiftPatterns");

            migrationBuilder.DropIndex(
                name: "IX_DailyTimesheets_EmployeeId",
                table: "DailyTimesheets");

            migrationBuilder.DropIndex(
                name: "IX_DailyTimesheets_ExpectedShiftId",
                table: "DailyTimesheets");

            migrationBuilder.DropColumn(
                name: "DayStartCutOffTime",
                table: "TenantAttendanceSettings");

            migrationBuilder.DropColumn(
                name: "TotalActualWorkedMinutes",
                table: "DailyTimesheets");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "OvertimeRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "OvertimeRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "SystemAnomalyFlag",
                table: "DailyTimesheets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "ExpectedShiftSource",
                table: "DailyTimesheets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateTable(
                name: "Attendances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newsequentialid())"),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CheckInLatitude = table.Column<double>(type: "float", nullable: true),
                    CheckInLongitude = table.Column<double>(type: "float", nullable: true),
                    CheckInSelfieUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CheckInTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckOutLatitude = table.Column<double>(type: "float", nullable: true),
                    CheckOutLongitude = table.Column<double>(type: "float", nullable: true),
                    CheckOutSelfieUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CheckOutTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    EarlyLeaveMinutes = table.Column<int>(type: "int", nullable: true),
                    LateMinutes = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Present"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Attendan__3214EC07446D18B6", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attendances_Employees",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Attendances_Tenants",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_EmployeeId",
                table: "Attendances",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "UQ_Attendance_Per_Day",
                table: "Attendances",
                columns: new[] { "TenantId", "EmployeeId", "WorkDate" },
                unique: true);
        }
    }
}
