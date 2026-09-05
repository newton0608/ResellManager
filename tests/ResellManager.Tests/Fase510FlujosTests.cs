using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

public sealed class Fase510FlujosTests
{
    private static readonly DateOnly Fecha = new(2026, 9, 3);

    [Fact]
    public async Task CompraLocal_ReservaVentaPago_ActualizaInventarioSaldoCanalYUtilidad()
    {
        await using var flujo = await FlujoMigrado.CrearAsync();
        var compra = await flujo.ComprarAsync(OrigenCompra.CompraLocal, cantidad: 2);
        var unidades = await flujo.UnidadesAsync();
        Assert.Equal(2, unidades.Count);
        Assert.Equal(2, unidades.Select(x => x.CodigoInterno).Distinct().Count());
        Assert.All(unidades, x =>
        {
            Assert.Equal(EstadoUnidadInventario.Disponible, x.Estado);
            Assert.Equal(Fecha, x.FechaIngreso);
            Assert.StartsWith(compra.CodigoInterno + "-01-", x.CodigoInterno);
        });
        await flujo.VerificarDashboardAsync(0m, 2, 80m, 0, 0m);

        var pedido = await flujo.PedirAsync(TipoPedido.Apartado, CanalVenta.Presencial);
        var unidad = unidades[0];
        Exito(await flujo.EjecutarAsync(db => new InventarioService(db)
            .ReservarAsync(unidad.Id, pedido.Detalles.Single().Id)));
        var reservada = (await flujo.UnidadesAsync()).Single(x => x.Id == unidad.Id);
        Assert.Equal(EstadoUnidadInventario.Disponible, reservada.Estado);
        Assert.Equal(pedido.Id, reservada.PedidoReservaId);
        await flujo.VerificarDashboardAsync(0m, 2, 80m, 1, 0m);

        var venta = await flujo.VenderFisicaAsync(pedido, unidad.Id);
        Assert.Equal(EstadoVenta.Registrada, venta.Estado);
        Assert.Equal(40m, venta.Detalles.Single().CostoUnitario);
        Assert.Equal(EstadoPedido.Completado, (await flujo.PedidoAsync(pedido.Id)).Estado);
        var vendida = (await flujo.UnidadesAsync()).Single(x => x.Id == unidad.Id);
        Assert.Equal(EstadoUnidadInventario.Vendida, vendida.Estado);
        Assert.Null(vendida.DetallePedidoReservaId);
        await flujo.VerificarDashboardAsync(100m, 1, 40m, 0, 60m);
        await flujo.VerificarCanalAsync(CanalVenta.Presencial, 1, 1, 100m);

        var pago = await flujo.PagarAsync(35m);
        await flujo.VerificarDashboardAsync(65m, 1, 40m, 0, 60m);
        var resumen = await flujo.EjecutarAsync(db => new DashboardService(db).ObtenerAsync());
        Assert.Equal(pago.Id, Assert.Single(resumen.UltimosPagos).Id);
        Assert.Equal(venta.Id, Assert.Single(resumen.UltimasVentas).Id);
        Assert.Equal(CanalVenta.Presencial, resumen.UltimasVentas.Single().Canal);
        await flujo.PagarAsync(65m);
        await flujo.VerificarDashboardAsync(0m, 1, 40m, 0, 60m);
    }

    [Fact]
    public async Task Importacion_TransitoRecepcionVenta_PreservaReservaHastaVender()
    {
        await using var flujo = await FlujoMigrado.CrearAsync();
        await flujo.ComprarAsync(OrigenCompra.Importacion);
        var unidad = Assert.Single(await flujo.UnidadesAsync());
        Assert.Equal(EstadoUnidadInventario.Comprada, unidad.Estado);
        Assert.Null(unidad.FechaIngreso);
        await flujo.VerificarDashboardAsync(0m, 0, 0m, 0, 0m);
        var pedido = await flujo.PedirAsync(TipoPedido.Importacion, CanalVenta.WhatsApp);
        Exito(await flujo.EjecutarAsync(db => new InventarioService(db)
            .ReservarAsync(unidad.Id, pedido.Detalles.Single().Id)));
        Exito(await flujo.EjecutarAsync(db => new InventarioService(db)
            .CambiarEstadoAsync(unidad.Id, EstadoUnidadInventario.EnTransito)));
        var enTransito = Assert.Single(await flujo.UnidadesAsync());
        Assert.Equal(EstadoUnidadInventario.EnTransito, enTransito.Estado);
        Assert.Equal(pedido.Id, enTransito.PedidoReservaId);
        Assert.Null(enTransito.FechaIngreso);
        await flujo.VerificarDashboardAsync(0m, 0, 0m, 1, 0m);
        var recibidas = Exito(await flujo.EjecutarAsync(db => new InventarioService(db)
            .RegistrarRecepcionAsync(new RecepcionMercanciaInput(Fecha, [unidad.Id]))));
        Assert.Equal(EstadoUnidadInventario.Disponible, Assert.Single(recibidas).Estado);
        Assert.Equal(Fecha, recibidas.Single().FechaIngreso);
        Assert.Equal(pedido.Id, recibidas.Single().PedidoReservaId);
        Assert.Equal(unidad.CodigoInterno, recibidas.Single().CodigoInterno);
        await flujo.VerificarDashboardAsync(0m, 1, 40m, 1, 0m);
        await flujo.VenderFisicaAsync(pedido, unidad.Id);
        var vendida = Assert.Single(await flujo.UnidadesAsync());
        Assert.Equal(EstadoUnidadInventario.Vendida, vendida.Estado);
        Assert.Null(vendida.DetallePedidoReservaId);
        Assert.Equal(Fecha, vendida.FechaIngreso);
        Assert.Equal(EstadoPedido.Completado, (await flujo.PedidoAsync(pedido.Id)).Estado);
        await flujo.VerificarDashboardAsync(100m, 0, 0m, 0, 60m);
        await flujo.VerificarCanalAsync(CanalVenta.WhatsApp, 1, 1, 100m);
    }

    [Fact]
    public async Task Catalogo_CompraPedidoVentaPago_NoCreaInventarioFisico()
    {
        await using var flujo = await FlujoMigrado.CrearAsync();
        await flujo.ComprarAsync(OrigenCompra.Catalogo);
        Assert.Empty(await flujo.UnidadesAsync());
        var pedido = await flujo.PedirAsync(TipoPedido.Catalogo, CanalVenta.Facebook);
        await flujo.VerificarDashboardAsync(0m, 0, 0m, 1, 0m);
        var venta = await flujo.VenderCatalogoAsync(pedido, 120m, 45m);
        Assert.Null(venta.Detalles.Single().UnidadInventarioId);
        Assert.Equal(45m, venta.Detalles.Single().CostoUnitario);
        Assert.Equal(EstadoPedido.Completado, (await flujo.PedidoAsync(pedido.Id)).Estado);
        await flujo.VerificarDashboardAsync(120m, 0, 0m, 0, 75m);
        await flujo.VerificarCanalAsync(CanalVenta.Facebook, 1, 1, 120m);
        await flujo.PagarAsync(120m);
        Assert.Empty(await flujo.UnidadesAsync());
        Assert.Equal(0, await flujo.EjecutarAsync(db => db.UnidadesInventario.CountAsync()));
        await flujo.VerificarDashboardAsync(0m, 0, 0m, 0, 75m);
    }

    [Fact]
    public async Task Apartado_MultiplesProductos_LiberaYCancelaSinAlterarEstadoFisico()
    {
        await using var flujo = await FlujoMigrado.CrearAsync();
        var segundo = await flujo.CrearProductoAsync("SKU-SEGUNDO");
        await flujo.ComprarAsync(OrigenCompra.Importacion, cantidad: 2);
        await flujo.ComprarAsync(OrigenCompra.CompraLocal, productoId: segundo.Id);
        var unidades = (await flujo.UnidadesAsync()).OrderBy(x => x.Id).ToArray();
        Exito(await flujo.EjecutarAsync(db => new InventarioService(db)
            .CambiarEstadoAsync(unidades[1].Id, EstadoUnidadInventario.EnTransito)));
        var pedido = Exito(await flujo.EjecutarAsync(db => new PedidoService(db).CrearAsync(
            new PedidoInput(CodigosInternos.CrearCodigoPedido(), Fecha, TipoPedido.Apartado,
                CanalVenta.Otro, flujo.Cliente.Id, null,
                [new(flujo.Producto.Id, 2, 100m, null), new(segundo.Id, 1, 100m, null)]))));
        var antes = (await flujo.UnidadesAsync()).ToDictionary(x => x.Id);
        foreach (var unidad in unidades)
        {
            var detalle = pedido.Detalles.Single(x => x.ProductoId == unidad.ProductoId);
            Exito(await flujo.EjecutarAsync(db => new InventarioService(db)
                .ReservarAsync(unidad.Id, detalle.Id)));
        }
        Assert.All(await flujo.UnidadesAsync(), x => Assert.Equal(pedido.Id, x.PedidoReservaId));
        await flujo.VerificarDashboardAsync(0m, 1, 40m, 1, 0m);
        Exito(await flujo.EjecutarAsync(db => new InventarioService(db)
            .CancelarReservaAsync(unidades[0].Id)));
        Assert.Null((await flujo.UnidadesAsync()).Single(x => x.Id == unidades[0].Id).PedidoReservaId);
        Assert.Equal(EstadoPedido.Pendiente, (await flujo.PedidoAsync(pedido.Id)).Estado);
        Exito(await flujo.EjecutarAsync(db => new InventarioService(db)
            .ReservarAsync(unidades[0].Id, pedido.Detalles.Single(x => x.ProductoId == flujo.Producto.Id).Id)));
        Exito(await flujo.EjecutarAsync(db => new PedidoService(db).CancelarAsync(pedido.Id)));
        Assert.Equal(EstadoPedido.Cancelado, (await flujo.PedidoAsync(pedido.Id)).Estado);
        Assert.All(await flujo.UnidadesAsync(), x =>
        {
            Assert.Null(x.DetallePedidoReservaId);
            Assert.Null(x.PedidoReservaId);
            Assert.Equal(antes[x.Id].Estado, x.Estado);
            Assert.Equal(antes[x.Id].FechaIngreso, x.FechaIngreso);
            Assert.Equal(antes[x.Id].CodigoInterno, x.CodigoInterno);
        });
        await flujo.VerificarDashboardAsync(0m, 1, 40m, 0, 0m);
        await flujo.VerificarCanalAsync(CanalVenta.Otro, 0, 0, 0m);
        // La unidad liberada puede reservarse para otro pedido con el mismo modelo.
        var nuevo = await flujo.PedirAsync(TipoPedido.Apartado, CanalVenta.Presencial);
        Exito(await flujo.EjecutarAsync(db => new InventarioService(db)
            .ReservarAsync(unidades[0].Id, nuevo.Detalles.Single().Id)));
        Assert.Equal(EstadoUnidadInventario.Comprada,
            (await flujo.UnidadesAsync()).Single(x => x.Id == unidades[0].Id).Estado);
    }

    [Fact]
    public async Task CancelacionAntesDeEntrega_RecalculaSaldoGlobalYDashboardSinRestaurarReserva()
    {
        await using var flujo = await FlujoMigrado.CrearAsync();
        await flujo.ComprarAsync(OrigenCompra.CompraLocal);
        var unidad = Assert.Single(await flujo.UnidadesAsync());
        var pedido = await flujo.PedirAsync(TipoPedido.Apartado, CanalVenta.Presencial);
        Exito(await flujo.EjecutarAsync(db => new InventarioService(db)
            .ReservarAsync(unidad.Id, pedido.Detalles.Single().Id)));
        var venta = await flujo.VenderFisicaAsync(pedido, unidad.Id);
        var otroPedido = await flujo.PedirAsync(TipoPedido.Catalogo, CanalVenta.Web);
        var otraVenta = await flujo.VenderCatalogoAsync(otroPedido, 200m, 80m);
        await flujo.PagarAsync(50m);
        await flujo.VerificarDashboardAsync(250m, 0, 0m, 0, 180m);

        Exito(await flujo.EjecutarAsync(db => new VentaService(db).CancelarAsync(venta.Id)));
        Assert.Equal(EstadoVenta.Cancelada, (await flujo.VentaAsync(venta.Id)).Estado);
        Assert.Equal(EstadoPedido.Pendiente, (await flujo.PedidoAsync(pedido.Id)).Estado);
        var liberada = Assert.Single(await flujo.UnidadesAsync());
        Assert.Equal(EstadoUnidadInventario.Disponible, liberada.Estado);
        Assert.Null(liberada.DetallePedidoReservaId);
        Assert.Equal(unidad.FechaIngreso, liberada.FechaIngreso);
        await flujo.VerificarDashboardAsync(150m, 1, 40m, 1, 120m);
        await flujo.VerificarCanalAsync(CanalVenta.Presencial, 1, 0, 0m);
        await flujo.VerificarCanalAsync(CanalVenta.Web, 1, 1, 200m);
        var dashboard = await flujo.EjecutarAsync(db => new DashboardService(db).ObtenerAsync());
        Assert.Equal(otraVenta.Id, Assert.Single(dashboard.UltimasVentas).Id);
        Assert.Single(dashboard.UltimosPagos);

        // Cancelar de nuevo no duplica inventario ni modifica los pagos globales.
        Exito(await flujo.EjecutarAsync(db => new VentaService(db).CancelarAsync(venta.Id)));
        await flujo.VerificarDashboardAsync(150m, 1, 40m, 1, 120m);
    }

    [Fact]
    public async Task CancelacionConUnidadEntregada_SeRechazaSinCambiosPersistidos()
    {
        await using var flujo = await FlujoMigrado.CrearAsync();
        await flujo.ComprarAsync(OrigenCompra.CompraLocal);
        var unidad = Assert.Single(await flujo.UnidadesAsync());
        var pedido = await flujo.PedirAsync(TipoPedido.Apartado, CanalVenta.Presencial);
        var venta = await flujo.VenderFisicaAsync(pedido, unidad.Id);
        Exito(await flujo.EjecutarAsync(db => new InventarioService(db)
            .CambiarEstadoAsync(unidad.Id, EstadoUnidadInventario.Entregada)));
        var resultado = await flujo.EjecutarAsync(db => new VentaService(db).CancelarAsync(venta.Id));
        Assert.False(resultado.IsSuccess);
        Assert.Contains("entregadas", resultado.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(EstadoVenta.Registrada, (await flujo.VentaAsync(venta.Id)).Estado);
        Assert.Equal(EstadoPedido.Completado, (await flujo.PedidoAsync(pedido.Id)).Estado);
        Assert.Equal(EstadoUnidadInventario.Entregada, Assert.Single(await flujo.UnidadesAsync()).Estado);
        await flujo.VerificarDashboardAsync(100m, 0, 0m, 0, 60m);
        await flujo.VerificarCanalAsync(CanalVenta.Presencial, 1, 1, 100m);
    }

    [Fact]
    public async Task CancelacionQueDejariaSaldoNegativo_ConservaVentaInventarioYPagos()
    {
        await using var flujo = await FlujoMigrado.CrearAsync();
        await flujo.ComprarAsync(OrigenCompra.CompraLocal);
        var unidad = Assert.Single(await flujo.UnidadesAsync());
        var pedido = await flujo.PedirAsync(TipoPedido.Apartado, CanalVenta.Presencial);
        var venta = await flujo.VenderFisicaAsync(pedido, unidad.Id);
        await flujo.PagarAsync(30m);
        var resultado = await flujo.EjecutarAsync(db => new VentaService(db).CancelarAsync(venta.Id));
        Assert.False(resultado.IsSuccess);
        Assert.Contains("pagos", resultado.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(EstadoVenta.Registrada, (await flujo.VentaAsync(venta.Id)).Estado);
        Assert.Equal(EstadoPedido.Completado, (await flujo.PedidoAsync(pedido.Id)).Estado);
        Assert.Equal(EstadoUnidadInventario.Vendida, Assert.Single(await flujo.UnidadesAsync()).Estado);
        Assert.Equal(30m, Assert.Single(await flujo.EjecutarAsync(db => new PagoService(db)
            .ListarPorClienteAsync(flujo.Cliente.Id))).Monto);
        await flujo.VerificarDashboardAsync(70m, 0, 0m, 0, 60m);
        await flujo.VerificarCanalAsync(CanalVenta.Presencial, 1, 1, 100m);
    }

    private static T Exito<T>(ServiceResult<T> resultado)
    {
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        return resultado.Value!;
    }

    private static void Exito(ServiceResult resultado) =>
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);

    // Cada operación y lectura usa un contexto nuevo: los asserts observan SQLite,
    // no entidades que permanezcan en el ChangeTracker de la operación anterior.
    private sealed class FlujoMigrado(SqliteConnection connection) : IAsyncDisposable
    {
        public ClienteDto Cliente { get; private set; } = null!;
        public CategoriaDto Categoria { get; private set; } = null!;
        public ProductoDto Producto { get; private set; } = null!;
        public ProveedorDto Proveedor { get; private set; } = null!;

        public static async Task<FlujoMigrado> CrearAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var flujo = new FlujoMigrado(connection);
            try
            {
                await flujo.EjecutarAsync(async db =>
                {
                    await db.Database.MigrateAsync();
                    Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
                    Assert.Empty(await db.Database.GetPendingMigrationsAsync());
                    Assert.False(db.Database.HasPendingModelChanges());
                    return true;
                });
                flujo.Proveedor = Exito(await flujo.EjecutarAsync(db => new ProveedorService(db)
                    .CrearAsync(new ProveedorInput("Proveedor de prueba V1", null, "GT", null))));
                flujo.Categoria = Exito(await flujo.EjecutarAsync(db => new CategoriaService(db)
                    .CrearAsync(new CategoriaInput("Categoría V1", null))));
                flujo.Producto = await flujo.CrearProductoAsync("SKU-EXTERNO-V1");
                flujo.Cliente = Exito(await flujo.EjecutarAsync(db => new ClienteService(db)
                    .CrearAsync(new ClienteInput("Cliente de prueba V1", null, "55550100", null, null))));
                return flujo;
            }
            catch
            {
                await flujo.DisposeAsync();
                throw;
            }
        }

        public async Task<T> EjecutarAsync<T>(Func<ResellManagerDbContext, Task<T>> operacion)
        {
            var options = new DbContextOptionsBuilder<ResellManagerDbContext>()
                .UseSqlite(connection).Options;
            await using var db = new ResellManagerDbContext(options);
            return await operacion(db);
        }

        public async Task<ProductoDto> CrearProductoAsync(string codigo) =>
            Exito(await EjecutarAsync(db => new ProductoService(db).CrearAsync(new ProductoInput(
                codigo, null, "Producto " + codigo, null, null, null, null, null, 100m, Categoria.Id))));

        public async Task<CompraDto> ComprarAsync(OrigenCompra origen, int cantidad = 1, int? productoId = null) =>
            Exito(await EjecutarAsync(db => new CompraService(db).RegistrarAsync(new CompraInput(
                CodigosInternos.CrearCodigoCompra(), Fecha,
                origen == OrigenCompra.CompraLocal ? Fecha : null, origen, Proveedor.Id, null,
                [new DetalleCompraInput(productoId ?? Producto.Id, cantidad, 40m)], null))));

        public async Task<PedidoDto> PedirAsync(TipoPedido tipo, CanalVenta canal) =>
            Exito(await EjecutarAsync(db => new PedidoService(db).CrearAsync(new PedidoInput(
                CodigosInternos.CrearCodigoPedido(), Fecha, tipo, canal, Cliente.Id, null,
                [new DetallePedidoInput(Producto.Id, 1, 100m, null)]))));

        public async Task<VentaDto> VenderFisicaAsync(PedidoDto pedido, int unidadId) =>
            Exito(await EjecutarAsync(db => new VentaService(db).RegistrarDesdePedidoAsync(new VentaInput(
                pedido.Id, CodigosInternos.CrearCodigoVenta(), Fecha, null,
                [new DetalleVentaInput(unidadId, null, null, 100m, null)]))));

        public async Task<VentaDto> VenderCatalogoAsync(PedidoDto pedido, decimal precio, decimal costo) =>
            Exito(await EjecutarAsync(db => new VentaService(db).RegistrarDesdePedidoAsync(new VentaInput(
                pedido.Id, CodigosInternos.CrearCodigoVenta(), Fecha, null,
                [new DetalleVentaInput(null, Producto.Id, costo, precio, null)]))));

        public async Task<PagoDto> PagarAsync(decimal monto) =>
            Exito(await EjecutarAsync(db => new PagoService(db).RegistrarAsync(new PagoInput(
                Cliente.Id, Fecha, monto, MetodoPago.Transferencia, "Referencia externa V1", null))));

        public Task<IReadOnlyList<UnidadInventarioDto>> UnidadesAsync() =>
            EjecutarAsync(db => new InventarioService(db).ListarAsync());

        public async Task<PedidoDto> PedidoAsync(int id) =>
            Exito(await EjecutarAsync(db => new PedidoService(db).ObtenerPorIdAsync(id)));

        public async Task<VentaDto> VentaAsync(int id) =>
            Exito(await EjecutarAsync(db => new VentaService(db).ObtenerPorIdAsync(id)));

        public async Task VerificarDashboardAsync(decimal saldo, int disponibles, decimal valor,
            int pedidosActivos, decimal utilidad)
        {
            var dashboard = await EjecutarAsync(db => new DashboardService(db).ObtenerAsync());
            Assert.Equal(saldo, dashboard.TotalAdeudado);
            Assert.Equal(disponibles, dashboard.UnidadesDisponibles);
            Assert.Equal(valor, dashboard.ValorInventarioDisponible);
            Assert.Equal(pedidosActivos, dashboard.PedidosActivos);
            Assert.Equal(saldo, Exito(await EjecutarAsync(db => new ClienteService(db)
                .ObtenerSaldoAsync(Cliente.Id))));
            Assert.Equal(utilidad, Exito(await EjecutarAsync(db => new DashboardService(db)
                .ObtenerUtilidadAsync(Fecha, Fecha))));
        }

        public async Task VerificarCanalAsync(CanalVenta canal, int pedidos, int ventas, decimal monto)
        {
            var dashboard = await EjecutarAsync(db => new DashboardService(db).ObtenerAsync());
            Assert.Equal(new ResumenCanalVentaDto(canal, pedidos, ventas, monto),
                dashboard.Canales.Single(x => x.Canal == canal));
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
