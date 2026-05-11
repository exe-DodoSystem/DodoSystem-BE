using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMEFLOWSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateShiftEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Shifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ShiftPatterns",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ShiftPatterns");
        }
    }
}
