using System.Collections;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Inventario;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed class RecepcionPerdidasTests
{
    private static object? Campo(object x, string nombre) => x.GetType().GetField(nombre, Flags)!.GetValue(x);
    private static object Grupo(RecepcionCompras x) => ((IEnumerable)Campo(x, "Grupos")!).Cast<object>().Single();
    private static RecepcionCompras Crear(IServiceProvider servicios) {
        var x = new RecepcionCompras();
        Set(x, "Scopes", servicios.GetRequiredService<IServiceScopeFactory>());
        Set(x, "Logger", NullLogger<RecepcionCompras>.Instance);
        return x;
    }

    [Theory]
    [InlineData(EstadoUnidadInventario.Comprada)]
    [InlineData(EstadoUnidadInventario.EnTransito)]
    public async Task AccionPorUnidad_ConfirmaAntesDeCambiar_YCancelarConservaSeleccion(EstadoUnidadInventario estado)
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadImportadaAsync("COM");
        if (estado == EstadoUnidadInventario.EnTransito)
            Assert.True((await new InventarioService(test.Db).CambiarEstadoAsync(unidad.Id, estado)).IsSuccess);
        await using var servicios = CierreV1OperativoTests.Servicios(test);
        var componente = Crear(servicios);
        await CallAsync(componente, "CargarAsync");
        Assert.Contains("Marcar perdida", Texto(componente));
        var grupo = Grupo(componente);
        var dto = Get<RecepcionCompraDto>(grupo, "Compra").Unidades.Single();
        Call(componente, "Seleccionar", grupo, unidad.Id, true);
        Call(componente, "RevisarPerdida", dto);
        Assert.NotNull(Campo(componente, "UnidadPerdida"));
        await test.Db.Entry(unidad).ReloadAsync();
        Assert.Equal(estado, unidad.Estado);
        Call(componente, "Revisar", grupo);
        Assert.Null(Campo(componente, "Revision"));
        Call(componente, "CerrarPerdida");
        await CallAsync(componente, "ConfirmarPerdidaAsync");
        Assert.Contains(unidad.Id, Get<HashSet<int>>(grupo, "Seleccionadas"));
        await test.Db.Entry(unidad).ReloadAsync();
        Assert.Equal(estado, unidad.Estado);
        Call(componente, "RevisarPerdida", dto);
        await CallAsync(componente, "ConfirmarPerdidaAsync");
        await test.Db.Entry(unidad).ReloadAsync();
        Assert.Equal(EstadoUnidadInventario.Perdida, unidad.Estado);
        Assert.Empty(Get<HashSet<int>>(grupo, "Seleccionadas"));
        Assert.Empty((IEnumerable)Campo(componente, "Grupos")!);
        Assert.Null(Campo(componente, "UnidadPerdida"));
    }

    [Fact]
    public async Task UnidadRecibidaDuranteConfirmacion_BackendRechazaPerdidaSinAlterarSeleccion()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadImportadaAsync("COM");
        await using var servicios = CierreV1OperativoTests.Servicios(test);
        var componente = Crear(servicios);
        var actualizaciones = 0;
        Set(componente, "OnRecibido", EventCallback.Factory.Create(new object(), (Action)(() => actualizaciones++)));
        await CallAsync(componente, "CargarAsync");
        var grupo = Grupo(componente);
        Call(componente, "Seleccionar", grupo, unidad.Id, true);
        Call(componente, "RevisarPerdida", Get<RecepcionCompraDto>(grupo, "Compra").Unidades.Single());
        Assert.True((await new InventarioService(test.Db).RegistrarRecepcionAsync(new(new(2026, 9, 15), [unidad.Id]))).IsSuccess);
        await CallAsync(componente, "ConfirmarPerdidaAsync");
        Assert.NotNull(Campo(componente, "Error"));
        Assert.NotNull(Campo(componente, "UnidadPerdida"));
        Assert.Contains(unidad.Id, Get<HashSet<int>>(grupo, "Seleccionadas"));
        Assert.Equal(0, actualizaciones);
        await test.Db.Entry(unidad).ReloadAsync();
        Assert.Equal(EstadoUnidadInventario.Disponible, unidad.Estado);
    }

    [Fact]
    public async Task RecepcionParcial_CincoUnidades_TresRecibidasDosPerdidas_SinAlterarCompra()
    {
        await using var test = await TestDatabase.CreateAsync();
        var compra = (await new CompraService(test.Db).RegistrarAsync(test.Compra(OrigenCompra.Importacion, "COM-CINCO", cantidad: 5))).Value!;
        var unidades = await test.Db.UnidadesInventario.OrderBy(x => x.Id).ToListAsync();
        var inventario = new InventarioService(test.Db);
        Assert.True((await inventario.CambiarEstadoAsync(unidades[3].Id, EstadoUnidadInventario.EnTransito)).IsSuccess);
        var pedido = await test.CrearPedidoAsync(TipoPedido.Importacion, "PED");
        Assert.True((await inventario.ReservarAsync(unidades[2].Id, pedido.Detalles.Single().Id)).IsSuccess);
        await using var servicios = CierreV1OperativoTests.Servicios(test);
        var componente = Crear(servicios);
        var actualizaciones = 0;
        Set(componente, "OnRecibido", EventCallback.Factory.Create(new object(), (Action)(() => actualizaciones++)));
        await CallAsync(componente, "CargarAsync");
        var grupo = Grupo(componente);
        foreach (var unidad in unidades.Take(2)) Call(componente, "Seleccionar", grupo, unidad.Id, true);
        Call(componente, "Revisar", grupo);
        await CallAsync(componente, "ConfirmarAsync");
        Assert.Equal(3, Get<RecepcionCompraDto>(Grupo(componente), "Compra").Unidades.Count);
        grupo = Grupo(componente);
        Call(componente, "Seleccionar", grupo, unidades[2].Id, true);
        Call(componente, "Seleccionar", grupo, unidades[3].Id, true);
        Call(componente, "RevisarPerdida", Get<RecepcionCompraDto>(grupo, "Compra").Unidades.Single(x => x.Id == unidades[2].Id));
        await CallAsync(componente, "ConfirmarPerdidaAsync");
        Assert.Equal(new[] { unidades[3].Id }, Get<HashSet<int>>(grupo, "Seleccionadas"));
        Assert.Equal(2, Get<RecepcionCompraDto>(Grupo(componente), "Compra").Unidades.Count);
        foreach (var unidad in unidades) await test.Db.Entry(unidad).ReloadAsync();
        Assert.Equal(new[] { EstadoUnidadInventario.Disponible, EstadoUnidadInventario.Disponible, EstadoUnidadInventario.Perdida,
            EstadoUnidadInventario.EnTransito, EstadoUnidadInventario.Comprada }, unidades.Select(x => x.Estado));
        Assert.Null(unidades[2].DetallePedidoReservaId);
        Assert.Equal(EstadoPedido.Pendiente, pedido.Estado);
        Call(componente, "Revisar", Grupo(componente));
        await CallAsync(componente, "ConfirmarAsync");
        var ultima = Assert.Single(Get<RecepcionCompraDto>(Grupo(componente), "Compra").Unidades);
        Assert.Equal(unidades[4].Id, ultima.Id);
        Call(componente, "RevisarPerdida", ultima);
        await CallAsync(componente, "ConfirmarPerdidaAsync");
        Assert.Empty((IEnumerable)Campo(componente, "Grupos")!);
        Assert.Equal(4, actualizaciones);
        var final = (await new CompraService(test.Db).ObtenerPorIdAsync(compra.Id)).Value!;
        Assert.Equal(compra.Total, final.Total);
        Assert.Equal(compra.Detalles.ToArray(), final.Detalles.ToArray());
        Assert.Equal(5, final.Recepcion!.Total);
        Assert.Equal(3, final.Recepcion.Recibidas);
        Assert.Equal(2, final.Recepcion.Perdidas);
        Assert.Equal(0, final.Recepcion.Pendientes);
        Assert.Equal(5, await test.Db.UnidadesInventario.CountAsync());
        Assert.False(test.Db.Database.HasPendingModelChanges());
    }

#pragma warning disable BL0006
    private static string Texto(RecepcionCompras componente)
    {
        using var builder = new RenderTreeBuilder();
        Call(componente, "BuildRenderTree", builder);
        var frames = builder.GetFrames();
        return string.Concat(frames.Array.Take(frames.Count).Select(x => x.FrameType switch {
            RenderTreeFrameType.Text => x.TextContent,
            RenderTreeFrameType.Markup => x.MarkupContent,
            _ => ""
        }));
    }
#pragma warning restore BL0006
}
