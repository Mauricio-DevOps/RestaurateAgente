using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurantes.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantPaymentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "paidat",
                table: "restaurantorders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "paymentcheckouturl",
                table: "restaurantorders",
                type: "TEXT",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "paymentcreatedat",
                table: "restaurantorders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "paymentid",
                table: "restaurantorders",
                type: "TEXT",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "paymentpreferenceid",
                table: "restaurantorders",
                type: "TEXT",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "paymentprovider",
                table: "restaurantorders",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "paymentproviderstatus",
                table: "restaurantorders",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "paymentproviderstatusdetail",
                table: "restaurantorders",
                type: "TEXT",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "paymentstatus",
                table: "restaurantorders",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "NAO_APLICAVEL");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "paymentupdatedat",
                table: "restaurantorders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("UPDATE restaurantorders SET paymentstatus = 'PAGAMENTO_APROVADO', paymentprovider = 'MercadoPago' WHERE type = 'DELIVERY' AND paymentstatus = 'NAO_APLICAVEL';");

            migrationBuilder.CreateTable(
                name: "restaurantpaymentsettings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    restaurantid = table.Column<Guid>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    protectedaccesstoken = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    protectedwebhooksecret = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    mercadopagouserid = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    isenabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    createdat = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updatedat = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_restaurantpaymentsettings", x => x.id);
                    table.ForeignKey(
                        name: "fk_restaurantpaymentsettings_restaurants_restaurantid",
                        column: x => x.restaurantid,
                        principalTable: "restaurants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "paymentwebhookevents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    restaurantid = table.Column<Guid>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    eventid = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    resourceid = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    action = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    paymentstatus = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    requestid = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    payloadjson = table.Column<string>(type: "TEXT", nullable: false),
                    createdat = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_paymentwebhookevents", x => x.id);
                    table.ForeignKey(
                        name: "fk_paymentwebhookevents_restaurants_restaurantid",
                        column: x => x.restaurantid,
                        principalTable: "restaurants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_restaurantorders_restaurantid_paymentstatus_createdat",
                table: "restaurantorders",
                columns: new[] { "restaurantid", "paymentstatus", "createdat" });

            migrationBuilder.CreateIndex(
                name: "ix_restaurantpaymentsettings_restaurantid_provider",
                table: "restaurantpaymentsettings",
                columns: new[] { "restaurantid", "provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_restaurantpaymentsettings_restaurantid",
                table: "restaurantpaymentsettings",
                column: "restaurantid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_paymentwebhookevents_provider_eventid",
                table: "paymentwebhookevents",
                columns: new[] { "provider", "eventid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_paymentwebhookevents_restaurantid_createdat",
                table: "paymentwebhookevents",
                columns: new[] { "restaurantid", "createdat" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "paymentwebhookevents");
            migrationBuilder.DropTable(name: "restaurantpaymentsettings");
            migrationBuilder.DropIndex(name: "ix_restaurantorders_restaurantid_paymentstatus_createdat", table: "restaurantorders");
            migrationBuilder.DropColumn(name: "paidat", table: "restaurantorders");
            migrationBuilder.DropColumn(name: "paymentcheckouturl", table: "restaurantorders");
            migrationBuilder.DropColumn(name: "paymentcreatedat", table: "restaurantorders");
            migrationBuilder.DropColumn(name: "paymentid", table: "restaurantorders");
            migrationBuilder.DropColumn(name: "paymentpreferenceid", table: "restaurantorders");
            migrationBuilder.DropColumn(name: "paymentprovider", table: "restaurantorders");
            migrationBuilder.DropColumn(name: "paymentproviderstatus", table: "restaurantorders");
            migrationBuilder.DropColumn(name: "paymentproviderstatusdetail", table: "restaurantorders");
            migrationBuilder.DropColumn(name: "paymentstatus", table: "restaurantorders");
            migrationBuilder.DropColumn(name: "paymentupdatedat", table: "restaurantorders");
        }
    }
}
