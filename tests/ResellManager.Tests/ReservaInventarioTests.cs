using Microsoft.EntityFrameworkCore;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

public sealed class ReservaInventarioTests
{
    [Fact]
    public async Task UnidadEnTransito_PuedeTenerReserva()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadImportadaAsync("IMP-TRANSITO-RES");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-TRANSITO-RES");
        var service = new InventarioService(test.Db);
        var transito = await service.CambiarEstadoAsync(
            unidad.Id,
            EstadoUnidadInventario.EnTransito
        );

        var reserva = await service.ReservarAsync(unidad.Id, pedido.Detalles.Single().Id);

        Assert.True(transito.IsSuccess, transito.ErrorMessage);
        Assert.True(reserva.IsSuccess, reserva.ErrorMessage);
        Assert.Equal(EstadoUnidadInventario.EnTransito, reserva.Value!.Estado);
        Assert.Equal(pedido.Detalles.Single().Id, reserva.Value.DetallePedidoReservaId);
    }

    [Fact]
    public async Task UnidadDisponible_PuedeTenerReserva()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("LOC-DISP-RES");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-DISP-RES");

        var reserva = await new InventarioService(test.Db).ReservarAsync(
            unidad.Id,
            pedido.Detalles.Single().Id
        );

        Assert.True(reserva.IsSuccess, reserva.ErrorMessage);
        Assert.Equal(EstadoUnidadInventario.Disponible, reserva.Value!.Estado);
        Assert.Equal(pedido.Id, reserva.Value.PedidoReservaId);
        Assert.Equal(test.Cliente.Id, reserva.Value.ClienteReservaId);
    }

    [Fact]
    public async Task CancelarReserva_ConservaEstadoFisico()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("LOC-CANCELA-RES");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-CANCELA-RES");
        var service = new InventarioService(test.Db);
        var reserva = await service.ReservarAsync(unidad.Id, pedido.Detalles.Single().Id);

        var cancelacion = await service.CancelarReservaAsync(unidad.Id);

        Assert.True(reserva.IsSuccess, reserva.ErrorMessage);
        Assert.True(cancelacion.IsSuccess, cancelacion.ErrorMessage);
        Assert.Equal(EstadoUnidadInventario.Disponible, cancelacion.Value!.Estado);
        Assert.Null(cancelacion.Value.DetallePedidoReservaId);
    }

    [Fact]
    public async Task UnidadReservada_NoPuedeVenderseAOtroPedido()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("LOC-RES-OTRO");
        var pedidoReserva = await test.CrearPedidoAsync(
            TipoPedido.Apartado,
            "PED-RESERVA-OTRO"
        );
        var pedidoVenta = await test.CrearPedidoAsync(TipoPedido.VentaDirecta, "PED-VENTA-OTRO");
        var inventario = new InventarioService(test.Db);
        var reserva = await inventario.ReservarAsync(
            unidad.Id,
            pedidoReserva.Detalles.Single().Id
        );

        var venta = await new VentaService(test.Db).RegistrarDesdePedidoAsync(
            new VentaInput(
                pedidoVenta.Id,
                "VEN-RES-OTRO",
                new DateOnly(2026, 2, 2),
                null,
                [new DetalleVentaInput(unidad.Id, null, null, 100m, null)]
            )
        );

        Assert.True(reserva.IsSuccess, reserva.ErrorMessage);
        Assert.False(venta.IsSuccess);
        Assert.Contains("reservada", venta.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        await test.Db.Entry(unidad).ReloadAsync();
        Assert.Equal(EstadoUnidadInventario.Disponible, unidad.Estado);
        Assert.Equal(pedidoReserva.Detalles.Single().Id, unidad.DetallePedidoReservaId);
    }

    [Fact]
    public async Task UnidadReservada_PuedeVenderseASuPedido()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("LOC-RES-PROPIO");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-RES-PROPIO");
        var inventario = new InventarioService(test.Db);
        var reserva = await inventario.ReservarAsync(unidad.Id, pedido.Detalles.Single().Id);

        var venta = await new VentaService(test.Db).RegistrarDesdePedidoAsync(
            new VentaInput(
                pedido.Id,
                "VEN-RES-PROPIO",
                new DateOnly(2026, 2, 2),
                null,
                [new DetalleVentaInput(unidad.Id, null, null, 100m, null)]
            )
        );

        Assert.True(reserva.IsSuccess, reserva.ErrorMessage);
        Assert.True(venta.IsSuccess, venta.ErrorMessage);
        await test.Db.Entry(unidad).ReloadAsync();
        Assert.Equal(EstadoUnidadInventario.Vendida, unidad.Estado);
        Assert.Null(unidad.DetallePedidoReservaId);
    }
}
