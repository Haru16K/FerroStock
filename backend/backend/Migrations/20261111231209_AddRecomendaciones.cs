using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddRecomendaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Calificacion",
                table: "Proveedores",
                type: "decimal(3,1)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiasEntrega",
                table: "Proveedores",
                type: "int",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.CreateTable(
                name: "ConfiguracionesIa",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Valor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesIa", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "Recomendaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Prioridad = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: true),
                    ProveedorId = table.Column<int>(type: "int", nullable: true),
                    MontoEstimado = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    FechaGeneracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaGestion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GestionadaPorId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recomendaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recomendaciones_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recomendaciones_Proveedores_ProveedorId",
                        column: x => x.ProveedorId,
                        principalTable: "Proveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recomendaciones_Usuarios_GestionadaPorId",
                        column: x => x.GestionadaPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Proveedores",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Calificacion", "DiasEntrega" },
                values: new object[] { 4.2m, 5 });

            migrationBuilder.UpdateData(
                table: "Proveedores",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Calificacion", "DiasEntrega" },
                values: new object[] { 3.5m, 12 });

            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$AT13V6UnSrbHsjZaVAfHxu5XQxGnjmA6vhOQIS9I7LDI20p/GvhDG");

            migrationBuilder.CreateIndex(
                name: "IX_Recomendaciones_Clave",
                table: "Recomendaciones",
                column: "Clave");

            migrationBuilder.CreateIndex(
                name: "IX_Recomendaciones_GestionadaPorId",
                table: "Recomendaciones",
                column: "GestionadaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Recomendaciones_ProductoId",
                table: "Recomendaciones",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Recomendaciones_ProveedorId",
                table: "Recomendaciones",
                column: "ProveedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Recomendaciones_Tipo_Estado",
                table: "Recomendaciones",
                columns: new[] { "Tipo", "Estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionesIa");

            migrationBuilder.DropTable(
                name: "Recomendaciones");

            migrationBuilder.DropColumn(
                name: "Calificacion",
                table: "Proveedores");

            migrationBuilder.DropColumn(
                name: "DiasEntrega",
                table: "Proveedores");

            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$muwmZoX/k0yJrLH6T/PmSeqma6TMRCWJVOSxqS5TfrnxYfJWTGZkK");
        }
    }
}
