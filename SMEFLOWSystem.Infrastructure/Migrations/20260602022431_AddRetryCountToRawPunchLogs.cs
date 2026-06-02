using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMEFLOWSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRetryCountToRawPunchLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "RawPunchLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "RawPunchLogs");
        }
    }
}
