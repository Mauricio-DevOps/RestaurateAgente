using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurantes.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkServiceRequestsToTabs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TabId",
                table: "ServiceRequests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_TabId",
                table: "ServiceRequests",
                column: "TabId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequests_RestaurantTabs_TabId",
                table: "ServiceRequests",
                column: "TabId",
                principalTable: "RestaurantTabs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequests_RestaurantTabs_TabId",
                table: "ServiceRequests");

            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_TabId",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "TabId",
                table: "ServiceRequests");
        }
    }
}
