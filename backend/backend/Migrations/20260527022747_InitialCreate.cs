using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Productos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CategoriaId = table.Column<int>(type: "int", nullable: false),
                    PrecioCompra = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    StockActual = table.Column<int>(type: "int", nullable: false),
                    StockMinimo = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Productos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Productos_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Movimientos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Proveedor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    UsuarioId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movimientos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Movimientos_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movimientos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Categorias",
                columns: new[] { "Id", "Descripcion", "Nombre" },
                values: new object[,]
                {
                    { 1, "Martillos, destornilladores, llaves", "Herramientas" },
                    { 2, "Cables, interruptores, tomacorrientes", "Electricidad" },
                    { 3, "Tubos, llaves de paso, uniones", "Plomería" },
                    { 4, "Pinturas, diluyentes, brochas", "Pinturas" },
                    { 5, "Tornillos, clavos, tacos", "Fijaciones" }
                });

            migrationBuilder.InsertData(
                table: "Usuarios",
                columns: new[] { "Id", "Activo", "Email", "Nombre", "PasswordHash", "Rol" },
                values: new object[] { 1, true, "admin@ferrotech.pe", "Administrador FerroTech", "$2a$11$Kay0HinH3u//Eij4DQSTFuCj0YNI5m22a3gPgQw9PfsQ201EDsOD6", "Administrador" });

            migrationBuilder.InsertData(
                table: "Productos",
                columns: new[] { "Id", "Activo", "CategoriaId", "Codigo", "Nombre", "PrecioCompra", "StockActual", "StockMinimo" },
                values: new object[,]
                {
                    { 1, true, 1, "HER-001", "Martillo carpintero 16oz", 18.50m, 25, 5 },
                    { 2, true, 1, "HER-002", "Destornillador estrella 6\"", 8.00m, 40, 10 },
                    { 3, true, 2, "ELE-001", "Cable NYM 2.5mm x metro", 3.20m, 200, 50 },
                    { 4, true, 2, "ELE-002", "Interruptor simple Bticino", 6.50m, 60, 15 },
                    { 5, true, 3, "PLO-001", "Tubo PVC 3/4\" x 3m", 12.00m, 80, 20 },
                    { 6, true, 4, "PIN-001", "Pintura latex blanca 4L", 35.00m, 3, 5 },
                    { 7, true, 5, "FIJ-001", "Tornillo autorroscante 1\"", 0.10m, 500, 100 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Movimientos_Fecha",
                table: "Movimientos",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Movimientos_ProductoId",
                table: "Movimientos",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Movimientos_UsuarioId",
                table: "Movimientos",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CategoriaId",
                table: "Productos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Codigo",
                table: "Productos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Movimientos");

            migrationBuilder.DropTable(
                name: "Productos");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Categorias");
        }
    }
}
