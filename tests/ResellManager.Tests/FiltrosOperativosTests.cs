using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Pages;

namespace ResellManager.Tests;

public sealed class FiltrosOperativosTests
{
    [Fact]
    public async Task Clientes_DeudaUsaSaldoBackendYSePuedeQuitarSinRecrearPagina()
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new ClienteService(test.Db);
        await servicio.CrearAsync(new ClienteInput("Sin deuda", null, "555-1111", null, null));
        await test.CrearVentaCatalogoAsync("PED-DEUDA", "VEN-DEUDA", 100m);
        var pagina = new Clientes();
        Establecer(pagina, "ClienteService", servicio);
        Establecer(pagina, "Logger", NullLogger<Clientes>.Instance);
        Establecer(pagina, "SaldoFiltro", "PENDIENTE");
        await ParametrosAsync(pagina);
        var encontrado = Assert.Single(Obtener<IReadOnlyList<ClienteDto>>(pagina, "ClientesEncontrados"));
        Assert.Equal(test.Cliente.Id, encontrado.Id);
        Assert.Equal(100m, encontrado.Saldo);

        Establecer(pagina, "Termino", "Sin deuda");
        await InvocarAsync(pagina, "BuscarAsync");
        Assert.Empty(Obtener<IReadOnlyList<ClienteDto>>(pagina, "ClientesEncontrados"));
        Establecer(pagina, "Termino", "");
        Establecer(pagina, "SaldoFiltro", null);
        await ParametrosAsync(pagina);
        Assert.Equal(2, Obtener<IReadOnlyList<ClienteDto>>(pagina, "ClientesEncontrados").Count);

        await new PagoService(test.Db).RegistrarAsync(new PagoInput(test.Cliente.Id,
            new DateOnly(2026, 9, 8), 100m, MetodoPago.Efectivo, null, null));
        Establecer(pagina, "SaldoFiltro", "pendiente");
        await ParametrosAsync(pagina);
        Assert.Empty(Obtener<IReadOnlyList<ClienteDto>>(pagina, "ClientesEncontrados"));
        Assert.Equal(0m, (await new DashboardService(test.Db).ObtenerAsync()).TotalAdeudado);
    }

    [Fact]
    public async Task Pedidos_ActivosIncluyeSoloPendienteYConfirmadoYPermiteVolverATodos()
    {
        await using var test = await TestDatabase.CreateAsync();
        foreach (var estado in Enum.GetValues<EstadoPedido>())
        {
            var pedido = await test.CrearPedidoAsync(TipoPedido.Catalogo, "PED-" + estado);
            pedido.Estado = estado;
        }
        await test.Db.SaveChangesAsync();
        var pagina = new Pedidos();
        Establecer(pagina, "PedidoService", new PedidoService(test.Db));
        Establecer(pagina, "Logger", NullLogger<Pedidos>.Instance);
        Establecer(pagina, "EstadoConsulta", "activos");
        await ParametrosAsync(pagina);
        var activos = Obtener<IReadOnlyList<PedidoDto>>(pagina, "PedidosEncontrados");
        Assert.Equal(2, activos.Count);
        Assert.Contains(activos, x => x.Estado == EstadoPedido.Pendiente);
        Assert.Contains(activos, x => x.Estado == EstadoPedido.Confirmado);
        Assert.DoesNotContain(activos, x => x.Estado is EstadoPedido.Cancelado or EstadoPedido.Completado);
        Assert.Equal(activos.Count, (await new DashboardService(test.Db).ObtenerAsync()).PedidosActivos);
        Establecer(pagina, "EstadoConsulta", null);
        await ParametrosAsync(pagina);
        Assert.Equal(4, Obtener<IReadOnlyList<PedidoDto>>(pagina, "PedidosEncontrados").Count);
    }

    [Theory]
    [InlineData("disponible", 1)]
    [InlineData("VENDIDA", 1)]
    [InlineData(null, 5)]
    [InlineData("inexistente", 5)]
    [InlineData("99", 5)]
    public async Task Inventario_QuerySeleccionaEstadoYDashboardCuentaSoloVendida(string? consulta, int cantidad)
    {
        await using var test = await TestDatabase.CreateAsync();
        await new CompraService(test.Db).RegistrarAsync(test.Compra(OrigenCompra.CompraLocal,
            "COM-FILTRO", new DateOnly(2026, 9, 8), cantidad: 5));
        var unidades = await test.Db.UnidadesInventario.OrderBy(x => x.Id).ToListAsync();
        var estados = Enum.GetValues<EstadoUnidadInventario>();
        for (var i = 0; i < unidades.Count; i++) unidades[i].Estado = estados[i];
        await test.Db.SaveChangesAsync();
        var pagina = new Inventario();
        var navegacion = new NavegacionPrueba();
        Establecer(pagina, "Navigation", navegacion);
        Establecer(pagina, "InventarioService", new InventarioService(test.Db));
        Establecer(pagina, "Logger", NullLogger<Inventario>.Instance);
        Establecer(pagina, "EstadoConsulta", consulta);
        await ParametrosAsync(pagina);
        var filtradas = Obtener<IReadOnlyList<UnidadInventarioDto>>(pagina, "Unidades");
        Assert.Equal(cantidad, filtradas.Count);
        if (cantidad == 1)
        {
            Assert.Equal(Enum.Parse<EstadoUnidadInventario>(consulta!, true), Assert.Single(filtradas).Estado);
            await InvocarAsync(pagina, "LimpiarFiltrosAsync");
            Assert.Equal("https://localhost/inventario", navegacion.Uri);
        }
        Establecer(pagina, "EstadoConsulta", null);
        await ParametrosAsync(pagina);
        Assert.Equal(5, Obtener<IReadOnlyList<UnidadInventarioDto>>(pagina, "Unidades").Count);
        var dashboard = await new DashboardService(test.Db).ObtenerAsync();
        Assert.Equal(1, dashboard.PendientesEntrega);
        Assert.Equal(1, dashboard.UnidadesDisponibles);
        Assert.Equal(40m, dashboard.ValorInventarioDisponible);
        unidades.Single(x => x.Estado == EstadoUnidadInventario.Vendida).Estado = EstadoUnidadInventario.Entregada;
        await test.Db.SaveChangesAsync();
        Assert.Equal(0, (await new DashboardService(test.Db).ObtenerAsync()).PendientesEntrega);
    }

    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static void Establecer(object x, string propiedad, object? valor) => x.GetType().GetProperty(propiedad, Flags)!.SetValue(x, valor);
    private static T Obtener<T>(object x, string propiedad) => (T)x.GetType().GetProperty(propiedad, Flags)!.GetValue(x)!;
    private static Task InvocarAsync(object x, string metodo) => (Task)x.GetType().GetMethod(metodo, Flags)!.Invoke(x, null)!;
    private static Task ParametrosAsync(object x) => InvocarAsync(x, "OnParametersSetAsync");
    private sealed class NavegacionPrueba : NavigationManager
    {
        public NavegacionPrueba() => Initialize("https://localhost/", "https://localhost/inventario?estado=vendida");
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }
}
