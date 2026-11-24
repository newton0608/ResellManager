using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResellManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MovimientosAjustesCFProductos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApellidoOcasional",
                table: "Ventas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanceladaPor",
                table: "Ventas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CanceladaUtc",
                table: "Ventas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreOcasional",
                table: "Ventas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistradaUtc",
                table: "Ventas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioId",
                table: "Ventas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CostoConocido",
                table: "UnidadesInventario",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<int>(
                name: "ClienteId",
                table: "Pedidos",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "CanalVenta",
                table: "Pedidos",
                type: "INTEGER",
                nullable: false,
                defaultValue: 5,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistradoUtc",
                table: "Pagos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioId",
                table: "Pagos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CostoConocido",
                table: "DetallesVenta",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CostoConocido",
                table: "DetallesCompra",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "AnulacionesCF",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VentaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", nullable: false),
                    SolicitanteId = table.Column<string>(type: "TEXT", nullable: false),
                    SolicitadaUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AdministradorId = table.Column<string>(type: "TEXT", nullable: true),
                    DecididaUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MotivoDecision = table.Column<string>(type: "TEXT", nullable: true),
                    CompletadaUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnulacionesCF", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnulacionesCF_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AperturasCartera",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false),
                    FilasJson = table.Column<string>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<string>(type: "TEXT", nullable: false),
                    ConfirmadaUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Corte = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Total = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AperturasCartera", x => x.Id);
                    table.CheckConstraint("CK_Apertura_Unica", "Id = 1");
                });

            migrationBuilder.CreateTable(
                name: "CambiosPrecioProducto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Anterior = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Nuevo = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    UsuarioId = table.Column<string>(type: "TEXT", nullable: false),
                    RegistradoUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CambiosPrecioProducto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CambiosPrecioProducto_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CobrosVenta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VentaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Metodo = table.Column<int>(type: "INTEGER", nullable: false),
                    Aplicado = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Entregado = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Referencia = table.Column<string>(type: "TEXT", nullable: true),
                    UsuarioId = table.Column<string>(type: "TEXT", nullable: false),
                    RegistradoUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CobrosVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CobrosVenta_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComandosFinancieros",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Huella = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Resultado = table.Column<string>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    RegistradoUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComandosFinancieros", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "GruposVariantes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposVariantes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LotesEtiquetas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContenidoJson = table.Column<string>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<string>(type: "TEXT", nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImpresoUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ImpresoPor = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotesEtiquetas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MigracionesCostosHistoricos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AlcanceJson = table.Column<string>(type: "TEXT", nullable: false),
                    Respaldo = table.Column<string>(type: "TEXT", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<string>(type: "TEXT", nullable: false),
                    RegistradoUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigracionesCostosHistoricos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosCuenta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Importe = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    FechaEfectiva = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    RegistradoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    RevierteId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Referencia = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosCuenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosCuenta_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosCuenta_MovimientosCuenta_RevierteId",
                        column: x => x.RevierteId,
                        principalTable: "MovimientosCuenta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotasCosto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnidadId = table.Column<int>(type: "INTEGER", nullable: false),
                    Anterior = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Nuevo = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    FechaConocimiento = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    RegistradoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<string>(type: "TEXT", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", nullable: false),
                    Referencia = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasCosto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasCosto_UnidadesInventario_UnidadId",
                        column: x => x.UnidadId,
                        principalTable: "UnidadesInventario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DevolucionesDineroCF",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnulacionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Importe = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Metodo = table.Column<int>(type: "INTEGER", nullable: false),
                    Referencia = table.Column<string>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<string>(type: "TEXT", nullable: false),
                    RegistradoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaEfectiva = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevolucionesDineroCF", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DevolucionesDineroCF_AnulacionesCF_AnulacionId",
                        column: x => x.AnulacionId,
                        principalTable: "AnulacionesCF",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecepcionesDevolucionCF",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnulacionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnidadId = table.Column<int>(type: "INTEGER", nullable: false),
                    Apta = table.Column<bool>(type: "INTEGER", nullable: false),
                    Observacion = table.Column<string>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<string>(type: "TEXT", nullable: false),
                    RegistradoUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecepcionesDevolucionCF", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecepcionesDevolucionCF_AnulacionesCF_AnulacionId",
                        column: x => x.AnulacionId,
                        principalTable: "AnulacionesCF",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecepcionesDevolucionCF_UnidadesInventario_UnidadId",
                        column: x => x.UnidadId,
                        principalTable: "UnidadesInventario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VinculosVariantes",
                columns: table => new
                {
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: false),
                    GrupoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AtributosJson = table.Column<string>(type: "TEXT", nullable: false),
                    Firma = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VinculosVariantes", x => x.ProductoId);
                    table.ForeignKey(
                        name: "FK_VinculosVariantes_GruposVariantes_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "GruposVariantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VinculosVariantes_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorreccionesDetalleCosto",
                columns: table => new
                {
                    NotaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DetalleVentaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorreccionesDetalleCosto", x => new { x.NotaId, x.DetalleVentaId });
                    table.ForeignKey(
                        name: "FK_CorreccionesDetalleCosto_DetallesVenta_DetalleVentaId",
                        column: x => x.DetalleVentaId,
                        principalTable: "DetallesVenta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorreccionesDetalleCosto_NotasCosto_NotaId",
                        column: x => x.NotaId,
                        principalTable: "NotasCosto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pedido_ClienteOCF",
                table: "Pedidos",
                sql: "ClienteId IS NOT NULL OR TipoPedido = 'VentaDirecta'");

            migrationBuilder.CreateIndex(
                name: "IX_AnulacionesCF_VentaId",
                table: "AnulacionesCF",
                column: "VentaId",
                unique: true,
                filter: "Estado IN (1,2)");

            migrationBuilder.CreateIndex(
                name: "IX_CambiosPrecioProducto_ProductoId",
                table: "CambiosPrecioProducto",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_CobrosVenta_VentaId",
                table: "CobrosVenta",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_CorreccionesDetalleCosto_DetalleVentaId",
                table: "CorreccionesDetalleCosto",
                column: "DetalleVentaId");

            migrationBuilder.CreateIndex(
                name: "IX_DevolucionesDineroCF_AnulacionId",
                table: "DevolucionesDineroCF",
                column: "AnulacionId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCuenta_ClienteId_FechaEfectiva",
                table: "MovimientosCuenta",
                columns: new[] { "ClienteId", "FechaEfectiva" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCuenta_ClienteId_Tipo",
                table: "MovimientosCuenta",
                columns: new[] { "ClienteId", "Tipo" },
                unique: true,
                filter: "Tipo = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCuenta_RevierteId",
                table: "MovimientosCuenta",
                column: "RevierteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotasCosto_UnidadId_RegistradoUtc",
                table: "NotasCosto",
                columns: new[] { "UnidadId", "RegistradoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RecepcionesDevolucionCF_AnulacionId_UnidadId",
                table: "RecepcionesDevolucionCF",
                columns: new[] { "AnulacionId", "UnidadId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecepcionesDevolucionCF_UnidadId",
                table: "RecepcionesDevolucionCF",
                column: "UnidadId");

            migrationBuilder.CreateIndex(
                name: "IX_VinculosVariantes_GrupoId_Firma",
                table: "VinculosVariantes",
                columns: new[] { "GrupoId", "Firma" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AperturasCartera");

            migrationBuilder.DropTable(
                name: "CambiosPrecioProducto");

            migrationBuilder.DropTable(
                name: "CobrosVenta");

            migrationBuilder.DropTable(
                name: "ComandosFinancieros");

            migrationBuilder.DropTable(
                name: "CorreccionesDetalleCosto");

            migrationBuilder.DropTable(
                name: "DevolucionesDineroCF");

            migrationBuilder.DropTable(
                name: "LotesEtiquetas");

            migrationBuilder.DropTable(
                name: "MigracionesCostosHistoricos");

            migrationBuilder.DropTable(
                name: "MovimientosCuenta");

            migrationBuilder.DropTable(
                name: "RecepcionesDevolucionCF");

            migrationBuilder.DropTable(
                name: "VinculosVariantes");

            migrationBuilder.DropTable(
                name: "NotasCosto");

            migrationBuilder.DropTable(
                name: "AnulacionesCF");

            migrationBuilder.DropTable(
                name: "GruposVariantes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Pedido_ClienteOCF",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "ApellidoOcasional",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "CanceladaPor",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "CanceladaUtc",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "NombreOcasional",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "RegistradaUtc",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "CostoConocido",
                table: "UnidadesInventario");

            migrationBuilder.DropColumn(
                name: "RegistradoUtc",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "CostoConocido",
                table: "DetallesVenta");

            migrationBuilder.DropColumn(
                name: "CostoConocido",
                table: "DetallesCompra");

            migrationBuilder.AlterColumn<int>(
                name: "ClienteId",
                table: "Pedidos",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CanalVenta",
                table: "Pedidos",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 5);
        }
    }
}
