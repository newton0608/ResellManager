using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResellManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseCurrencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostoUnitarioMonedaOrigen",
                table: "DetallesCompra",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaTipoCambioReferencia",
                table: "Compras",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FuenteTipoCambio",
                table: "Compras",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Moneda",
                table: "Compras",
                type: "TEXT",
                maxLength: 3,
                nullable: false,
                defaultValue: "GTQ");

            migrationBuilder.AddColumn<decimal>(
                name: "TipoCambio",
                table: "Compras",
                type: "TEXT",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "TipoCambioReferencia",
                table: "Compras",
                type: "TEXT",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalMonedaOrigen",
                table: "Compras",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            // Copia directa: no convertir, redondear ni recalcular importes históricos en GTQ.
            migrationBuilder.Sql("UPDATE Compras SET TotalMonedaOrigen = Total;");
            migrationBuilder.Sql("UPDATE DetallesCompra SET CostoUnitarioMonedaOrigen = CostoUnitario;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostoUnitarioMonedaOrigen",
                table: "DetallesCompra");

            migrationBuilder.DropColumn(
                name: "FechaTipoCambioReferencia",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "FuenteTipoCambio",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "Moneda",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "TipoCambio",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "TipoCambioReferencia",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "TotalMonedaOrigen",
                table: "Compras");
        }
    }
}
