using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurantes.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountCoupons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CouponCodeSnapshot",
                table: "Orders",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CouponTypeSnapshot",
                table: "Orders",
                type: "TEXT",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CouponValueSnapshot",
                table: "Orders",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscountCents",
                table: "Orders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "DiscountCouponId",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubtotalCents",
                table: "Orders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE Orders SET SubtotalCents = TotalCents WHERE SubtotalCents = 0;");

            migrationBuilder.CreateTable(
                name: "DiscountCoupons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Value = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountCoupons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscountCoupons_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DiscountCouponId",
                table: "Orders",
                column: "DiscountCouponId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_DiscountCouponId",
                table: "Orders",
                columns: new[] { "RestaurantId", "DiscountCouponId" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscountCoupons_RestaurantId_Code",
                table: "DiscountCoupons",
                columns: new[] { "RestaurantId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_DiscountCoupons_DiscountCouponId",
                table: "Orders",
                column: "DiscountCouponId",
                principalTable: "DiscountCoupons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_DiscountCoupons_DiscountCouponId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "DiscountCoupons");

            migrationBuilder.DropIndex(
                name: "IX_Orders_DiscountCouponId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RestaurantId_DiscountCouponId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CouponCodeSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CouponTypeSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CouponValueSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountCents",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountCouponId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SubtotalCents",
                table: "Orders");
        }
    }
}
