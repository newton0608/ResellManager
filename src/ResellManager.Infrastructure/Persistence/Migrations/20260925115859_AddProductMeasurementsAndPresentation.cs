using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResellManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductMeasurementsAndPresentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ContenidoMl",
                table: "Productos",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PesoGramos",
                table: "Productos",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Presentacion",
                table: "Productos",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Productos_ContenidoMl_Positivo",
                table: "Productos",
                sql: "ContenidoMl IS NULL OR ContenidoMl > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Productos_PesoGramos_Positivo",
                table: "Productos",
                sql: "PesoGramos IS NULL OR PesoGramos > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Productos_Presentacion_Longitud",
                table: "Productos",
                sql: "Presentacion IS NULL OR length(Presentacion) <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Productos_UnaMedida",
                table: "Productos",
                sql: "ContenidoMl IS NULL OR PesoGramos IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Productos_ContenidoMl_Positivo",
                table: "Productos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Productos_PesoGramos_Positivo",
                table: "Productos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Productos_Presentacion_Longitud",
                table: "Productos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Productos_UnaMedida",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "ContenidoMl",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "PesoGramos",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "Presentacion",
                table: "Productos");
        }
    }
}
