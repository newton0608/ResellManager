using Microsoft.EntityFrameworkCore;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

public sealed class CompraTests
{
    [Fact]
    public async Task CompraLocal_GeneraUnidadesDisponibles()
    {
        await using var test = await TestDatabase.CreateAsync();

        var result = await new CompraService(test.Db).RegistrarAsync(
            test.Compra(OrigenCompra.CompraLocal, "CL-1", new DateOnly(2026, 1, 12), 2)
        );

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var unidades = await test.Db.UnidadesInventario.ToListAsync();
        Assert.Equal(2, unidades.Count);
        Assert.All(unidades, x => Assert.Equal(EstadoUnidadInventario.Disponible, x.Estado));
        Assert.All(unidades, x => Assert.Equal(new DateOnly(2026, 1, 12), x.FechaIngreso));
    }

    [Fact]
    public async Task Importacion_GeneraUnidadesNoDisponiblesYSinFechaIngreso()
    {
        await using var test = await TestDatabase.CreateAsync();

        var result = await new CompraService(test.Db).RegistrarAsync(
            test.Compra(OrigenCompra.Importacion, "IMP-1")
        );

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var unidad = await test.Db.UnidadesInventario.SingleAsync();
        Assert.Equal(EstadoUnidadInventario.Comprada, unidad.Estado);
        Assert.Null(unidad.FechaIngreso);
    }

    [Fact]
    public async Task Catalogo_NoGeneraUnidades()
    {
        await using var test = await TestDatabase.CreateAsync();

        var result = await new CompraService(test.Db).RegistrarAsync(
            test.Compra(OrigenCompra.Catalogo, "CAT-1")
        );

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Empty(await test.Db.UnidadesInventario.ToListAsync());
        Assert.Single(await test.Db.DetallesCompra.ToListAsync());
    }

    [Fact]
    public async Task EnvioHermano_GeneraUnidadesDisponibles()
    {
        await using var test = await TestDatabase.CreateAsync();

        var result = await new CompraService(test.Db).RegistrarAsync(
            test.Compra(OrigenCompra.EnvioHermano, "ENV-1", new DateOnly(2026, 1, 14))
        );

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var unidad = await test.Db.UnidadesInventario.SingleAsync();
        Assert.Equal(EstadoUnidadInventario.Disponible, unidad.Estado);
        Assert.Equal(new DateOnly(2026, 1, 14), unidad.FechaIngreso);
    }
}

public sealed class RecepcionTests
{
    [Fact]
    public async Task UnidadImportada_PuedeRecibirse()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadImportadaAsync("IMP-REC");
        var fecha = new DateOnly(2026, 2, 15);

        var result = await new InventarioService(test.Db).RegistrarRecepcionAsync(
            new RecepcionMercanciaInput(fecha, [unidad.Id])
        );

        Assert.True(result.IsSuccess, result.ErrorMessage);
        await test.Db.Entry(unidad).ReloadAsync();
        Assert.Equal(EstadoUnidadInventario.Disponible, unidad.Estado);
        Assert.Equal(fecha, unidad.FechaIngreso);
    }

    [Theory]
    [InlineData(EstadoUnidadInventario.Vendida)]
    [InlineData(EstadoUnidadInventario.Entregada)]
    public async Task UnidadVendidaOEntregada_NoPuedeRecibirse(EstadoUnidadInventario estado)
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadImportadaAsync($"IMP-{estado}");
        unidad.Estado = estado;
        await test.Db.SaveChangesAsync();

        var result = await new InventarioService(test.Db).RegistrarRecepcionAsync(
            new RecepcionMercanciaInput(new DateOnly(2026, 2, 15), [unidad.Id])
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(estado, unidad.Estado);
        Assert.Null(unidad.FechaIngreso);
    }
}
