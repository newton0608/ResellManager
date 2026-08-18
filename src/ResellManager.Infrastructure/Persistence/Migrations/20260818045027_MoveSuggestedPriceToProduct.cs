using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResellManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveSuggestedPriceToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PrecioSugerido",
                table: "Productos",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                """
                UPDATE Productos
                SET PrecioSugerido = COALESCE(
                    (SELECT PrecioLista
                     FROM UnidadesInventario
                     WHERE ProductoId = Productos.Id
                     ORDER BY Id DESC
                     LIMIT 1),
                    0)
                """
            );

            migrationBuilder.DropColumn(
                name: "PrecioLista",
                table: "UnidadesInventario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PrecioLista",
                table: "UnidadesInventario",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                """
                UPDATE UnidadesInventario
                SET PrecioLista = COALESCE(
                    (SELECT PrecioSugerido
                     FROM Productos
                     WHERE Id = UnidadesInventario.ProductoId),
                    0)
                """
            );

            migrationBuilder.DropColumn(
                name: "PrecioSugerido",
                table: "Productos");
        }
    }
}
