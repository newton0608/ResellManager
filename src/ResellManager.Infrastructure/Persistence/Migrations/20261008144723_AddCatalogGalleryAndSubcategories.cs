using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResellManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogGalleryAndSubcategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoriaPadreId",
                table: "Categorias",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductoImagenes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: false),
                    RutaRelativa = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    Orden = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoImagenes", x => x.Id);
                    table.CheckConstraint("CK_ProductoImagenes_Orden", "Orden >= 0 AND Orden < 8");
                    table.ForeignKey(
                        name: "FK_ProductoImagenes_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Registrar la portada existente preserva la referencia y el archivo, sin reprocesarlos.
            // Guid en el mismo formato TEXT en mayúsculas que escribe el proveedor SQLite de EF.
            migrationBuilder.Sql(
                """
                INSERT INTO ProductoImagenes (Id, ProductoId, RutaRelativa, Orden)
                SELECT hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' ||
                       hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6)),
                       Id, ImagenPrincipalRuta, 0
                FROM Productos
                WHERE ImagenPrincipalRuta IS NOT NULL AND trim(ImagenPrincipalRuta) <> '';
                """);
            migrationBuilder.CreateIndex(
                name: "IX_Categorias_CategoriaPadreId",
                table: "Categorias",
                column: "CategoriaPadreId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductoImagenes_ProductoId_Orden",
                table: "ProductoImagenes",
                columns: new[] { "ProductoId", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductoImagenes_ProductoId_RutaRelativa",
                table: "ProductoImagenes",
                columns: new[] { "ProductoId", "RutaRelativa" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Categorias_Categorias_CategoriaPadreId",
                table: "Categorias",
                column: "CategoriaPadreId",
                principalTable: "Categorias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categorias_Categorias_CategoriaPadreId",
                table: "Categorias");

            migrationBuilder.DropTable(
                name: "ProductoImagenes");

            migrationBuilder.DropIndex(
                name: "IX_Categorias_CategoriaPadreId",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "CategoriaPadreId",
                table: "Categorias");
        }
    }
}
