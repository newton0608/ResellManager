using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResellManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestrictCatalogSalesWithoutInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "UnidadInventarioId",
                table: "DetallesVenta",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<decimal>(
                name: "CostoUnitario",
                table: "DetallesVenta",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductoId",
                table: "DetallesVenta",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DetallesVenta_ProductoId",
                table: "DetallesVenta",
                column: "ProductoId");

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesVenta_Productos_ProductoId",
                table: "DetallesVenta",
                column: "ProductoId",
                principalTable: "Productos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetallesVenta_Productos_ProductoId",
                table: "DetallesVenta");

            migrationBuilder.DropIndex(
                name: "IX_DetallesVenta_ProductoId",
                table: "DetallesVenta");

            migrationBuilder.DropColumn(
                name: "CostoUnitario",
                table: "DetallesVenta");

            migrationBuilder.DropColumn(
                name: "ProductoId",
                table: "DetallesVenta");

            migrationBuilder.AlterColumn<int>(
                name: "UnidadInventarioId",
                table: "DetallesVenta",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
