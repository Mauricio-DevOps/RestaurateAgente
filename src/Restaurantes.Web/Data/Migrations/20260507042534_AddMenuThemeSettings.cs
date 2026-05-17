using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurantes.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuThemeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MenuMode",
                table: "Restaurants",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "CLARO");

            migrationBuilder.AddColumn<string>(
                name: "MenuTheme",
                table: "Restaurants",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "ELEGANTE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MenuMode",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "MenuTheme",
                table: "Restaurants");
        }
    }
}
