using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResellManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateInventoryReservationFromPhysicalState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE UnidadesInventario
                SET Estado = CASE
                    WHEN FechaIngreso IS NULL THEN 'Comprada'
                    ELSE 'Disponible'
                END
                WHERE Estado = 'Apartada'
                """
            );

            migrationBuilder.AddColumn<int>(
                name: "DetallePedidoReservaId",
                table: "UnidadesInventario",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesInventario_DetallePedidoReservaId",
                table: "UnidadesInventario",
                column: "DetallePedidoReservaId");

            migrationBuilder.AddForeignKey(
                name: "FK_UnidadesInventario_DetallesPedido_DetallePedidoReservaId",
                table: "UnidadesInventario",
                column: "DetallePedidoReservaId",
                principalTable: "DetallesPedido",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE UnidadesInventario
                SET Estado = 'Apartada'
                WHERE DetallePedidoReservaId IS NOT NULL
                """
            );

            migrationBuilder.DropForeignKey(
                name: "FK_UnidadesInventario_DetallesPedido_DetallePedidoReservaId",
                table: "UnidadesInventario");

            migrationBuilder.DropIndex(
                name: "IX_UnidadesInventario_DetallePedidoReservaId",
                table: "UnidadesInventario");

            migrationBuilder.DropColumn(
                name: "DetallePedidoReservaId",
                table: "UnidadesInventario");
        }
    }
}
