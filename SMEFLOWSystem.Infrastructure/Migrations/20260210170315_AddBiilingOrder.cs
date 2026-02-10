using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMEFLOWSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBiilingOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_Orders_OrderId",
                table: "PaymentTransactions");

            migrationBuilder.RenameColumn(
                name: "OrderId",
                table: "PaymentTransactions",
                newName: "BillingOrderId");

            migrationBuilder.RenameIndex(
                name: "IX_PaymentTransactions_OrderId",
                table: "PaymentTransactions",
                newName: "IX_PaymentTransactions_BillingOrderId");

            migrationBuilder.CreateTable(
                name: "BillingOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newsequentialid())"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillingOrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillingDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true, defaultValue: 0m),
                    FinalAmount = table.Column<decimal>(type: "decimal(19,2)", nullable: true, computedColumnSql: "([TotalAmount]-[DiscountAmount])", stored: true),
                    PaymentStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__BillingOrders__3214EC07", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_BillingOrderNumber_Tenant",
                table: "BillingOrders",
                columns: new[] { "TenantId", "BillingOrderNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_BillingOrders_BillingOrderId",
                table: "PaymentTransactions",
                column: "BillingOrderId",
                principalTable: "BillingOrders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_BillingOrders_BillingOrderId",
                table: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "BillingOrders");

            migrationBuilder.RenameColumn(
                name: "BillingOrderId",
                table: "PaymentTransactions",
                newName: "OrderId");

            migrationBuilder.RenameIndex(
                name: "IX_PaymentTransactions_BillingOrderId",
                table: "PaymentTransactions",
                newName: "IX_PaymentTransactions_OrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_Orders_OrderId",
                table: "PaymentTransactions",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id");
        }
    }
}
