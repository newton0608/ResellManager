using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Clientes;
using ResellManager.Web.Components.Compras;
using ResellManager.Web.Components.Inventario;
using ResellManager.Web.Components.Pages;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed class UnidadesPerdidasTests
{
    [Fact]
    public async Task ActividadMensual_CerradaInicialmente_ConservaContenidoAlAbrir()
    {
        var componente = new ActividadMensual<string>();
        Set(componente, "ClienteId", 1);
        Set(componente, "Consultar", (Func<int, DateOnly?, Task<PaginaMes<string>>>)((_, _) =>
            Task.FromResult(new PaginaMes<string>(new(2026, 9, 1), ["operación conservada"], new(2026, 8, 1)))));
        Set(componente, "Registro", (Microsoft.AspNetCore.Components.RenderFragment<string>)(valor => builder => builder.AddContent(0, valor)));
        await CallAsync(componente, "OnParametersSetAsync");
        Assert.DoesNotContain("operación conservada", Texto(componente));
        var abierta = componente.GetType().GetField("Abierta", Flags)!;
        Assert.False((bool)abierta.GetValue(componente)!);
        abierta.SetValue(componente, true);
        await CallAsync(componente, "OnParametersSetAsync");
        Assert.True((bool)abierta.GetValue(componente)!);
        Assert.Contains("operación conservada", Texto(componente));
        Assert.Contains("Septiembre 2026", Texto(componente));
        Assert.Contains("agosto 2026", Texto(componente));
    }

#pragma warning disable BL0006
    private static string Texto(object componente)
    {
        using var builder = new Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder();
        Call(componente, "BuildRenderTree", builder);
        var frames = builder.GetFrames();
        return string.Concat(frames.Array.Take(frames.Count)
            .Where(x => x.FrameType == Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrameType.Text)
            .Select(x => x.TextContent));
    }
#pragma warning restore BL0006

    [Theory]
    [InlineData(EstadoUnidadInventario.Comprada)]
    [InlineData(EstadoUnidadInventario.EnTransito)]
    public async Task PerderUnidadReservada_LiberaSinCambiarPedidoCompraOCosto(EstadoUnidadInventario inicial)
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadImportadaAsync("COM-PERDIDA");
        var inventario = new InventarioService(test.Db);
        if (inicial == EstadoUnidadInventario.EnTransito)
            Assert.True((await inventario.CambiarEstadoAsync(unidad.Id, inicial)).IsSuccess);
        var pedido = await test.CrearPedidoAsync(TipoPedido.Importacion, "PED-RESERVA");
        var detalle = pedido.Detalles.Single();
        Assert.True((await inventario.ReservarAsync(unidad.Id, detalle.Id)).IsSuccess);
        var costo = unidad.Costo;
        var compra = (await new CompraService(test.Db).ListarAsync()).Single();
        var result = await inventario.CambiarEstadoAsync(unidad.Id, EstadoUnidadInventario.Perdida);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(EstadoUnidadInventario.Perdida, result.Value!.Estado);
        Assert.Null(result.Value.DetallePedidoReservaId);
        Assert.Null(result.Value.FechaIngreso);
        Assert.Equal(costo, result.Value.Costo);
        Assert.Equal(unidad.Id, (await test.Db.UnidadesInventario.AsNoTracking().SingleAsync()).Id);
        var despues = (await new CompraService(test.Db).ObtenerPorIdAsync(compra.Id)).Value!;
        Assert.Equal(compra.Total, despues.Total);
        Assert.Equal(compra.Detalles.ToArray(), despues.Detalles.ToArray());
        Assert.Equal(EstadoPedido.Pendiente, (await test.Db.Pedidos.AsNoTracking().SingleAsync()).Estado);
        Assert.Equal(1, (await test.Db.DetallesPedido.AsNoTracking().SingleAsync()).Cantidad);
        Assert.Empty(await test.Db.UnidadesInventario.Where(x => x.DetallePedidoReservaId == detalle.Id).ToListAsync());
        Assert.Empty(await new SeleccionOperativaService(test.Db).PendientesClienteAsync(test.Cliente.Id));
        // No requiere columnas ni migración: se persiste el nombre en el mapeo string existente.
        var propiedad = test.Db.Model.FindEntityType(typeof(UnidadInventario))!.FindProperty(nameof(UnidadInventario.Estado))!;
        Assert.Equal(typeof(string), propiedad.GetTypeMapping().Converter!.ProviderClrType);
        Assert.Equal(30, propiedad.GetMaxLength());
        Assert.Equal("Perdida", await test.Db.Database.SqlQueryRaw<string>("SELECT Estado AS Value FROM UnidadesInventario").SingleAsync());
        Assert.False(test.Db.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData(EstadoUnidadInventario.Comprada)]
    [InlineData(EstadoUnidadInventario.EnTransito)]
    [InlineData(EstadoUnidadInventario.Disponible)]
    [InlineData(EstadoUnidadInventario.Vendida)]
    [InlineData(EstadoUnidadInventario.Entregada)]
    public async Task Perdida_EsTerminal(EstadoUnidadInventario destino)
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadImportadaAsync("COM");
        var servicio = new InventarioService(test.Db);
        Assert.True((await servicio.CambiarEstadoAsync(unidad.Id, EstadoUnidadInventario.Perdida)).IsSuccess);
        Assert.False((await servicio.CambiarEstadoAsync(unidad.Id, destino)).IsSuccess);
        Assert.Equal(EstadoUnidadInventario.Perdida, unidad.Estado);
    }

    [Theory]
    [InlineData(EstadoUnidadInventario.Disponible)]
    [InlineData(EstadoUnidadInventario.Vendida)]
    [InlineData(EstadoUnidadInventario.Entregada)]
    public async Task UnidadIngresada_NoPuedeMarcarsePerdida(EstadoUnidadInventario inicial)
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("LOCAL");
        unidad.Estado = inicial;
        await test.Db.SaveChangesAsync();
        Assert.False((await new InventarioService(test.Db).CambiarEstadoAsync(unidad.Id, EstadoUnidadInventario.Perdida)).IsSuccess);
        Assert.Equal(inicial, unidad.Estado);
        Assert.NotNull(unidad.FechaIngreso);
    }

    [Fact]
    public async Task Perdida_NoAdmiteRecepcionReservaVentaNiSeleccionOperativa()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadImportadaAsync("COM");
        var servicio = new InventarioService(test.Db);
        Assert.True((await servicio.CambiarEstadoAsync(unidad.Id, EstadoUnidadInventario.Perdida)).IsSuccess);
        var pedido = await test.CrearPedidoAsync(TipoPedido.Importacion, "PED");
        Assert.False((await servicio.RegistrarRecepcionAsync(new(new(2026, 9, 15), [unidad.Id]))).IsSuccess);
        Assert.False((await servicio.ReservarAsync(unidad.Id, pedido.Detalles.Single().Id)).IsSuccess);
        Assert.False((await new VentaService(test.Db).RegistrarDesdePedidoAsync(new(pedido.Id, "VEN", new(2026, 9, 15), null,
            [new(unidad.Id, null, null, 100, null)]))).IsSuccess);
        var seleccion = new SeleccionOperativaService(test.Db);
        Assert.Empty(await seleccion.ListarReservablesAsync(test.Producto.Id));
        Assert.Empty(await seleccion.BuscarUnidadesAsync("COM"));
        Assert.Empty(await servicio.ListarDisponiblesAsync());
        Assert.Empty(await new RecepcionCompraService(test.Db).PendientesAsync());
        Assert.Equal(unidad.Id, Assert.Single(await servicio.BuscarAsync(null, EstadoUnidadInventario.Perdida)).Id);
        Assert.Null(unidad.FechaIngreso);
        Assert.Empty(await test.Db.Ventas.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResumenImportacion_RecibidasPorIngreso_PerdidasResueltasYTotalIntacto(bool conPerdida)
    {
        await using var test = await TestDatabase.CreateAsync();
        var compras = new CompraService(test.Db);
        var entrada = test.Compra(OrigenCompra.Importacion, "COM-DIEZ", cantidad: 10) with
        { Detalles = [new(test.Producto.Id, 10, 100)] };
        var compra = (await compras.RegistrarAsync(entrada)).Value!;
        var unidades = await test.Db.UnidadesInventario.OrderBy(x => x.Id).ToListAsync();
        var inventario = new InventarioService(test.Db);
        Assert.True((await inventario.CambiarEstadoAsync(unidades[0].Id, EstadoUnidadInventario.EnTransito)).IsSuccess);
        if (conPerdida) Assert.True((await inventario.CambiarEstadoAsync(unidades[0].Id, EstadoUnidadInventario.Perdida)).IsSuccess);
        Assert.True((await inventario.RegistrarRecepcionAsync(new(new(2026, 9, 15), unidades.Skip(3).Select(x => x.Id).ToArray()))).IsSuccess);
        var parcial = (await compras.ObtenerPorIdAsync(compra.Id)).Value!.Recepcion!;
        Assert.Equal(7, parcial.Recibidas);
        Assert.Equal(conPerdida ? 2 : 3, parcial.Pendientes);
        Assert.Equal(conPerdida ? 1 : 0, parcial.Perdidas);
        Assert.Equal(conPerdida ? 0 : 1, parcial.EnTransito);
        Assert.Equal(2, parcial.Compradas);
        Assert.Equal("Recepción pendiente", CompraPresentacion.EstadoRecepcion(parcial));
        if (conPerdida)
        {
            Assert.False((await inventario.RegistrarRecepcionAsync(new(new(2026, 9, 15), [unidades[0].Id, unidades[1].Id]))).IsSuccess);
            Assert.Equal(EstadoUnidadInventario.Comprada, unidades[1].Estado);
        }
        Assert.True((await inventario.RegistrarRecepcionAsync(new(new(2026, 9, 15),
            unidades.Where(x => x.Estado is EstadoUnidadInventario.Comprada or EstadoUnidadInventario.EnTransito).Select(x => x.Id).ToArray()))).IsSuccess);
        var pedido = await test.CrearPedidoAsync(TipoPedido.Importacion, "PED", cantidad: 2);
        Assert.True((await new VentaService(test.Db).RegistrarDesdePedidoAsync(new(pedido.Id, "VEN", new(2026, 9, 15), null,
            [new(unidades[3].Id, null, null, 100, null), new(unidades[4].Id, null, null, 100, null)]))).IsSuccess);
        Assert.True((await inventario.CambiarEstadoAsync(unidades[3].Id, EstadoUnidadInventario.Entregada)).IsSuccess);
        var final = (await compras.ObtenerPorIdAsync(compra.Id)).Value!;
        Assert.Equal(1000, final.Total);
        Assert.Equal(10, await test.Db.UnidadesInventario.CountAsync());
        Assert.All(await test.Db.UnidadesInventario.ToListAsync(), x => Assert.Equal(100, x.Costo));
        Assert.Equal(10, final.Recepcion!.Total);
        Assert.Equal(conPerdida ? 9 : 10, final.Recepcion.Recibidas);
        Assert.Equal(0, final.Recepcion.Pendientes);
        Assert.Equal(conPerdida ? "Recepción finalizada con pérdidas" : "Recepción completada", CompraPresentacion.EstadoRecepcion(final.Recepcion));
        Assert.Empty(await new RecepcionCompraService(test.Db).PendientesAsync());
    }

    [Fact]
    public async Task MarcarPerdida_ExigeConfirmacion_YConservaAccionEnTransito()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadImportadaAsync("COM");
        var pagina = new Inventario();
        Set(pagina, "InventarioService", new InventarioService(test.Db));
        Set(pagina, "Logger", NullLogger<Inventario>.Instance);
        await CallAsync(pagina, "CargarInventarioAsync");
        var dto = Assert.Single(Get<IReadOnlyList<UnidadInventarioDto>>(pagina, "Unidades"));
        Assert.Equal(EstadoUnidadInventario.EnTransito, InventarioPresentacion.SiguienteEstadoManual(dto));
        Assert.True(InventarioPresentacion.PuedeMarcarPerdida(dto));
        Call(pagina, "RevisarPerdida", dto);
        Assert.Equal(EstadoUnidadInventario.Comprada, unidad.Estado);
        Call(pagina, "CerrarPerdida");
        await CallAsync(pagina, "ConfirmarPerdidaAsync");
        Assert.Equal(EstadoUnidadInventario.Comprada, unidad.Estado);
        Call(pagina, "RevisarPerdida", dto);
        await CallAsync(pagina, "ConfirmarPerdidaAsync");
        var perdida = Assert.Single(Get<IReadOnlyList<UnidadInventarioDto>>(pagina, "Unidades"));
        Assert.Equal(EstadoUnidadInventario.Perdida, perdida.Estado);
        Assert.False(InventarioPresentacion.PuedeMarcarPerdida(perdida));
        Assert.False(InventarioPresentacion.PuedeRecibirse(perdida));
        Assert.Null(InventarioPresentacion.SiguienteEstadoManual(perdida));
        Assert.Equal("Perdida", InventarioPresentacion.Estado(perdida.Estado));
    }

    [Fact]
    public async Task PendientesCliente_IniciaCerradoAunqueTengaReserva()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadImportadaAsync("COM");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Importacion, "PED");
        Assert.True((await new InventarioService(test.Db).ReservarAsync(unidad.Id, pedido.Detalles.Single().Id)).IsSuccess);
        await using var servicios = CierreV1OperativoTests.Servicios(test);
        var componente = new PendientesCliente();
        Set(componente, "Scopes", servicios.GetRequiredService<IServiceScopeFactory>());
        Set(componente, "Logger", NullLogger<PendientesCliente>.Instance);
        Set(componente, "ClienteId", test.Cliente.Id);
        await CallAsync(componente, "OnParametersSetAsync");
        var abierta = typeof(PendientesCliente).GetField("Abierta", Flags)!;
        Assert.False((bool)abierta.GetValue(componente)!);
        Assert.Single((IReadOnlyList<PendienteClienteDto>)typeof(PendientesCliente).GetField("Unidades", Flags)!.GetValue(componente)!);
        abierta.SetValue(componente, true);
        await CallAsync(componente, "OnParametersSetAsync");
        Assert.True((bool)abierta.GetValue(componente)!);
    }
}
