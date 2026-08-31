using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Pagos;
using ResellManager.Web.Components.Ventas;
using PagosPage = ResellManager.Web.Components.Pages.Pagos;

namespace ResellManager.Tests;

public sealed class VentaDirectaPedidoAutomaticoTests
{
    [Fact]
    public async Task VentaDirecta_CreaPedidoAutomaticoYVentaConMismoClienteProductosYPrecios()
    {
        await using var test = await TestDatabase.CreateAsync();
        var productoB = await test.CrearProductoAsync("PROD-VD-B", 175m);
        var unidadA = await test.CrearUnidadDisponibleAsync("COMPRA-VD-A");
        var unidadB = await test.CrearUnidadDisponibleAsync("COMPRA-VD-B", productoB);
        var inventario = new InventarioService(test.Db);
        var unidades = await inventario.ListarDisponiblesAsync();
        var seleccionadas = unidades
            .Where(x => x.Id == unidadA.Id || x.Id == unidadB.Id)
            .Select(x => new UnidadVentaDirectaFormModel
            {
                Unidad = x,
                Seleccionada = true,
                PrecioFinal = x.Id == unidadA.Id ? 130m : 180m,
            })
            .ToArray();
        var pedidoService = new PedidoService(test.Db);
        var codigoPedido = VentaPresentacion.CrearCodigoPedidoVentaDirecta();

        var pedido = await pedidoService.CrearAsync(
            new PedidoInput(
                codigoPedido,
                new DateOnly(2026, 8, 30),
                TipoPedido.VentaDirecta,
                test.Cliente.Id,
                "Pedido automático de prueba",
                seleccionadas.Select(x => x.ToPedidoInput()).ToArray()
            )
        );
        var venta = await new VentaService(test.Db).RegistrarDesdePedidoAsync(
            new VentaInput(
                pedido.Value!.Id,
                "VEN-DIRECTA-ORQUESTADA",
                new DateOnly(2026, 8, 30),
                "Venta presencial",
                seleccionadas.Select(x => x.ToVentaInput()).ToArray()
            )
        );

        Assert.True(pedido.IsSuccess, pedido.ErrorMessage);
        Assert.True(venta.IsSuccess, venta.ErrorMessage);
        Assert.Equal(TipoPedido.VentaDirecta, pedido.Value.TipoPedido);
        Assert.Equal(pedido.Value.Id, venta.Value!.PedidoId);
        Assert.Equal(test.Cliente.Id, pedido.Value.ClienteId);
        Assert.Equal(test.Cliente.Id, venta.Value.ClienteId);
        Assert.Equal(2, pedido.Value.Detalles.Count);
        Assert.All(pedido.Value.Detalles, x => Assert.Equal(1, x.Cantidad));
        Assert.Equal(
            seleccionadas.Select(x => x.Unidad.ProductoId).OrderBy(x => x),
            pedido.Value.Detalles.Select(x => x.ProductoId).OrderBy(x => x)
        );
        Assert.Equal(
            seleccionadas.Select(x => x.PrecioFinal).OrderBy(x => x),
            pedido.Value.Detalles.Select(x => x.PrecioUnitario).OrderBy(x => x)
        );

        var pedidoFinal = await pedidoService.ObtenerPorIdAsync(pedido.Value.Id);
        Assert.Equal(EstadoPedido.Completado, pedidoFinal.Value!.Estado);
        await test.Db.Entry(unidadA).ReloadAsync();
        await test.Db.Entry(unidadB).ReloadAsync();
        Assert.Equal(EstadoUnidadInventario.Vendida, unidadA.Estado);
        Assert.Equal(EstadoUnidadInventario.Vendida, unidadB.Estado);
    }

    [Theory]
    [InlineData(EstadoUnidadInventario.Disponible, true)]
    [InlineData(EstadoUnidadInventario.Comprada, false)]
    [InlineData(EstadoUnidadInventario.EnTransito, false)]
    [InlineData(EstadoUnidadInventario.Vendida, false)]
    [InlineData(EstadoUnidadInventario.Entregada, false)]
    public void ElegibilidadVentaDirecta_ExigeEstadoDisponible(
        EstadoUnidadInventario estado,
        bool esperado)
    {
        var unidad = UnidadDto(1, estado);

        Assert.Equal(esperado, VentaPresentacion.EsUnidadElegibleVentaDirecta(unidad));
    }

    [Fact]
    public void ElegibilidadVentaDirecta_RechazaReservaActiva()
    {
        var unidad = UnidadDto(
            1,
            EstadoUnidadInventario.Disponible,
            detallePedidoReservaId: 7,
            pedidoReservaId: 3
        );

        Assert.False(VentaPresentacion.EsUnidadElegibleVentaDirecta(unidad));
    }

    [Fact]
    public void VentaDirecta_NoPermiteSeleccionarUnidadDosVeces()
    {
        Assert.True(VentaPresentacion.TieneUnidadesDuplicadas([15, 27, 15]));
        Assert.False(VentaPresentacion.TieneUnidadesDuplicadas([15, 27]));
    }

    [Fact]
    public void CodigoPedidoAutomatico_UsaPrefijoDistinguibleYGuidCompleto()
    {
        var primero = VentaPresentacion.CrearCodigoPedidoVentaDirecta();
        var segundo = VentaPresentacion.CrearCodigoPedidoVentaDirecta();

        Assert.StartsWith(VentaPresentacion.PrefijoPedidoVentaDirecta, primero);
        Assert.Equal(39, primero.Length);
        Assert.NotEqual(primero, segundo);
        Assert.True(Guid.TryParseExact(primero[7..], "N", out _));
    }

    [Fact]
    public async Task SiVentaFalla_ElPedidoAutomaticoPermanecePendienteYRecuperable()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearVentaCatalogoAsync("PED-CODIGO-OCUPADO", "VEN-CODIGO-OCUPADO");
        var unidad = await test.CrearUnidadDisponibleAsync("COMPRA-VD-FALLA");
        var unidadDto = (await new InventarioService(test.Db).ListarDisponiblesAsync())
            .Single(x => x.Id == unidad.Id);
        var renglon = new UnidadVentaDirectaFormModel
        {
            Unidad = unidadDto,
            Seleccionada = true,
            PrecioFinal = 125m,
        };
        var pedidoService = new PedidoService(test.Db);
        var pedido = await pedidoService.CrearAsync(
            new PedidoInput(
                VentaPresentacion.CrearCodigoPedidoVentaDirecta(),
                new DateOnly(2026, 8, 30),
                TipoPedido.VentaDirecta,
                test.Cliente.Id,
                null,
                [renglon.ToPedidoInput()]
            )
        );

        var venta = await new VentaService(test.Db).RegistrarDesdePedidoAsync(
            new VentaInput(
                pedido.Value!.Id,
                "VEN-CODIGO-OCUPADO",
                new DateOnly(2026, 8, 30),
                null,
                [renglon.ToVentaInput()]
            )
        );

        Assert.True(pedido.IsSuccess, pedido.ErrorMessage);
        Assert.False(venta.IsSuccess);
        var recuperado = await pedidoService.ObtenerPorIdAsync(pedido.Value.Id);
        Assert.True(recuperado.IsSuccess, recuperado.ErrorMessage);
        Assert.Equal(EstadoPedido.Pendiente, recuperado.Value!.Estado);
        Assert.Null(recuperado.Value.VentaId);
        await test.Db.Entry(unidad).ReloadAsync();
        Assert.Equal(EstadoUnidadInventario.Disponible, unidad.Estado);
    }

    [Fact]
    public void TodaVentaMantienePedidoObligatorioYBlazorNoUsaDbContext()
    {
        Assert.Equal(typeof(int), typeof(Venta).GetProperty(nameof(Venta.PedidoId))!.PropertyType);

        var raiz = BuscarRaizRepositorio();
        var componentes = Directory.GetFiles(
            Path.Combine(raiz, "src", "ResellManager.Web", "Components"),
            "*.razor",
            SearchOption.AllDirectories
        );
        Assert.All(componentes, archivo =>
            Assert.DoesNotContain(
                "DbContext",
                File.ReadAllText(archivo),
                StringComparison.Ordinal
            ));
    }

    private static UnidadInventarioDto UnidadDto(
        int id,
        EstadoUnidadInventario estado,
        int? detallePedidoReservaId = null,
        int? pedidoReservaId = null) =>
        new(
            id,
            $"UNI-{id}",
            estado,
            new DateOnly(2026, 8, 30),
            50m,
            1,
            "Producto",
            "PROD-1",
            1,
            OrigenCompra.CompraLocal,
            detallePedidoReservaId,
            pedidoReservaId,
            pedidoReservaId.HasValue ? 1 : null
        );

    private static string BuscarRaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "ResellManager.sln")))
            directorio = directorio.Parent;
        return directorio?.FullName
            ?? throw new InvalidOperationException("No fue posible localizar ResellManager.sln.");
    }
}

public sealed class PagoRecargaClienteTests
{
    [Fact]
    public async Task RegistrarPago_ConservaClienteYRecargaSaldoEHistorialDesdeBackend()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearVentaCatalogoAsync("PED-PAGO-RECARGA", "VEN-PAGO-RECARGA", 100m);
        var componente = new PagosPage();
        Establecer(componente, "PagoService", (IPagoService)new PagoService(test.Db));
        Establecer(componente, "ClienteService", (IClienteService)new ClienteService(test.Db));
        Establecer(componente, "Logger", NullLogger<PagosPage>.Instance);
        componente.ClienteDesdeQuery = test.Cliente.Id;
        await InvocarAsync(componente, "CargarClientesAsync");

        var modelo = Obtener<PagoFormModel>(componente, "Modelo");
        modelo.Monto = 25m;
        modelo.MetodoPago = MetodoPago.Transferencia;
        modelo.Referencia = "RECARGA-BACKEND";
        Establecer(componente, "SaldoActual", (decimal?)999m);

        await InvocarAsync(componente, "RegistrarPagoAsync");

        var clienteSeleccionado = Obtener<ClienteDto>(componente, "ClienteSeleccionado");
        var modeloReiniciado = Obtener<PagoFormModel>(componente, "Modelo");
        var saldo = Obtener<decimal?>(componente, "SaldoActual");
        var historial = Obtener<IReadOnlyList<PagoDto>>(componente, "Historial");
        Assert.Equal(test.Cliente.Id, clienteSeleccionado.Id);
        Assert.Equal(test.Cliente.Id, modeloReiniciado.ClienteId);
        Assert.Equal(0m, modeloReiniciado.Monto);
        Assert.Null(modeloReiniciado.Referencia);
        Assert.Equal(75m, saldo);
        Assert.Single(historial);
        Assert.Equal("RECARGA-BACKEND", historial.Single().Referencia);
        Assert.False(Obtener<bool>(componente, "Registrando"));
    }

    private static void Establecer<T>(object instancia, string propiedad, T valor)
    {
        var info = instancia.GetType().GetProperty(
            propiedad,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        ) ?? throw new InvalidOperationException($"No se encontró {propiedad}.");
        info.SetValue(instancia, valor);
    }

    private static T Obtener<T>(object instancia, string propiedad)
    {
        var info = instancia.GetType().GetProperty(
            propiedad,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        ) ?? throw new InvalidOperationException($"No se encontró {propiedad}.");
        return (T)info.GetValue(instancia)!;
    }

    private static async Task InvocarAsync(object instancia, string metodo)
    {
        var info = instancia.GetType().GetMethod(
            metodo,
            BindingFlags.Instance | BindingFlags.NonPublic
        ) ?? throw new InvalidOperationException($"No se encontró {metodo}.");
        await (Task)info.Invoke(instancia, null)!;
    }
}
