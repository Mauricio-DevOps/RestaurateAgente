using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurantes.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantWhatsAppPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WhatsAppPhone",
                table: "Restaurants",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_WhatsAppPhone",
                table: "Restaurants",
                column: "WhatsAppPhone",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Restaurants_WhatsAppPhone",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "WhatsAppPhone",
                table: "Restaurants");
        }
    }
}
