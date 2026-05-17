using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Restaurantes.Web.Data;

#nullable disable

namespace Restaurantes.Web.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260507165000_AddMenuItemWhatsAppProductId")]
    public partial class AddMenuItemWhatsAppProductId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WhatsAppProductId",
                table: "MenuItems",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_RestaurantId_WhatsAppProductId",
                table: "MenuItems",
                columns: new[] { "RestaurantId", "WhatsAppProductId" },
                unique: true,
                filter: "\"WhatsAppProductId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MenuItems_RestaurantId_WhatsAppProductId",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "WhatsAppProductId",
                table: "MenuItems");
        }
    }
}
