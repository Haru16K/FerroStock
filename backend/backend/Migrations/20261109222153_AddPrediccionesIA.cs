using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPrediccionesIA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsDemo",
                table: "Ventas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EsEstacional",
                table: "Productos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MesPicoEstacional",
                table: "Productos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EjecucionesModelo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Inicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Fin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Origen = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Mes = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProductosProcesados = table.Column<int>(type: "int", nullable: false),
                    Mae = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Mape = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    EstacionalesDetectados = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EjecucionesModelo", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Productos",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "EsEstacional", "MesPicoEstacional" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "Productos",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "EsEstacional", "MesPicoEstacional" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "Productos",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "EsEstacional", "MesPicoEstacional" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "Productos",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "EsEstacional", "MesPicoEstacional" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "Productos",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "EsEstacional", "MesPicoEstacional" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "Productos",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "EsEstacional", "MesPicoEstacional" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "Productos",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "EsEstacional", "MesPicoEstacional" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$muwmZoX/k0yJrLH6T/PmSeqma6TMRCWJVOSxqS5TfrnxYfJWTGZkK");

            migrationBuilder.CreateIndex(
                name: "IX_EjecucionesModelo_Mes_Estado",
                table: "EjecucionesModelo",
                columns: new[] { "Mes", "Estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EjecucionesModelo");

            migrationBuilder.DropColumn(
                name: "EsDemo",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "EsEstacional",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "MesPicoEstacional",
                table: "Productos");

            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$vRpI/2trNa8N7kOSI2MFkuR1lFBYbwIjJHqpubmUBbtjCcfw5go22");
        }
    }
}
