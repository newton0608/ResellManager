using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Pages;
using ResellManager.Web.Components.Pagos;
using ResellManager.Web.Components.Shared;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed class HistorialConsultaTests
{
    private static readonly DateOnly Agosto = new(2026, 8, 31);
    private static readonly DateOnly Inicio = new(2026, 9, 1);
    private static readonly DateOnly Fin = new(2026, 9, 10);

    // La misma matriz verifica los cuatro proveedores SQL, no una lista filtrada en memoria.
    [Theory]
    [InlineData(null, null, 4)]
    [InlineData("2026-09-01", null, 3)]
    [InlineData(null, "2026-09-01", 2)]
    [InlineData("2026-09-01", "2026-09-10", 3)]
    [InlineData("2026-09-10", "2026-09-10", 2)]
    [InlineData("2026-09-10", "2026-09-01", 0)]
    [InlineData("2027-01-01", null, 0)]
    public async Task Historiales_FechaInclusivaOpcionalOrdenDesempateYMeses(string? desde, string? hasta, int cantidad)
    {
        await using var test = await TestDatabase.CreateAsync();
        var fechas = new[] { Fin, Agosto, Inicio, Fin };
        for (var i = 0; i < fechas.Length; i++)
        {
            var venta = await test.CrearVentaCatalogoAsync($"PED-{i}", $"VEN-{i}");
            venta.Fecha = fechas[i];
            var pedido = await test.Db.Pedidos.FindAsync(venta.PedidoId);
            pedido!.Fecha = fechas[i];
            var compra = await new CompraService(test.Db).RegistrarAsync(test.Compra(OrigenCompra.Importacion, $"COM-{i}"));
            Assert.True(compra.IsSuccess, compra.ErrorMessage);
            (await test.Db.Compras.FindAsync(compra.Value!.Id))!.FechaCompra = fechas[i];
            var pago = await new PagoService(test.Db).RegistrarAsync(new PagoInput(test.Cliente.Id, fechas[i], 10m, MetodoPago.Efectivo, null, null));
            Assert.True(pago.IsSuccess, pago.ErrorMessage);
            await test.Db.SaveChangesAsync();
        }
        var filtro = new FiltroHistorial(Desde: desde is null ? null : DateOnly.Parse(desde), Hasta: hasta is null ? null : DateOnly.Parse(hasta));
        var pedidos = await new PedidoService(test.Db).ListarAsync(filtro: filtro);
        var ventas = await new VentaService(test.Db).ListarAsync(filtro: filtro);
        var compras = await new CompraService(test.Db).ListarAsync(filtro: filtro);
        var pagos = await new PagoService(test.Db).ListarPorClienteAsync(test.Cliente.Id, filtro: filtro);
        Verificar(pedidos.Select(x => new Registro(x.Id, x.Fecha)), cantidad);
        Verificar(ventas.Select(x => new Registro(x.Id, x.Fecha)), cantidad);
        Verificar(compras.Select(x => new Registro(x.Id, x.FechaCompra)), cantidad);
        Verificar(pagos.Select(x => new Registro(x.Id, x.Fecha)), cantidad);
        Assert.Equal(360m, (await new ClienteService(test.Db).ObtenerSaldoAsync(test.Cliente.Id)).Value);
    }

    [Fact]
    public async Task Pedidos_CodigoActivosCombinacionYEntregaFisicaReal()
    {
        await using var test = await TestDatabase.CreateAsync();
        foreach (var estado in Enum.GetValues<EstadoPedido>())
        {
            var pedido = await test.CrearPedidoAsync(TipoPedido.Catalogo, "PED-" + estado);
            pedido.Estado = estado;
            pedido.Fecha = Inicio;
        }
        await test.Db.SaveChangesAsync();
        var servicio = new PedidoService(test.Db);
        Assert.Equal(4, (await servicio.ListarAsync()).Count);
        Assert.Equal(2, (await servicio.ListarAsync(soloActivos: true)).Count);
        Assert.Single(await servicio.ListarAsync(filtro: new("ped-confirmado", Inicio, Fin), soloActivos: true));
        Assert.Empty(await servicio.ListarAsync(filtro: new("ped-cancelado"), soloActivos: true));
        Assert.Empty(await servicio.ListarAsync(conEntregaPendiente: true));
        var unidad = await test.CrearUnidadDisponibleAsync("COM-FISICA");
        Assert.Empty(await servicio.ListarAsync(conEntregaPendiente: true));
        var fisico = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-FISICO");
        Assert.True((await new InventarioService(test.Db).ReservarAsync(unidad.Id, fisico.Detalles.Single().Id)).IsSuccess);
        var venta = await new VentaService(test.Db).RegistrarDesdePedidoAsync(new VentaInput(fisico.Id, "VEN-FISICA", Inicio, null,
            [new DetalleVentaInput(unidad.Id, test.Producto.Id, 40m, 100m, null)]));
        Assert.True(venta.IsSuccess, venta.ErrorMessage);
        Assert.Equal(fisico.Id, Assert.Single(await servicio.ListarAsync(conEntregaPendiente: true)).Id);
        Assert.Single(await servicio.ListarAsync(filtro: new("ped-fisico", new(2026, 2, 1), Inicio), conEntregaPendiente: true));
        Assert.True((await new InventarioService(test.Db).CambiarEstadoAsync(unidad.Id, EstadoUnidadInventario.Entregada)).IsSuccess);
        Assert.Empty(await servicio.ListarAsync(conEntregaPendiente: true));
        Assert.True((await servicio.ObtenerPorIdAsync(fisico.Id)).IsSuccess);
    }

    [Fact]
    public async Task Ventas_CodigoEstadoYFechasCombinados()
    {
        await using var test = await TestDatabase.CreateAsync();
        var venta = await test.CrearVentaCatalogoAsync("PED-A", "VEN-BUSCAR");
        venta.Fecha = Inicio;
        await test.Db.SaveChangesAsync();
        var servicio = new VentaService(test.Db);
        Assert.Single(await servicio.ListarAsync(filtro: new("ven-buscar", Inicio, Fin), estado: EstadoVenta.Registrada));
        Assert.Empty(await servicio.ListarAsync(filtro: new("inexistente")));
        Assert.Empty(await servicio.ListarAsync(estado: EstadoVenta.Cancelada));
        Assert.True((await servicio.CancelarAsync(venta.Id)).IsSuccess);
        Assert.Single(await servicio.ListarAsync(filtro: new("VEN-BUSCAR", Inicio, Fin), estado: EstadoVenta.Cancelada));
        Assert.Empty(await servicio.ListarAsync(estado: EstadoVenta.Registrada));
    }

    [Fact]
    public async Task Compras_TerminoUnicoCodigoOProveedorCombinadoConFecha()
    {
        await using var test = await TestDatabase.CreateAsync();
        test.Proveedor.Nombre = "Distribuidora Central";
        var servicio = new CompraService(test.Db);
        var compra = await servicio.RegistrarAsync(test.Compra(OrigenCompra.Importacion, "COM-BUSCAR"));
        (await test.Db.Compras.FindAsync(compra.Value!.Id))!.FechaCompra = Inicio;
        await test.Db.SaveChangesAsync();
        Assert.Single(await servicio.ListarAsync(filtro: new("com-buscar", Inicio, Fin)));
        Assert.Single(await servicio.ListarAsync(filtro: new("DISTRIBUIDORA", Inicio, Fin)));
        Assert.Empty(await servicio.ListarAsync(filtro: new("central", Fin, null)));
        Assert.Empty(await servicio.ListarAsync(filtro: new("inexistente")));
    }

    [Fact]
    public async Task Pago_FiltroDeHistorialNoAlteraSaldoNiBorradorNiIncluyeOtroCliente()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearVentaCatalogoAsync("PED-SALDO", "VEN-SALDO");
        var servicio = new PagoService(test.Db);
        Assert.True((await servicio.RegistrarAsync(new(test.Cliente.Id, Agosto, 20m, MetodoPago.Efectivo, null, null))).IsSuccess);
        Assert.True((await servicio.RegistrarAsync(new(test.Cliente.Id, Inicio, 30m, MetodoPago.Efectivo, null, null))).IsSuccess);
        var otro = new Cliente { Nombres = "Otro" };
        test.Db.Clientes.Add(otro);
        await test.Db.SaveChangesAsync();
        test.Db.Pagos.Add(new Pago { ClienteId = otro.Id, Fecha = Inicio, Monto = 8m, MetodoPago = MetodoPago.Efectivo });
        await test.Db.SaveChangesAsync();
        var pagina = new Pagos();
        Set(pagina, "PagoService", servicio);
        Set(pagina, "ClienteService", new ClienteService(test.Db));
        Set(pagina, "Logger", NullLogger<Pagos>.Instance);
        Set(pagina, "ClienteDesdeQuery", test.Cliente.Id.ToString());
        await CallAsync(pagina, "OnParametersSetAsync");
        var borrador = Get<PagoFormModel>(pagina, "Modelo");
        borrador.Monto = 15m;
        Set(pagina, "CargandoHistorial", true);
        Call(pagina, "RevisarPago");
        Assert.False(Get<bool>(pagina, "RevisandoPago"));
        Set(pagina, "CargandoHistorial", false);
        Set(pagina, "DesdeConsulta", "2026-09-01");
        await CallAsync(pagina, "OnParametersSetAsync");
        Assert.Equal(30m, Assert.Single(Get<IReadOnlyList<PagoDto>>(pagina, "Historial")).Monto);
        Assert.Equal(50m, Get<decimal?>(pagina, "SaldoActual"));
        Assert.Same(borrador, Get<PagoFormModel>(pagina, "Modelo"));
        Assert.Equal(15m, borrador.Monto);
        Set(pagina, "HastaConsulta", "2026-08-31");
        await CallAsync(pagina, "OnParametersSetAsync");
        Assert.NotNull(Get<string?>(pagina, "ErrorHistorial"));
        Assert.Empty(Get<IReadOnlyList<PagoDto>>(pagina, "Historial"));
        Assert.Equal(50m, Get<decimal?>(pagina, "SaldoActual"));
        Set(pagina, "DesdeConsulta", null);
        Set(pagina, "HastaConsulta", null);
        await CallAsync(pagina, "OnParametersSetAsync");
        Assert.Equal(2, Get<IReadOnlyList<PagoDto>>(pagina, "Historial").Count);
    }

    [Fact]
    public void Agrupacion_EspanolAnioMesOrdenYDesempateSinMesesVacios()
    {
        var grupos = HistorialMensual.Agrupar(new[] { new Registro(1, Inicio), new Registro(2, Agosto), new Registro(3, Fin) }, x => x.Fecha, x => x.Id);
        Assert.Equal(new[] { "Septiembre 2026", "Agosto 2026" }, grupos.Select(x => x.Titulo));
        Assert.Equal(new[] { 3, 1 }, grupos[0].Registros.Select(x => x.Id));
        Assert.Single(grupos[1].Registros);
        Assert.Empty(HistorialMensual.Agrupar(Array.Empty<Registro>(), x => x.Fecha, x => x.Id));
        var anios = HistorialMensual.Agrupar(new[] { new Registro(1, Inicio), new Registro(2, new(2025, 9, 1)) }, x => x.Fecha, x => x.Id);
        Assert.Equal(new[] { "Septiembre 2026", "Septiembre 2025" }, anios.Select(x => x.Titulo));
    }

    [Theory]
    [InlineData("2026-09-01", "2026-09-10", true)]
    [InlineData("2026-09-10", "2026-09-01", false)]
    [InlineData("invalida", null, false)]
    [InlineData(null, "2026-02-30", false)]
    [InlineData(null, null, true)]
    public void Filtros_QueryValidaFechasYLimpiarConservaCliente(string? desde, string? hasta, bool valido)
    {
        var modelo = new FiltroHistorialModelo();
        Assert.Equal(valido, modelo.Cargar("PED ABC", desde, hasta, "ACTIVOS") is null);
        Assert.Equal("activos", modelo.Estado);
        Assert.Contains("estado=activos", modelo.Url("/pedidos"));
        modelo.Limpiar();
        Assert.False(modelo.Activo);
        Assert.Equal("/pedidos", modelo.Url("/pedidos"));
        Assert.Equal("/pagos?cliente=3", modelo.Url("/pagos", 3));
    }

    [Theory]
    [InlineData("pedidos")]
    [InlineData("ventas")]
    [InlineData("compras")]
    public async Task Paginas_QueryFiltraValidaYLimpia_GruposEnAmbasPresentaciones(string modulo)
    {
        await using var test = await TestDatabase.CreateAsync();
        var venta = await test.CrearVentaCatalogoAsync("PED-QUERY", "VEN-QUERY");
        venta.Fecha = Inicio;
        (await test.Db.Pedidos.FindAsync(venta.PedidoId))!.Fecha = Inicio;
        var compra = await new CompraService(test.Db).RegistrarAsync(test.Compra(OrigenCompra.Importacion, "COM-QUERY"));
        (await test.Db.Compras.FindAsync(compra.Value!.Id))!.FechaCompra = Inicio;
        await test.Db.SaveChangesAsync();
        object pagina = modulo switch { "pedidos" => new Pedidos(), "ventas" => new Ventas(), _ => new Compras() };
        if (pagina is Pedidos) { Set(pagina, "PedidoService", new PedidoService(test.Db)); Set(pagina, "Logger", NullLogger<Pedidos>.Instance); }
        if (pagina is Ventas) { Set(pagina, "VentaService", new VentaService(test.Db)); Set(pagina, "Logger", NullLogger<Ventas>.Instance); }
        if (pagina is Compras) { Set(pagina, "CompraService", new CompraService(test.Db)); Set(pagina, "Logger", NullLogger<Compras>.Instance); }
        var navegacion = new Navegacion();
        Set(pagina, "Navigation", navegacion);
        Set(pagina, "BuscarConsulta", "query");
        Set(pagina, "DesdeConsulta", "2026-09-01");
        Set(pagina, "HastaConsulta", "2026-09-10");
        await CallAsync(pagina, "OnParametersSetAsync");
        Assert.Null(Get<string?>(pagina, "ErrorMessage"));
        var texto = BuscadoresContextualesTests.TextoRenderizado(pagina);
        Assert.Equal(2, texto.Split("Septiembre 2026").Length - 1); // Tabla y tarjetas, mismos grupos.
        Set(pagina, "HastaConsulta", "2026-08-31");
        await CallAsync(pagina, "OnParametersSetAsync");
        Assert.Contains("Desde", Get<string>(pagina, "ErrorMessage"));
        Assert.DoesNotContain("Septiembre 2026", BuscadoresContextualesTests.TextoRenderizado(pagina));
        await CallAsync(pagina, "LimpiarFiltrosAsync");
        Assert.EndsWith("/" + modulo, navegacion.Uri);
        Set(pagina, "BuscarConsulta", null);
        Set(pagina, "DesdeConsulta", null);
        Set(pagina, "HastaConsulta", null);
        await CallAsync(pagina, "OnParametersSetAsync");
        Assert.Null(Get<string?>(pagina, "ErrorMessage"));
        Assert.Contains("Septiembre 2026", BuscadoresContextualesTests.TextoRenderizado(pagina));
    }

    private static void Verificar(IEnumerable<Registro> datos, int cantidad)
    {
        var lista = datos.ToArray();
        Assert.Equal(cantidad, lista.Length);
        Assert.Equal(lista.OrderByDescending(x => x.Fecha).ThenByDescending(x => x.Id), lista);
        var grupos = HistorialMensual.Agrupar(lista, x => x.Fecha, x => x.Id);
        Assert.Equal(lista, grupos.SelectMany(x => x.Registros));
        Assert.All(grupos, x => Assert.NotEmpty(x.Registros));
    }

    private sealed record Registro(int Id, DateOnly Fecha);
}
