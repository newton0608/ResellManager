using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Pages;
using ResellManager.Web.Components.Pedidos;
using ResellManager.Web.Components.Ventas;

namespace ResellManager.Tests;

public sealed class Fase510CodigosTests
{
    [Theory]
    [InlineData(false, "PED-")]
    [InlineData(true, "VEN-")]
    public void CodigoNormal_EsGuidCompletoUnicoSinCapturaManual(bool venta, string prefijo)
    {
        var codigos = Enumerable.Range(0, 64)
            .Select(_ => venta ? CodigosInternos.CrearCodigoVenta() : CodigosInternos.CrearCodigoPedido())
            .ToArray();

        Assert.Equal(codigos.Length, codigos.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codigos, codigo =>
        {
            Assert.Matches($"^{prefijo}[A-F0-9]{{32}}$", codigo);
            Assert.True(Guid.TryParseExact(codigo[prefijo.Length..], "N", out _));
            Assert.Equal(36, codigo.Length);
        });
        Assert.Null((venta ? typeof(VentaFormModel) : typeof(PedidoFormModel)).GetProperty("CodigoInterno"));
    }

    [Fact]
    public async Task PedidoNormal_DobleSubmitNoDuplicaYReintentoConservaCodigo()
    {
        await using var test = await TestDatabase.CreateAsync();
        var espera = new TaskCompletionSource<ServiceResult<PedidoDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var servicioReal = new PedidoService(test.Db);
        var pedidos = new PedidoRegistradoService((input, intento) =>
            intento == 1 ? espera.Task : servicioReal.CrearAsync(input));
        var pagina = new PedidoNuevo();
        var navigation = new NavegacionPrueba();
        Establecer(pagina, "PedidoService", pedidos);
        Establecer(pagina, "Logger", NullLogger<PedidoNuevo>.Instance);
        Establecer(pagina, "Navigation", navigation);
        Establecer(pagina, "Productos", await new ProductoService(test.Db).ListarAsync());
        var modelo = Obtener<PedidoFormModel>(pagina, "Modelo");
        modelo.ClienteId = test.Cliente.Id;
        modelo.TipoPedido = TipoPedido.Apartado;
        modelo.Detalles[0].ProductoId = test.Producto.Id;
        modelo.Detalles[0].PrecioUnitario = 120m;
        var codigo = Obtener<string>(pagina, "CodigoPedido");

        var primero = InvocarAsync(pagina, "GuardarAsync");
        Assert.True(Obtener<bool>(pagina, "Guardando"));
        await InvocarAsync(pagina, "GuardarAsync");
        Assert.Single(pedidos.Intentos);
        espera.SetResult(ServiceResult<PedidoDto>.Failure("No fue posible guardar; intenta nuevamente."));
        await primero;
        Assert.False(Obtener<bool>(pagina, "Guardando"));
        Assert.Contains("intenta nuevamente", Obtener<string>(pagina, "ErrorGuardado"));

        await InvocarAsync(pagina, "GuardarAsync");

        Assert.Equal(2, pedidos.Intentos.Count);
        Assert.All(pedidos.Intentos, input => Assert.Equal(codigo, input.CodigoInterno));
        Assert.Equal(codigo, (await test.Db.Pedidos.SingleAsync()).CodigoInterno);
        Assert.Contains("/pedidos/", navigation.Uri);
        Assert.False(Obtener<bool>(pagina, "Guardando"));
    }

    [Fact]
    public async Task VentaNormal_DobleSubmitNoDuplicaYReintentoConservaCodigo()
    {
        await using var test = await TestDatabase.CreateAsync();
        var pedido = await test.CrearPedidoAsync(TipoPedido.Catalogo, "PED-ORIGEN-CATALOGO");
        var espera = new TaskCompletionSource<ServiceResult<VentaDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var servicioReal = new VentaService(test.Db);
        var ventas = new VentaRegistradaService((input, intento) =>
            intento == 1 ? espera.Task : servicioReal.RegistrarDesdePedidoAsync(input));
        var pedidos = new PedidoService(test.Db);
        var pagina = new VentaNueva();
        var navigation = new NavegacionPrueba();
        Establecer(pagina, "VentaService", ventas);
        Establecer(pagina, "PedidoService", pedidos);
        Establecer(pagina, "Logger", NullLogger<VentaNueva>.Instance);
        Establecer(pagina, "Navigation", navigation);
        Establecer(pagina, "PedidosElegibles", await pedidos.ListarAsync());
        await InvocarAsync(pagina, "SeleccionarPedidoAsync", pedido.Id);
        Obtener<List<DetalleVentaFormModel>>(pagina, "Detalles").Single().CostoUnitario = 40m;
        var codigo = Obtener<string>(pagina, "CodigoVenta");

        var primero = InvocarAsync(pagina, "RegistrarAsync");
        Assert.True(Obtener<bool>(pagina, "Registrando"));
        await InvocarAsync(pagina, "RegistrarAsync");
        Assert.Single(ventas.Intentos);
        espera.SetResult(ServiceResult<VentaDto>.Failure("Venta rechazada temporalmente."));
        await primero;
        Assert.False(Obtener<bool>(pagina, "Registrando"));
        Assert.Contains("rechazada", Obtener<string>(pagina, "ErrorGuardado"));

        await InvocarAsync(pagina, "RegistrarAsync");

        Assert.Equal(2, ventas.Intentos.Count);
        Assert.All(ventas.Intentos, input => Assert.Equal(codigo, input.CodigoInterno));
        Assert.Equal(codigo, (await test.Db.Ventas.SingleAsync()).CodigoInterno);
        Assert.Empty(await test.Db.UnidadesInventario.ToListAsync());
        Assert.Contains("/ventas/", navigation.Uri);
        Assert.False(Obtener<bool>(pagina, "Registrando"));
    }

    [Fact]
    public async Task VentaDirecta_FalloPedidoYFalloVentaConservanAmbosCodigosSinDobleSubmit()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearUnidadDisponibleAsync("COM-DIRECTA-REINTENTO-510");
        var inventario = new InventarioService(test.Db);
        var pedidoReal = new PedidoService(test.Db);
        var ventaReal = new VentaService(test.Db);
        var espera = new TaskCompletionSource<ServiceResult<PedidoDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pedidos = new PedidoRegistradoService((input, intento) =>
            intento == 1 ? espera.Task : pedidoReal.CrearAsync(input));
        var ventas = new VentaRegistradaService((input, intento) => intento == 1
            ? Task.FromResult(ServiceResult<VentaDto>.Failure("Fallo controlado de venta."))
            : ventaReal.RegistrarDesdePedidoAsync(input));
        var formulario = new VentaDirectaForm();
        var ocupacion = new List<bool>();
        Establecer(formulario, "PedidoService", pedidos);
        Establecer(formulario, "VentaService", ventas);
        Establecer(formulario, "InventarioService", inventario);
        Establecer(formulario, "Logger", NullLogger<VentaDirectaForm>.Instance);
        Establecer(formulario, "Navigation", new NavegacionPrueba());
        Establecer(formulario, "OcupadoChanged", EventCallback.Factory.Create<bool>(new object(), ocupacion.Add));
        Establecer(formulario, "Clientes", await new ClienteService(test.Db).ListarAsync());
        Establecer(formulario, "Unidades", (await inventario.ListarDisponiblesAsync()).Select(x =>
            new UnidadVentaDirectaFormModel { Unidad = x, Seleccionada = true, PrecioFinal = 120m }).ToList());
        Obtener<VentaDirectaFormModel>(formulario, "Modelo").ClienteId = test.Cliente.Id;
        var codigoPedido = Obtener<string>(formulario, "CodigoPedidoDirecto");
        var codigoVenta = Obtener<string>(formulario, "CodigoVentaDirecta");

        var primero = InvocarAsync(formulario, "RegistrarVentaDirectaAsync");
        Assert.True(Obtener<bool>(formulario, "Registrando"));
        Assert.Equal(new[] { true }, ocupacion);
        await InvocarAsync(formulario, "RegistrarVentaDirectaAsync");
        Assert.Single(pedidos.Intentos);
        espera.SetResult(ServiceResult<PedidoDto>.Failure("Fallo controlado del pedido."));
        await primero;
        Assert.Empty(ventas.Intentos);
        Assert.Null(Obtener<PedidoDto?>(formulario, "PedidoAutomaticoCreado"));

        await InvocarAsync(formulario, "RegistrarVentaDirectaAsync");
        var pedidoCreado = Obtener<PedidoDto>(formulario, "PedidoAutomaticoCreado");
        Assert.Contains("pero la venta no pudo registrarse", Obtener<string>(formulario, "ErrorGuardado"));
        await InvocarAsync(formulario, "RegistrarVentaDirectaAsync");

        Assert.Matches("^PED-VD-[A-F0-9]{32}$", codigoPedido);
        Assert.Matches("^VEN-VD-[A-F0-9]{32}$", codigoVenta);
        Assert.Equal(2, pedidos.Intentos.Count);
        Assert.All(pedidos.Intentos, input => Assert.Equal(codigoPedido, input.CodigoInterno));
        Assert.Equal(2, ventas.Intentos.Count);
        Assert.All(ventas.Intentos, input =>
        {
            Assert.Equal(codigoVenta, input.CodigoInterno);
            Assert.Equal(pedidoCreado.Id, input.PedidoId);
        });
        Assert.Single(await test.Db.Pedidos.ToListAsync());
        Assert.Single(await test.Db.Ventas.ToListAsync());
        Assert.Equal(EstadoUnidadInventario.Vendida, (await test.Db.UnidadesInventario.SingleAsync()).Estado);
        Assert.Equal(new[] { true, false, true, false, true, false }, ocupacion);
        Assert.False(Obtener<bool>(formulario, "Registrando"));
    }

    [Theory]
    [InlineData("Registrando")]
    [InlineData("DirectaOcupada")]
    [InlineData("CargandoPedido")]
    [InlineData("Cargando")]
    public void SelectorModo_NoCambiaDuranteOperacionYConservaInicializacionDirecta(string bloqueo)
    {
        var pagina = new VentaNueva();
        Establecer(pagina, "Cargando", false);
        Establecer(pagina, bloqueo, true);
        var tipoModo = Obtener<object>(pagina, "ModoActual").GetType();
        var directa = Enum.Parse(tipoModo, "VentaDirecta");
        var desdePedido = Enum.Parse(tipoModo, "DesdePedido");

        Invocar(pagina, "CambiarModo", directa);
        Assert.Equal(desdePedido, Obtener<object>(pagina, "ModoActual"));
        Assert.False(Obtener<bool>(pagina, "DirectaInicializada"));

        Establecer(pagina, bloqueo, false);
        Invocar(pagina, "CambiarModo", directa);
        Assert.Equal(directa, Obtener<object>(pagina, "ModoActual"));
        Invocar(pagina, "CambiarModo", desdePedido);
        Assert.True(Obtener<bool>(pagina, "DirectaInicializada"));
        Assert.Equal(desdePedido, Obtener<object>(pagina, "ModoActual"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("999999")]
    public async Task Venta_EnlacePedidoInvalidoNoDejaCargaEterna(string parametro)
    {
        var pagina = new VentaNueva();
        Establecer(pagina, "PedidoDesdeQuery", parametro);
        Establecer(pagina, "PedidoService", new PedidoRegistradoService((_, _) => throw new NotSupportedException()));
        Establecer(pagina, "Logger", NullLogger<VentaNueva>.Instance);

        await InvocarAsync(pagina, "CargarAsync");

        Assert.False(Obtener<bool>(pagina, "Cargando"));
        Assert.False(string.IsNullOrWhiteSpace(Obtener<string>(pagina, "ErrorGuardado")));
        Assert.Null(Obtener<PedidoDto?>(pagina, "PedidoSeleccionado"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VentaDirecta_InicializadaPermaneceMontadaDuranteRecargaDePedidos(bool cargando)
    {
        var pagina = new VentaNueva();
        Establecer(pagina, "DirectaInicializada", true);
        Establecer(pagina, "Cargando", cargando);
        using var render = new RenderTreeBuilder();

        Invocar(pagina, "BuildRenderTree", render);

        // Se inspecciona el árbol generado para detectar la eliminación accidental del formulario.
#pragma warning disable BL0006
        var frames = render.GetFrames();
        Assert.Single(frames.Array.Take(frames.Count).Where(frame =>
            frame.FrameType == Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrameType.Component
            && frame.ComponentType == typeof(VentaDirectaForm)));
#pragma warning restore BL0006
    }

    [Fact]
    public void ObservacionesPedido_AlineadasCon500YVentaDirectaNoAgregaTextoQueDesborde()
    {
        var modelo = new PedidoFormModel { ClienteId = 1, Observaciones = new string('a', 500) };
        Assert.True(Validator.TryValidateObject(modelo, new ValidationContext(modelo), [], true));
        modelo.Observaciones += "a";
        Assert.False(Validator.TryValidateObject(modelo, new ValidationContext(modelo), [], true));
        var formulario = new VentaDirectaForm();
        var directo = Obtener<VentaDirectaFormModel>(formulario, "Modelo");
        directo.Observaciones = new string('a', 500);

        Assert.Equal(directo.Observaciones, Invocar(formulario, "ConstruirObservacionesPedido"));
        directo.Observaciones = null;
        Assert.Contains("generado automáticamente", Assert.IsType<string>(Invocar(formulario, "ConstruirObservacionesPedido")));
    }

    private const BindingFlags Miembros = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static void Establecer(object instancia, string propiedad, object? valor) =>
        instancia.GetType().GetProperty(propiedad, Miembros)!.SetValue(instancia, valor);

    private static T Obtener<T>(object instancia, string propiedad) =>
        (T)instancia.GetType().GetProperty(propiedad, Miembros)!.GetValue(instancia)!;

    private static object? Invocar(object instancia, string metodo, params object[] argumentos) =>
        instancia.GetType().GetMethod(metodo, Miembros)!.Invoke(instancia, argumentos);

    private static Task InvocarAsync(object instancia, string metodo, params object[] argumentos) =>
        (Task)Invocar(instancia, metodo, argumentos)!;

    private sealed class NavegacionPrueba : NavigationManager
    {
        public NavegacionPrueba() => Initialize("https://localhost/", "https://localhost/");
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }

    private sealed class PedidoRegistradoService(Func<PedidoInput, int, Task<ServiceResult<PedidoDto>>> registrar) : IPedidoService
    {
        public List<PedidoInput> Intentos { get; } = [];
        public Task<ServiceResult<PedidoDto>> CrearAsync(PedidoInput input, CancellationToken ct = default)
        {
            Intentos.Add(input);
            return registrar(input, Intentos.Count);
        }
        public Task<IReadOnlyList<PedidoDto>> ListarAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PedidoDto>>([]);
        public Task<ServiceResult<PedidoDto>> ObtenerPorIdAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ServiceResult<PedidoDto>> AgregarDetalleAsync(int pedidoId, DetallePedidoInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ServiceResult> CancelarAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class VentaRegistradaService(Func<VentaInput, int, Task<ServiceResult<VentaDto>>> registrar) : IVentaService
    {
        public List<VentaInput> Intentos { get; } = [];
        public Task<ServiceResult<VentaDto>> RegistrarDesdePedidoAsync(VentaInput input, CancellationToken ct = default)
        {
            Intentos.Add(input);
            return registrar(input, Intentos.Count);
        }
        public Task<ServiceResult<VentaDto>> ObtenerPorIdAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VentaDto>> ListarAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ServiceResult<decimal>> CalcularTotalAsync(int ventaId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ServiceResult> CancelarAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
