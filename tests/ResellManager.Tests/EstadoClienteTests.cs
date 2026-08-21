using Microsoft.EntityFrameworkCore;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

public sealed class TransicionesEstadoInventarioTests
{
    [Fact]
    public async Task Disponible_NoPuedeVolverAComprada()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("LOC-NO-COMPRADA");

        var result = await new InventarioService(test.Db).CambiarEstadoAsync(
            unidad.Id,
            EstadoUnidadInventario.Comprada
        );

        Assert.False(result.IsSuccess);
        await test.Db.Entry(unidad).ReloadAsync();
        Assert.Equal(EstadoUnidadInventario.Disponible, unidad.Estado);
    }

    [Fact]
    public async Task Disponible_NoPuedeVolverAEnTransito()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("LOC-NO-TRANSITO");

        var result = await new InventarioService(test.Db).CambiarEstadoAsync(
            unidad.Id,
            EstadoUnidadInventario.EnTransito
        );

        Assert.False(result.IsSuccess);
        await test.Db.Entry(unidad).ReloadAsync();
        Assert.Equal(EstadoUnidadInventario.Disponible, unidad.Estado);
    }

    [Fact]
    public async Task Vendida_SoloPuedePasarAEntregadaManualmente()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await CrearUnidadVendidaAsync(test, "LOC-VENDIDA");
        var service = new InventarioService(test.Db);

        var invalida = await service.CambiarEstadoAsync(
            unidad.Id,
            EstadoUnidadInventario.Comprada
        );
        var entrega = await service.CambiarEstadoAsync(
            unidad.Id,
            EstadoUnidadInventario.Entregada
        );

        Assert.False(invalida.IsSuccess);
        Assert.True(entrega.IsSuccess, entrega.ErrorMessage);
        Assert.Equal(EstadoUnidadInventario.Entregada, entrega.Value!.Estado);
    }

    [Fact]
    public async Task Entregada_NoPuedeCambiar()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await CrearUnidadVendidaAsync(test, "LOC-ENTREGADA");
        var service = new InventarioService(test.Db);
        var entrega = await service.CambiarEstadoAsync(
            unidad.Id,
            EstadoUnidadInventario.Entregada
        );

        var result = await service.CambiarEstadoAsync(
            unidad.Id,
            EstadoUnidadInventario.Disponible
        );

        Assert.True(entrega.IsSuccess, entrega.ErrorMessage);
        Assert.False(result.IsSuccess);
        await test.Db.Entry(unidad).ReloadAsync();
        Assert.Equal(EstadoUnidadInventario.Entregada, unidad.Estado);
    }

    private static async Task<ResellManager.Domain.Entities.UnidadInventario> CrearUnidadVendidaAsync(
        TestDatabase test,
        string codigo
    )
    {
        var unidad = await test.CrearUnidadDisponibleAsync(codigo);
        var pedido = await test.CrearPedidoAsync(TipoPedido.VentaDirecta, $"PED-{codigo}");
        var venta = await new VentaService(test.Db).RegistrarDesdePedidoAsync(
            new VentaInput(
                pedido.Id,
                $"VEN-{codigo}",
                new DateOnly(2026, 2, 2),
                null,
                [new DetalleVentaInput(unidad.Id, null, null, 100m, null)]
            )
        );
        Assert.True(venta.IsSuccess, venta.ErrorMessage);
        await test.Db.Entry(unidad).ReloadAsync();
        return unidad;
    }
}

public sealed class ClienteSqliteTests
{
    [Fact]
    public async Task ClienteService_ListarAsync_FuncionaConVentasYPagosEnSQLite()
    {
        await using var test = await TestDatabase.CreateAsync();
        await CrearVentaYPagoAsync(test);

        var clientes = await new ClienteService(test.Db).ListarAsync();

        var cliente = Assert.Single(clientes);
        Assert.Equal(test.Cliente.Id, cliente.Id);
        Assert.Equal(60m, cliente.Saldo);
    }

    [Fact]
    public async Task ClienteService_BuscarAsync_FuncionaConVentasYPagosEnSQLite()
    {
        await using var test = await TestDatabase.CreateAsync();
        await CrearVentaYPagoAsync(test);

        var clientes = await new ClienteService(test.Db).BuscarAsync("555-0100");

        var cliente = Assert.Single(clientes);
        Assert.Equal(test.Cliente.Id, cliente.Id);
        Assert.Equal(60m, cliente.Saldo);
    }

    private static async Task CrearVentaYPagoAsync(TestDatabase test)
    {
        await test.CrearVentaCatalogoAsync("PED-CLI-SQL", "VEN-CLI-SQL", 100m, 40m);
        var pago = await new PagoService(test.Db).RegistrarAsync(
            new PagoInput(
                test.Cliente.Id,
                new DateOnly(2026, 2, 3),
                40m,
                MetodoPago.Efectivo,
                null,
                null
            )
        );
        Assert.True(pago.IsSuccess, pago.ErrorMessage);
    }
}
