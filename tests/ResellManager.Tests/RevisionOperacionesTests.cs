using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Pages;
using ResellManager.Web.Components.Pedidos;
using ResellManager.Web.Components.Shared;
using ResellManager.Web.Components.Ventas;

namespace ResellManager.Tests;

public sealed class RevisionOperacionesTests
{
    [Fact]
    public async Task Pedido_RevisarYEditarNoPersisten_ConfirmarUnaVezConservaDatos()
    {
        await using var test = await TestDatabase.CreateAsync();
        var pagina = new PedidoNuevo();
        Set(pagina, "PedidoService", new PedidoService(test.Db));
        Set(pagina, "Productos", await new ProductoService(test.Db).ListarAsync());
        Set(pagina, "Clientes", await new ClienteService(test.Db).ListarAsync());
        Set(pagina, "Logger", NullLogger<PedidoNuevo>.Instance);
        Set(pagina, "Navigation", new Navegacion());
        var modelo = Get<PedidoFormModel>(pagina, "Modelo");
        modelo.ClienteId = test.Cliente.Id;
        modelo.Detalles[0].ProductoId = test.Producto.Id;
        modelo.Detalles[0].Cantidad = 2;
        modelo.Detalles[0].PrecioUnitario = 85m;
        Call(pagina, "RevisarPedido");
        Assert.True(Get<bool>(pagina, "RevisandoPedido"));
        Assert.Empty(await test.Db.Pedidos.ToListAsync());
        Call(pagina, "EditarPedido");
        await CallAsync(pagina, "ConfirmarPedidoAsync");
        Assert.Empty(await test.Db.Pedidos.ToListAsync());
        Assert.Same(modelo, Get<PedidoFormModel>(pagina, "Modelo"));
        Assert.Equal(170m, modelo.Detalles.Sum(x => x.Cantidad * x.PrecioUnitario));
        Call(pagina, "RevisarPedido");
        await CallAsync(pagina, "ConfirmarPedidoAsync");
        await CallAsync(pagina, "ConfirmarPedidoAsync");
        Assert.Single(await test.Db.Pedidos.ToListAsync());
        Assert.False(Get<bool>(pagina, "RevisandoPedido"));
    }

    [Fact]
    public async Task VentaDirecta_RevisarNoCreaPedidoNiVenta_ConfirmarConservaFlujo()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearUnidadDisponibleAsync("COM-REVISION");
        var formulario = new VentaDirectaForm();
        Set(formulario, "PedidoService", new PedidoService(test.Db));
        Set(formulario, "VentaService", new VentaService(test.Db));
        Set(formulario, "InventarioService", new InventarioService(test.Db));
        Set(formulario, "SeleccionService", new SeleccionOperativaService(test.Db));
        Set(formulario, "ClienteService", new ClienteService(test.Db));
        Set(formulario, "ProductoService", new ProductoService(test.Db));
        Set(formulario, "Logger", NullLogger<VentaDirectaForm>.Instance);
        Set(formulario, "Navigation", new Navegacion());
        await CallAsync(formulario, "CargarAsync");
        Call(formulario, "ElegirCliente", (await new ClienteService(test.Db).ListarAsync()).Single());
        await CallAsync(formulario, "AgregarUnidadAsync", (await new InventarioService(test.Db).ListarDisponiblesAsync()).Single());
        var modelo = Get<VentaDirectaFormModel>(formulario, "Modelo");
        modelo.ClienteId = test.Cliente.Id;
        var unidad = Get<List<UnidadVentaDirectaFormModel>>(formulario, "Unidades")[0];
        unidad.Seleccionada = true;
        unidad.PrecioFinal = 100m;
        await CallAsync(formulario, "RevisarVentaDirectaAsync");
        Assert.Empty(await test.Db.Pedidos.ToListAsync());
        Assert.Empty(await test.Db.Ventas.ToListAsync());
        await CallAsync(formulario, "EditarVentaAsync");
        await CallAsync(formulario, "ConfirmarVentaAsync");
        Assert.Empty(await test.Db.Ventas.ToListAsync());
        Assert.True(unidad.Seleccionada);
        await CallAsync(formulario, "RevisarVentaDirectaAsync");
        await CallAsync(formulario, "ConfirmarVentaAsync");
        await CallAsync(formulario, "ConfirmarVentaAsync");
        var pedido = Assert.Single(await test.Db.Pedidos.ToListAsync());
        Assert.Equal(TipoPedido.VentaDirecta, pedido.TipoPedido);
        Assert.Equal(CanalVenta.Presencial, pedido.CanalVenta);
        Assert.StartsWith("PED-VD-", pedido.CodigoInterno);
        Assert.StartsWith("VEN-VD-", Assert.Single(await test.Db.Ventas.ToListAsync()).CodigoInterno);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inventario_RecepcionYEntregaRequierenConfirmacion(bool entrega)
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadImportadaAsync("COM-INV-REV");
        if (entrega) { unidad.Estado = EstadoUnidadInventario.Vendida; await test.Db.SaveChangesAsync(); }
        var estadoInicial = unidad.Estado;
        var pagina = new Inventario();
        Set(pagina, "InventarioService", new InventarioService(test.Db));
        Set(pagina, "Logger", NullLogger<Inventario>.Instance);
        await CallAsync(pagina, "CargarInventarioAsync");
        var dto = Assert.Single(Get<IReadOnlyList<UnidadInventarioDto>>(pagina, "Unidades"));
        if (entrega) await CallAsync(pagina, "RevisarCambioAsync", dto, EstadoUnidadInventario.Entregada);
        else
        {
            Get<HashSet<int>>(pagina, "UnidadesSeleccionadas").Add(unidad.Id);
            Call(pagina, "RevisarRecepcion");
        }
        Assert.Equal(estadoInicial, unidad.Estado);
        Call(pagina, "CerrarRevisionInventario");
        await CallAsync(pagina, "ConfirmarInventarioAsync");
        Assert.Equal(estadoInicial, unidad.Estado);
        if (entrega) await CallAsync(pagina, "RevisarCambioAsync", dto, EstadoUnidadInventario.Entregada);
        else Call(pagina, "RevisarRecepcion");
        await CallAsync(pagina, "ConfirmarInventarioAsync");
        Assert.Equal(entrega ? EstadoUnidadInventario.Entregada : EstadoUnidadInventario.Disponible, unidad.Estado);
    }

    [Fact]
    public async Task ConfirmacionComun_BloqueaDobleClickYEscapeDuranteEnvio()
    {
        var respuesta = new TaskCompletionSource();
        var dialogo = new ConfirmacionOperacion();
        var llamadas = 0;
        var ediciones = 0;
        Set(dialogo, "OnConfirmar", EventCallback.Factory.Create(new object(), async () => { llamadas++; await respuesta.Task; }));
        Set(dialogo, "OnEditar", EventCallback.Factory.Create(new object(), () => ediciones++));
        var primera = CallAsync(dialogo, "ConfirmarAsync");
        await CallAsync(dialogo, "ConfirmarAsync");
        await dialogo.CancelarDesdeTeclado();
        Assert.Equal(1, llamadas);
        Assert.Equal(0, ediciones);
        respuesta.SetResult();
        await primera;
        await dialogo.CancelarDesdeTeclado();
        Assert.Equal(1, ediciones);
    }

    internal const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    internal static void Set(object x, string property, object? value) => x.GetType().GetProperty(property, Flags)!.SetValue(x, value);
    internal static T Get<T>(object x, string property) => (T)x.GetType().GetProperty(property, Flags)!.GetValue(x)!;
    internal static object? Call(object x, string method, params object[] args) => x.GetType().GetMethod(method, Flags)!.Invoke(x, args);
    internal static Task CallAsync(object x, string method, params object[] args) => (Task)Call(x, method, args)!;
    internal sealed class Navegacion : NavigationManager
    {
        public Navegacion() => Initialize("https://localhost/", "https://localhost/");
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }
}
