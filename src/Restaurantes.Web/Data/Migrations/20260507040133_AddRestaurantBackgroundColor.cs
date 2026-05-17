using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurantes.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantBackgroundColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackgroundColor",
                table: "Restaurants",
                type: "TEXT",
                maxLength: 7,
                nullable: false,
                defaultValue: "#F6F3EF");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackgroundColor",
                table: "Restaurants");
        }
    }
}
