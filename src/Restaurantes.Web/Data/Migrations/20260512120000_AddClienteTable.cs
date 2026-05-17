using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Restaurantes.Web.Data;

#nullable disable

namespace Restaurantes.Web.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260512120000_AddClienteTable")]
    public partial class AddClienteTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    ID_Cliente = table.Column<Guid>(type: "TEXT", nullable: false),
                    CLIENTE_NOME = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    CPF_CNPJ = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    CLIENTE_EMAIL = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    CLIENTE_TELEFONE_CELULAR = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CLIENTE_DATA_CRIACAO = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.ID_Cliente);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_CpfCnpj",
                table: "Clientes",
                column: "CPF_CNPJ",
                unique: true,
                filter: "\"CPF_CNPJ\" IS NOT NULL AND \"CPF_CNPJ\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Email",
                table: "Clientes",
                column: "CLIENTE_EMAIL");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_TelefoneCelular",
                table: "Clientes",
                column: "CLIENTE_TELEFONE_CELULAR",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clientes");
        }
    }
}
