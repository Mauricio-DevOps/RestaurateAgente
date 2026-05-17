using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurantes.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantOperationalSla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttendanceSlaMinutes",
                table: "Restaurants",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingSlaMinutes",
                table: "Restaurants",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttendanceSlaMinutes",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "PendingSlaMinutes",
                table: "Restaurants");
        }
    }
}
