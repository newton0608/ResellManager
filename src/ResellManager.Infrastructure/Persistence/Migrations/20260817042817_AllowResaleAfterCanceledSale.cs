using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResellManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowResaleAfterCanceledSale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DetallesVenta_UnidadInventarioId",
                table: "DetallesVenta");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesVenta_UnidadInventarioId",
                table: "DetallesVenta",
                column: "UnidadInventarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DetallesVenta_UnidadInventarioId",
                table: "DetallesVenta");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesVenta_UnidadInventarioId",
                table: "DetallesVenta",
                column: "UnidadInventarioId",
                unique: true);
        }
    }
}
