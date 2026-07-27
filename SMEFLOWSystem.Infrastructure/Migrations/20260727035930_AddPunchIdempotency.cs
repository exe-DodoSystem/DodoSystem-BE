using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMEFLOWSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPunchIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientRequestId",
                table: "RawPunchLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_RawPunchLogs_Tenant_Employee_ClientRequestId",
                table: "RawPunchLogs",
                columns: new[] { "TenantId", "EmployeeId", "ClientRequestId" },
                unique: true,
                filter: "\"ClientRequestId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_RawPunchLogs_Tenant_Employee_ClientRequestId",
                table: "RawPunchLogs");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "RawPunchLogs");
        }
    }
}
