using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurantes.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MenuItems_RestaurantId",
                table: "MenuItems");

            migrationBuilder.AddColumn<bool>(
                name: "IsPromotion",
                table: "MenuItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PromotionEndsAt",
                table: "MenuItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PromotionStartsAt",
                table: "MenuItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_RestaurantId_IsPromotion",
                table: "MenuItems",
                columns: new[] { "RestaurantId", "IsPromotion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MenuItems_RestaurantId_IsPromotion",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "IsPromotion",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "PromotionEndsAt",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "PromotionStartsAt",
                table: "MenuItems");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_RestaurantId",
                table: "MenuItems",
                column: "RestaurantId");
        }
    }
}
