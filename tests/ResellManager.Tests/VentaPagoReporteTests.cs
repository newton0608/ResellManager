using Microsoft.EntityFrameworkCore;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

public sealed class VentaTests
{
    [Fact]
    public async Task VentaCatalogo_SeRegistraSinUnidadInventario()
    {
        await using var test = await TestDatabase.CreateAsync();
        var pedido = await test.CrearPedidoAsync(TipoPedido.Catalogo, "PED-CAT");

        var result = await new VentaService(test.Db).RegistrarDesdePedidoAsync(
            new VentaInput(
                pedido.Id,
                "VEN-CAT",
                new DateOnly(2026, 2, 2),
                null,
                [new DetalleVentaInput(null, test.Producto.Id, 40m, 100m, null)]
            )
        );

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var detalle = await test.Db.DetallesVenta.SingleAsync();
        Assert.Null(detalle.UnidadInventarioId);
        Assert.Equal(test.Producto.Id, detalle.ProductoId);
    }

    [Fact]
    public async Task VentaNoCatalogo_RechazaDetalleSinUnidadInventario()
    {
        await using var test = await TestDatabase.CreateAsync();
        var pedido = await test.CrearPedidoAsync(TipoPedido.VentaDirecta, "PED-SIN-UNI");

        var result = await new VentaService(test.Db).RegistrarDesdePedidoAsync(
            new VentaInput(
                pedido.Id,
                "VEN-SIN-UNI",
                new DateOnly(2026, 2, 2),
                null,
                [new DetalleVentaInput(null, test.Producto.Id, 40m, 100m, null)]
            )
        );

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task UnidadEnVentaRegistrada_NoPuedeVenderseOtraVez()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("CL-DOBLE");
        var primerPedido = await test.CrearPedidoAsync(TipoPedido.VentaDirecta, "PED-1");
        var segundoPedido = await test.CrearPedidoAsync(TipoPedido.VentaDirecta, "PED-2");
        var service = new VentaService(test.Db);
        var detalle = new DetalleVentaInput(unidad.Id, null, null, 100m, null);

        var primera = await service.RegistrarDesdePedidoAsync(
            new VentaInput(primerPedido.Id, "VEN-1", new DateOnly(2026, 2, 2), null, [detalle])
        );
        var segunda = await service.RegistrarDesdePedidoAsync(
            new VentaInput(segundoPedido.Id, "VEN-2", new DateOnly(2026, 2, 3), null, [detalle])
        );

        Assert.True(primera.IsSuccess, primera.ErrorMessage);
        Assert.False(segunda.IsSuccess);
    }

    [Fact]
    public async Task VentaCanceladaAntesDeEntrega_LiberaUnidad()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("CL-CANCELA");
        var pedido = await test.CrearPedidoAsync(TipoPedido.VentaDirecta, "PED-CANCELA");
        var service = new VentaService(test.Db);
        var venta = await service.RegistrarDesdePedidoAsync(
            new VentaInput(
                pedido.Id,
                "VEN-CANCELA",
                new DateOnly(2026, 2, 2),
                null,
                [new DetalleVentaInput(unidad.Id, null, null, 100m, null)]
            )
        );

        var cancelacion = await service.CancelarAsync(venta.Value!.Id);

        Assert.True(cancelacion.IsSuccess, cancelacion.ErrorMessage);
        await test.Db.Entry(unidad).ReloadAsync();
        Assert.Equal(EstadoUnidadInventario.Disponible, unidad.Estado);
    }

    [Fact]
    public async Task VentaEntregada_NoPuedeCancelarsePorFlujoSimple()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("CL-ENTREGA");
        var pedido = await test.CrearPedidoAsync(TipoPedido.VentaDirecta, "PED-ENTREGA");
        var service = new VentaService(test.Db);
        var venta = await service.RegistrarDesdePedidoAsync(
            new VentaInput(
                pedido.Id,
                "VEN-ENTREGA",
                new DateOnly(2026, 2, 2),
                null,
                [new DetalleVentaInput(unidad.Id, null, null, 100m, null)]
            )
        );
        unidad.Estado = EstadoUnidadInventario.Entregada;
        await test.Db.SaveChangesAsync();

        var cancelacion = await service.CancelarAsync(venta.Value!.Id);

        Assert.False(cancelacion.IsSuccess);
        Assert.Equal(EstadoUnidadInventario.Entregada, unidad.Estado);
    }
}

public sealed class PagoTests
{
    [Fact]
    public async Task PagoMayorADeuda_SeRechaza()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearVentaCatalogoAsync("PED-PAGO-1", "VEN-PAGO-1");

        var result = await new PagoService(test.Db).RegistrarAsync(
            new PagoInput(
                test.Cliente.Id,
                new DateOnly(2026, 2, 3),
                101m,
                MetodoPago.Efectivo,
                null,
                null
            )
        );

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task PagoIgualADeuda_SeAcepta()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearVentaCatalogoAsync("PED-PAGO-2", "VEN-PAGO-2");

        var result = await new PagoService(test.Db).RegistrarAsync(
            new PagoInput(
                test.Cliente.Id,
                new DateOnly(2026, 2, 3),
                100m,
                MetodoPago.Efectivo,
                null,
                null
            )
        );

        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    [Fact]
    public async Task PagoSinDeuda_SeRechaza()
    {
        await using var test = await TestDatabase.CreateAsync();

        var result = await new PagoService(test.Db).RegistrarAsync(
            new PagoInput(
                test.Cliente.Id,
                new DateOnly(2026, 2, 3),
                10m,
                MetodoPago.Efectivo,
                null,
                null
            )
        );

        Assert.False(result.IsSuccess);
    }
}

public sealed class ReporteTests
{
    [Fact]
    public async Task UtilidadPorPeriodo_IgnoraVentasCanceladasYUsaCostoUnitario()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearVentaCatalogoAsync("PED-UTI-1", "VEN-UTI-1", 100m, 40m);
        var cancelada = await test.CrearVentaCatalogoAsync("PED-UTI-2", "VEN-UTI-2", 200m, 10m);
        cancelada.Estado = EstadoVenta.Cancelada;
        await test.Db.SaveChangesAsync();

        var result = await new DashboardService(test.Db).ObtenerUtilidadAsync(
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 28)
        );

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(60m, result.Value);
    }
}
