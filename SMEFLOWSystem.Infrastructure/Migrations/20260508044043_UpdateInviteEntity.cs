using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMEFLOWSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInviteEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InvitedByUserId",
                table: "Invites",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invites_InvitedByUserId",
                table: "Invites",
                column: "InvitedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invites_InvitedByUserId",
                table: "Invites",
                column: "InvitedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invites_InvitedByUserId",
                table: "Invites");

            migrationBuilder.DropIndex(
                name: "IX_Invites_InvitedByUserId",
                table: "Invites");

            migrationBuilder.DropColumn(
                name: "InvitedByUserId",
                table: "Invites");
        }
    }
}
