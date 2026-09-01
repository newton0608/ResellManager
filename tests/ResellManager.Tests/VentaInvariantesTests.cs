using Microsoft.EntityFrameworkCore;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

public sealed class CantidadesPedidoVentaTests
{
    [Fact]
    public async Task VentaConMasCantidadQuePedido_SeRechaza()
    {
        await using var test = await TestDatabase.CreateAsync();
        var pedido = await test.CrearPedidoAsync(TipoPedido.Catalogo, "PED-MAS", 2);

        var result = await RegistrarCatalogoAsync(test, pedido, "VEN-MAS", test.Producto.Id, 3);

        Assert.False(result.IsSuccess);
        Assert.Contains("coincidir exactamente", result.ErrorMessage!);
    }

    [Fact]
    public async Task VentaConMenosCantidadQuePedido_SeRechaza()
    {
        await using var test = await TestDatabase.CreateAsync();
        var pedido = await test.CrearPedidoAsync(TipoPedido.Catalogo, "PED-MENOS", 2);

        var result = await RegistrarCatalogoAsync(test, pedido, "VEN-MENOS", test.Producto.Id, 1);

        Assert.False(result.IsSuccess);
        Assert.Contains("coincidir exactamente", result.ErrorMessage!);
    }

    [Fact]
    public async Task VentaConCantidadExacta_SeAcepta()
    {
        await using var test = await TestDatabase.CreateAsync();
        var pedido = await test.CrearPedidoAsync(TipoPedido.Catalogo, "PED-EXACTA", 2);

        var result = await RegistrarCatalogoAsync(test, pedido, "VEN-EXACTA", test.Producto.Id, 2);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        await test.Db.Entry(pedido).ReloadAsync();
        Assert.Equal(EstadoPedido.Completado, pedido.Estado);
        Assert.Equal(2, result.Value!.Detalles.Count);
    }

    [Fact]
    public async Task VentaMultiplesProductos_DebeCoincidirPorProducto()
    {
        await using var test = await TestDatabase.CreateAsync();
        var segundoProducto = await test.CrearProductoAsync("PROD-2");
        var pedido = new Pedido
        {
            CodigoInterno = "PED-MULTI",
            Fecha = new DateOnly(2026, 2, 1),
            TipoPedido = TipoPedido.Catalogo,
            CanalVenta = CanalVenta.Facebook,
            Estado = EstadoPedido.Pendiente,
            ClienteId = test.Cliente.Id,
            Detalles =
            [
                new DetallePedido
                {
                    ProductoId = test.Producto.Id,
                    Cantidad = 2,
                    PrecioUnitario = 100m,
                },
                new DetallePedido
                {
                    ProductoId = segundoProducto.Id,
                    Cantidad = 1,
                    PrecioUnitario = 90m,
                },
            ],
        };
        test.Db.Pedidos.Add(pedido);
        await test.Db.SaveChangesAsync();
        var detalles = new[]
        {
            Catalogo(test.Producto.Id),
            Catalogo(segundoProducto.Id),
            Catalogo(segundoProducto.Id),
        };

        var result = await new VentaService(test.Db).RegistrarDesdePedidoAsync(
            new VentaInput(
                pedido.Id,
                "VEN-MULTI",
                new DateOnly(2026, 2, 2),
                null,
                detalles
            )
        );

        Assert.False(result.IsSuccess);
        Assert.Contains("por producto", result.ErrorMessage!);
    }

    private static Task<ResellManager.Application.Common.ServiceResult<VentaDto>> RegistrarCatalogoAsync(
        TestDatabase test,
        Pedido pedido,
        string codigo,
        int productoId,
        int cantidad
    ) =>
        new VentaService(test.Db).RegistrarDesdePedidoAsync(
            new VentaInput(
                pedido.Id,
                codigo,
                new DateOnly(2026, 2, 2),
                null,
                Enumerable.Range(0, cantidad).Select(_ => Catalogo(productoId)).ToArray()
            )
        );

    private static DetalleVentaInput Catalogo(int productoId) =>
        new(null, productoId, 40m, 100m, null);
}

public sealed class CancelacionVentaPagoTests
{
    [Fact]
    public async Task CancelarVentaQueDejariaSaldoNegativo_SeRechaza()
    {
        await using var test = await TestDatabase.CreateAsync();
        var venta = await test.CrearVentaCatalogoAsync("PED-CAN-PAGO", "VEN-CAN-PAGO");
        var pago = await new PagoService(test.Db).RegistrarAsync(
            new PagoInput(
                test.Cliente.Id,
                new DateOnly(2026, 2, 3),
                100m,
                MetodoPago.Efectivo,
                null,
                null
            )
        );

        var result = await new VentaService(test.Db).CancelarAsync(venta.Id);

        Assert.True(pago.IsSuccess, pago.ErrorMessage);
        Assert.False(result.IsSuccess);
        Assert.Contains("pagos", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        await test.Db.Entry(venta).ReloadAsync();
        Assert.Equal(EstadoVenta.Registrada, venta.Estado);
    }

    [Fact]
    public async Task CancelarVentaConOtrasDeudasSuficientes_SeAcepta()
    {
        await using var test = await TestDatabase.CreateAsync();
        var ventaA = await test.CrearVentaCatalogoAsync("PED-CAN-A", "VEN-CAN-A");
        await test.CrearVentaCatalogoAsync("PED-CAN-B", "VEN-CAN-B");
        var pago = await new PagoService(test.Db).RegistrarAsync(
            new PagoInput(
                test.Cliente.Id,
                new DateOnly(2026, 2, 3),
                50m,
                MetodoPago.Efectivo,
                null,
                null
            )
        );

        var result = await new VentaService(test.Db).CancelarAsync(ventaA.Id);
        var saldo = await new ClienteService(test.Db).ObtenerSaldoAsync(test.Cliente.Id);

        Assert.True(pago.IsSuccess, pago.ErrorMessage);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        await test.Db.Entry(ventaA).ReloadAsync();
        Assert.Equal(EstadoVenta.Cancelada, ventaA.Estado);
        Assert.Equal(50m, saldo.Value);
    }
}
