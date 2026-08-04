using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMEFLOWSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBillingOrderDiscountColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalAmount",
                table: "BillingOrders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "BillingOrders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "BillingOrders",
                type: "numeric(18,2)",
                nullable: true,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalAmount",
                table: "BillingOrders",
                type: "numeric(19,2)",
                nullable: true,
                computedColumnSql: "\"TotalAmount\" - \"DiscountAmount\"",
                stored: true);
        }
    }
}
