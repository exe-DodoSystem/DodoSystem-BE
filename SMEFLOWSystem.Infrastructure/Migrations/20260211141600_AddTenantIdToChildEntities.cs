using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMEFLOWSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToChildEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "UserRoles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "OrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "BillingOrderModules",
                type: "uniqueidentifier",
                nullable: true);

            // Backfill TenantId from required principals
            migrationBuilder.Sql(@"
UPDATE ur
SET ur.TenantId = u.TenantId
FROM UserRoles ur
INNER JOIN Users u ON ur.UserId = u.Id
WHERE ur.TenantId IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE oi
SET oi.TenantId = o.TenantId
FROM OrderItems oi
INNER JOIN Orders o ON oi.OrderId = o.Id
WHERE oi.TenantId IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE bom
SET bom.TenantId = bo.TenantId
FROM BillingOrderModules bom
INNER JOIN BillingOrders bo ON bom.BillingOrderId = bo.Id
WHERE bom.TenantId IS NULL;
");

            // Enforce NOT NULL after backfill
            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "UserRoles",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "OrderItems",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "BillingOrderModules",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BillingOrderModules");
        }
    }
}
