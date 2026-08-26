using Microsoft.EntityFrameworkCore;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

public sealed class Fase57VentaFisicaTests
{
    [Fact]
    public async Task RegistrarVentaFisica_CompletaPedidoVendeUnidadLiberaReservaYConservaCosto()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("COMPRA-FISICA");
        var pedido = await test.CrearPedidoAsync(TipoPedido.VentaDirecta, "PED-FISICA");
        var reserva = await new InventarioService(test.Db).ReservarAsync(
            unidad.Id,
            pedido.Detalles.Single().Id);

        var resultado = await new VentaService(test.Db).RegistrarDesdePedidoAsync(
            Fisica(pedido.Id, "VEN-FISICA", unidad.Id));

        Assert.True(reserva.IsSuccess, reserva.ErrorMessage);
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        await test.Db.Entry(unidad).ReloadAsync();
        await test.Db.Entry(pedido).ReloadAsync();
        Assert.Equal(EstadoUnidadInventario.Vendida, unidad.Estado);
        Assert.Null(unidad.DetallePedidoReservaId);
        Assert.Equal(EstadoPedido.Completado, pedido.Estado);
        Assert.Equal(unidad.Costo, resultado.Value!.Detalles.Single().CostoUnitario);
    }

    [Fact]
    public async Task VentaFisica_ExigeProductoYCantidadExactos()
    {
        await using var test = await TestDatabase.CreateAsync();
        var productoB = await test.CrearProductoAsync("PROD-FIS-B");
        var unidadA1 = await test.CrearUnidadDisponibleAsync("COMPRA-A1");
        var unidadA2 = await test.CrearUnidadDisponibleAsync("COMPRA-A2");
        var unidadB = await test.CrearUnidadDisponibleAsync("COMPRA-B", productoB);
        var pedido = new Pedido
        {
            CodigoInterno = "PED-FIS-EXACTO",
            Fecha = new DateOnly(2026, 8, 1),
            TipoPedido = TipoPedido.VentaDirecta,
            Estado = EstadoPedido.Pendiente,
            ClienteId = test.Cliente.Id,
            Detalles =
            [
                new DetallePedido { ProductoId = test.Producto.Id, Cantidad = 2, PrecioUnitario = 110m },
                new DetallePedido { ProductoId = productoB.Id, Cantidad = 1, PrecioUnitario = 90m },
            ],
        };
        test.Db.Pedidos.Add(pedido);
        await test.Db.SaveChangesAsync();

        var resultado = await new VentaService(test.Db).RegistrarDesdePedidoAsync(
            new VentaInput(
                pedido.Id,
                "VEN-FIS-EXACTO",
                new DateOnly(2026, 8, 2),
                null,
                [Fisico(unidadA1.Id, 110m), Fisico(unidadA2.Id, 110m), Fisico(unidadB.Id, 90m)]));

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal(2, resultado.Value!.Detalles.Count(x => x.ProductoId == test.Producto.Id));
        Assert.Single(resultado.Value.Detalles.Where(x => x.ProductoId == productoB.Id));
    }

    [Fact]
    public async Task VentaFisica_RechazaUnidadDuplicada()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("COMPRA-DUP");
        var pedido = await test.CrearPedidoAsync(TipoPedido.VentaDirecta, "PED-DUP", 2);

        var resultado = await new VentaService(test.Db).RegistrarDesdePedidoAsync(
            new VentaInput(
                pedido.Id,
                "VEN-DUP",
                new DateOnly(2026, 8, 2),
                null,
                [Fisico(unidad.Id), Fisico(unidad.Id)]));

        Assert.False(resultado.IsSuccess);
        Assert.Contains("repetirse", resultado.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(EstadoUnidadInventario.Comprada)]
    [InlineData(EstadoUnidadInventario.EnTransito)]
    [InlineData(EstadoUnidadInventario.Vendida)]
    [InlineData(EstadoUnidadInventario.Entregada)]
    public async Task VentaFisica_RechazaUnidadQueNoEstaDisponible(EstadoUnidadInventario estado)
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync($"COMPRA-{estado}");
        unidad.Estado = estado;
        await test.Db.SaveChangesAsync();
        var pedido = await test.CrearPedidoAsync(TipoPedido.VentaDirecta, $"PED-{estado}");

        var resultado = await new VentaService(test.Db).RegistrarDesdePedidoAsync(
            Fisica(pedido.Id, $"VEN-{estado}", unidad.Id));

        Assert.False(resultado.IsSuccess);
        Assert.Contains("Solo se pueden vender unidades disponibles", resultado.ErrorMessage!);
    }

    [Fact]
    public async Task VentaFisica_RechazaReservaDeOtroPedido()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("COMPRA-RES-OTRO");
        var pedidoVenta = await test.CrearPedidoAsync(TipoPedido.VentaDirecta, "PED-VENTA");
        var pedidoReserva = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-RESERVA");
        var reserva = await new InventarioService(test.Db).ReservarAsync(
            unidad.Id,
            pedidoReserva.Detalles.Single().Id);

        var resultado = await new VentaService(test.Db).RegistrarDesdePedidoAsync(
            Fisica(pedidoVenta.Id, "VEN-RES-OTRO", unidad.Id));

        Assert.True(reserva.IsSuccess, reserva.ErrorMessage);
        Assert.False(resultado.IsSuccess);
        Assert.Contains("pedido distinto", resultado.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VentaCatalogo_RequiereProductoYCostoSinUnidad()
    {
        await using var test = await TestDatabase.CreateAsync();
        var pedidoSinProducto = await test.CrearPedidoAsync(TipoPedido.Catalogo, "PED-CAT-PROD");
        var pedidoSinCosto = await test.CrearPedidoAsync(TipoPedido.Catalogo, "PED-CAT-COSTO");
        var service = new VentaService(test.Db);

        var sinProducto = await service.RegistrarDesdePedidoAsync(
            new VentaInput(pedidoSinProducto.Id, "VEN-CAT-PROD", new DateOnly(2026, 8, 2), null,
                [new DetalleVentaInput(null, null, 40m, 100m, null)]));
        var sinCosto = await service.RegistrarDesdePedidoAsync(
            new VentaInput(pedidoSinCosto.Id, "VEN-CAT-COSTO", new DateOnly(2026, 8, 2), null,
                [new DetalleVentaInput(null, test.Producto.Id, null, 100m, null)]));

        Assert.False(sinProducto.IsSuccess);
        Assert.False(sinCosto.IsSuccess);
        Assert.Contains("producto y costo", sinProducto.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("producto y costo", sinCosto.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Venta_RechazaCodigoRepetido()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearVentaCatalogoAsync("PED-COD-1", "VEN-COD-REPETIDO");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Catalogo, "PED-COD-2");

        var resultado = await new VentaService(test.Db).RegistrarDesdePedidoAsync(
            Catalogo(pedido.Id, "VEN-COD-REPETIDO", test.Producto.Id));

        Assert.False(resultado.IsSuccess);
        Assert.Contains("código de venta", resultado.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Venta_RechazaPedidoCanceladoYPedidoConVentaPrevia()
    {
        await using var test = await TestDatabase.CreateAsync();
        var cancelado = await test.CrearPedidoAsync(TipoPedido.Catalogo, "PED-CANCELADO");
        cancelado.Estado = EstadoPedido.Cancelado;
        await test.Db.SaveChangesAsync();
        var vendido = await test.CrearPedidoAsync(TipoPedido.Catalogo, "PED-YA-VENDIDO");
        var service = new VentaService(test.Db);
        var primera = await service.RegistrarDesdePedidoAsync(
            Catalogo(vendido.Id, "VEN-PRIMERA", test.Producto.Id));

        var desdeCancelado = await service.RegistrarDesdePedidoAsync(
            Catalogo(cancelado.Id, "VEN-CANCELADO", test.Producto.Id));
        var repetida = await service.RegistrarDesdePedidoAsync(
            Catalogo(vendido.Id, "VEN-SEGUNDA", test.Producto.Id));

        Assert.True(primera.IsSuccess, primera.ErrorMessage);
        Assert.False(desdeCancelado.IsSuccess);
        Assert.False(repetida.IsSuccess);
        Assert.Contains("cancelado", desdeCancelado.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ya tiene una venta", repetida.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    private static VentaInput Fisica(int pedidoId, string codigo, int unidadId) =>
        new(pedidoId, codigo, new DateOnly(2026, 8, 2), null, [Fisico(unidadId)]);

    private static DetalleVentaInput Fisico(int unidadId, decimal precio = 100m) =>
        new(unidadId, null, null, precio, null);

    private static VentaInput Catalogo(int pedidoId, string codigo, int productoId) =>
        new(pedidoId, codigo, new DateOnly(2026, 8, 2), null,
            [new DetalleVentaInput(null, productoId, 40m, 100m, null)]);
}

public sealed class Fase57CancelacionVentaTests
{
    [Fact]
    public async Task CancelarVentaFisica_CancelaVentaLiberaUnidadDevuelvePedidoYNoRestauraReserva()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("COMPRA-CANCELAR");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-CANCELAR");
        var inventario = new InventarioService(test.Db);
        var reserva = await inventario.ReservarAsync(unidad.Id, pedido.Detalles.Single().Id);
        var ventaService = new VentaService(test.Db);
        var venta = await ventaService.RegistrarDesdePedidoAsync(
            new VentaInput(pedido.Id, "VEN-CANCELAR", new DateOnly(2026, 8, 2), null,
                [new DetalleVentaInput(unidad.Id, null, null, 100m, null)]));

        var cancelacion = await ventaService.CancelarAsync(venta.Value!.Id);

        Assert.True(reserva.IsSuccess, reserva.ErrorMessage);
        Assert.True(cancelacion.IsSuccess, cancelacion.ErrorMessage);
        var ventaEntidad = await test.Db.Ventas.SingleAsync(x => x.Id == venta.Value.Id);
        await test.Db.Entry(unidad).ReloadAsync();
        await test.Db.Entry(pedido).ReloadAsync();
        Assert.Equal(EstadoVenta.Cancelada, ventaEntidad.Estado);
        Assert.Equal(EstadoUnidadInventario.Disponible, unidad.Estado);
        Assert.Null(unidad.DetallePedidoReservaId);
        Assert.Equal(EstadoPedido.Pendiente, pedido.Estado);
    }

    [Fact]
    public async Task CancelarVentaYaCancelada_EsIdempotente()
    {
        await using var test = await TestDatabase.CreateAsync();
        var venta = await test.CrearVentaCatalogoAsync("PED-IDEM", "VEN-IDEM");
        var service = new VentaService(test.Db);

        var primera = await service.CancelarAsync(venta.Id);
        var segunda = await service.CancelarAsync(venta.Id);

        Assert.True(primera.IsSuccess, primera.ErrorMessage);
        Assert.True(segunda.IsSuccess, segunda.ErrorMessage);
        await test.Db.Entry(venta).ReloadAsync();
        Assert.Equal(EstadoVenta.Cancelada, venta.Estado);
    }
}

public sealed class Fase57PagoTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Pago_DebeSerMayorQueCero(decimal monto)
    {
        await using var test = await TestDatabase.CreateAsync();

        var resultado = await new PagoService(test.Db).RegistrarAsync(
            new PagoInput(test.Cliente.Id, new DateOnly(2026, 8, 3), monto,
                MetodoPago.Efectivo, null, null));

        Assert.False(resultado.IsSuccess);
        Assert.Contains("mayor que cero", resultado.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Pago_RequiereClienteExistente()
    {
        await using var test = await TestDatabase.CreateAsync();

        var resultado = await new PagoService(test.Db).RegistrarAsync(
            new PagoInput(int.MaxValue, new DateOnly(2026, 8, 3), 10m,
                MetodoPago.Transferencia, null, null));

        Assert.False(resultado.IsSuccess);
        Assert.Contains("Cliente no encontrado", resultado.ErrorMessage!);
    }

    [Fact]
    public async Task AbonoParcial_DisminuyeSaldoCalculadoPorBackend()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearVentaCatalogoAsync("PED-ABONO", "VEN-ABONO", 100m);

        var pago = await new PagoService(test.Db).RegistrarAsync(
            new PagoInput(test.Cliente.Id, new DateOnly(2026, 8, 3), 35m,
                MetodoPago.Transferencia, "TRX-35", "Abono parcial"));
        var saldo = await new ClienteService(test.Db).ObtenerSaldoAsync(test.Cliente.Id);

        Assert.True(pago.IsSuccess, pago.ErrorMessage);
        Assert.Equal(65m, saldo.Value);
        Assert.Equal(test.Cliente.Id, pago.Value!.ClienteId);
    }

    [Fact]
    public async Task MultiplesPagos_ActualizanSaldoYElHistorialConservaOrdenBackend()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearVentaCatalogoAsync("PED-MULTIPAGO", "VEN-MULTIPAGO", 100m);
        var service = new PagoService(test.Db);
        var primero = await service.RegistrarAsync(
            new PagoInput(test.Cliente.Id, new DateOnly(2026, 8, 3), 20m,
                MetodoPago.Efectivo, "P-1", null));
        var segundo = await service.RegistrarAsync(
            new PagoInput(test.Cliente.Id, new DateOnly(2026, 8, 5), 30m,
                MetodoPago.Deposito, "P-2", null));

        var historial = await service.ListarPorClienteAsync(test.Cliente.Id);
        var saldo = await new ClienteService(test.Db).ObtenerSaldoAsync(test.Cliente.Id);

        Assert.True(primero.IsSuccess, primero.ErrorMessage);
        Assert.True(segundo.IsSuccess, segundo.ErrorMessage);
        Assert.Equal(50m, saldo.Value);
        Assert.Equal(["P-2", "P-1"], historial.Select(x => x.Referencia));
    }

    [Fact]
    public async Task Pago_PerteneceAlClienteYNoAUnaVenta()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearVentaCatalogoAsync("PED-GLOBAL", "VEN-GLOBAL", 100m);
        var resultado = await new PagoService(test.Db).RegistrarAsync(
            new PagoInput(test.Cliente.Id, new DateOnly(2026, 8, 3), 10m,
                MetodoPago.Otro, null, null));

        var entidad = await test.Db.Pagos.SingleAsync();

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal(test.Cliente.Id, entidad.ClienteId);
        Assert.Null(typeof(Pago).GetProperty("VentaId"));
    }
}
