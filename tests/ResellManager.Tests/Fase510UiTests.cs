using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;
using ResellManager.Infrastructure.Storage;
using ResellManager.Web.Components.Clientes;
using ResellManager.Web.Components.Compras;
using ResellManager.Web.Components.Inventario;
using ResellManager.Web.Components.Pages;
using ResellManager.Web.Components.Pedidos;
using ResellManager.Web.Components.Productos;
using ResellManager.Web.Components.Ventas;

namespace ResellManager.Tests;

[Collection("Integración web")]
public sealed class Fase510UiTests
{
    [Fact]
    public async Task HostsDeIntegracion_UsanSQLiteYComprobantesDistintosSinCompartirDatos()
    {
        using var primera = new AplicacionAutenticacionFactory();
        using var segunda = new AplicacionAutenticacionFactory();
        using var clientePrimera = CrearCliente(primera);
        using var clienteSegunda = CrearCliente(segunda);
        await using var scopePrimera = primera.Services.CreateAsyncScope();
        await using var scopeSegunda = segunda.Services.CreateAsyncScope();
        var dbPrimera = scopePrimera.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
        var dbSegunda = scopeSegunda.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
        var conexionPrimera = new SqliteConnectionStringBuilder(dbPrimera.Database.GetConnectionString());
        var conexionSegunda = new SqliteConnectionStringBuilder(dbSegunda.Database.GetConnectionString());
        var comprobantesPrimera = scopePrimera.ServiceProvider
            .GetRequiredService<IOptions<AlmacenamientoComprobantesOptions>>().Value.DirectorioBase;
        var comprobantesSegunda = scopeSegunda.ServiceProvider
            .GetRequiredService<IOptions<AlmacenamientoComprobantesOptions>>().Value.DirectorioBase;

        Assert.Equal(primera.RutaBaseDatos, conexionPrimera.DataSource);
        Assert.Equal(segunda.RutaBaseDatos, conexionSegunda.DataSource);
        Assert.NotEqual(conexionPrimera.DataSource, conexionSegunda.DataSource);
        Assert.False(conexionPrimera.Pooling);
        Assert.False(conexionSegunda.Pooling);
        Assert.Equal(Path.GetFullPath(primera.RutaComprobantes), comprobantesPrimera);
        Assert.Equal(Path.GetFullPath(segunda.RutaComprobantes), comprobantesSegunda);
        Assert.NotEqual(comprobantesPrimera, comprobantesSegunda);
        Assert.Empty(await dbPrimera.Clientes.ToListAsync());
        Assert.Empty(await dbSegunda.Clientes.ToListAsync());

        var creado = await new ClienteService(dbPrimera).CrearAsync(
            new ClienteInput("Solo existe en el primer host", null, "555-0510", null, null));
        Assert.True(creado.IsSuccess, creado.ErrorMessage);
        Assert.Single(await dbPrimera.Clientes.ToListAsync());
        Assert.Empty(await dbSegunda.Clientes.ToListAsync());
    }

    [Fact]
    public async Task ListadosVacios_MuestranContextoYAccionParaContinuar()
    {
        using var factory = new AplicacionAutenticacionFactory();
        using var cliente = CrearCliente(factory);
        await IniciarSesionAsync(cliente);
        var casos = new (string Ruta, string Mensaje, string Destino)[]
        {
            ("/clientes", "Aún no hay clientes", "/clientes/nuevo"),
            ("/productos", "Aún no hay productos", "/productos/nuevo"),
            ("/categorias", "Aún no hay categorías", "/categorias/nueva"),
            ("/inventario", "Aún no hay unidades de inventario", "/compras/nueva"),
            ("/pedidos", "Aún no hay pedidos", "/pedidos/nuevo"),
            ("/ventas", "Aún no hay ventas", "/ventas/nueva"),
            ("/pagos", "No hay clientes registrados", "/clientes/nuevo"),
            ("/compras", "Aún no hay compras", "/compras/nueva"),
            ("/proveedores", "Aún no hay proveedores", "/proveedores/nuevo"),
        };
        foreach (var (ruta, mensaje, destino) in casos)
        {
            var html = await ObtenerPaginaAsync(cliente, ruta);
            Assert.True(html.Contains(mensaje, StringComparison.Ordinal),
                $"{ruta}: {string.Join(" | ", Regex.Matches(html, "<h[12][^>]*>(.*?)</h[12]>").Select(x => x.Groups[1].Value))}");
            var vacio = Regex.Match(html, "<section class=\"empty-state\"[\\s\\S]*?</section>");
            Assert.True(vacio.Success, ruta);
            Assert.Contains($"href=\"{destino}\"", vacio.Value);
            Assert.DoesNotContain("<table", html);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
        var nuevo = await new ClienteService(db).CrearAsync(
            new ClienteInput("Cliente sin pagos", null, "555-0510", null, null));
        Assert.True(nuevo.IsSuccess, nuevo.ErrorMessage);
        var pagos = await ObtenerPaginaAsync(cliente, $"/pagos?cliente={nuevo.Value!.Id}");
        Assert.Contains("Aún no hay pagos", pagos);
        Assert.Contains("No necesita registrar otro pago", pagos);
        Assert.DoesNotContain("<table", pagos);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("999999")]
    public async Task DetallesYEdiciones_IdInvalidoONoExistente_TerminanEnEstadoControlado(string id)
    {
        using var factory = new AplicacionAutenticacionFactory();
        using var cliente = CrearCliente(factory);
        await IniciarSesionAsync(cliente);
        var casos = new (string Ruta, string Mensaje)[]
        {
            ($"/clientes/{id}", "No fue posible abrir el cliente"),
            ($"/productos/{id}", "No fue posible abrir el producto"),
            ($"/pedidos/{id}", "No fue posible abrir el pedido"),
            ($"/ventas/{id}", "No fue posible cargar la venta"),
            ($"/compras/{id}", "No fue posible cargar la compra"),
            ($"/clientes/{id}/editar", "No fue posible abrir el cliente"),
            ($"/productos/{id}/editar", "No fue posible abrir el formulario"),
            ($"/categorias/{id}/editar", "No fue posible abrir la categoría"),
        };
        foreach (var (ruta, mensaje) in casos)
        {
            var html = await ObtenerPaginaAsync(cliente, ruta);
            Assert.Contains(mensaje, html);
            Assert.Contains("role=\"alert\"", html);
            Assert.DoesNotContain("class=\"loading-state\"", html);
            Assert.DoesNotContain("class=\"form-panel\"", html);
            Assert.DoesNotContain("NullReferenceException", html);
        }
    }

    [Fact]
    public async Task RutasDesconocidasYIdsNoNumericos_Renderizan404ConSalidaUtil()
    {
        using var factory = new AplicacionAutenticacionFactory();
        using var cliente = CrearCliente(factory);
        await IniciarSesionAsync(cliente);
        foreach (var ruta in new[]
        {
            "/ruta-v1-inexistente", "/clientes/abc", "/productos/abc", "/pedidos/abc",
            "/ventas/abc", "/compras/abc", "/categorias/abc/editar",
        })
        {
            var html = await ObtenerPaginaAsync(cliente, ruta, HttpStatusCode.NotFound);
            Assert.Contains("Página no encontrada", html);
            Assert.Contains("Volver al inicio", html);
            Assert.DoesNotContain("Exception", html);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("999999")]
    public async Task SeleccionDesdeEnlaceInvalida_MuestraErrorAunqueNoHayaClienteOPedido(string id)
    {
        using var factory = new AplicacionAutenticacionFactory();
        using var cliente = CrearCliente(factory);
        await IniciarSesionAsync(cliente);
        var pagos = await ObtenerPaginaAsync(cliente, $"/pagos?cliente={id}");
        Assert.Contains("El cliente solicitado no está disponible", pagos);
        Assert.Contains("role=\"alert\"", pagos);
        var venta = await ObtenerPaginaAsync(cliente, $"/ventas/nueva?pedido={id}");
        Assert.Contains("role=\"alert\"", venta);
        Assert.Contains(id == "999999"
            ? "El pedido seleccionado ya no es elegible para venta."
            : "El enlace contiene un pedido inválido.", venta);
        Assert.DoesNotContain("Exception", pagos + venta);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PipelineNoEncontrado_PreservaMetodoYEstadoHttpSinReejecutarMutaciones(bool autenticado)
    {
        using var factory = new AplicacionAutenticacionFactory();
        using var cliente = CrearCliente(factory);
        if (autenticado)
            await IniciarSesionAsync(cliente);

        using var post = await cliente.PostAsync("/ruta-post-inexistente", new StringContent(string.Empty));
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        Assert.Null(post.Headers.Location);
        Assert.DoesNotContain("Página no encontrada", WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync()));

        using var logoutPorGet = await cliente.GetAsync("/account/logout");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, logoutPorGet.StatusCode);
        Assert.Null(logoutPorGet.Headers.Location);
        Assert.DoesNotContain("Página no encontrada", WebUtility.HtmlDecode(await logoutPorGet.Content.ReadAsStringAsync()));

        using var desconocida = await cliente.GetAsync("/ruta-get-inexistente");
        Assert.Equal(autenticado ? HttpStatusCode.NotFound : HttpStatusCode.Redirect, desconocida.StatusCode);
        if (autenticado)
            Assert.Contains("Página no encontrada", WebUtility.HtmlDecode(await desconocida.Content.ReadAsStringAsync()));
        else
            Assert.Equal("/login", desconocida.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task PedidoConVentaCancelada_RecuperaCancelacionYConservaNavegacionALaVenta()
    {
        using var factory = new AplicacionAutenticacionFactory();
        using var cliente = CrearCliente(factory);
        await IniciarSesionAsync(cliente);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
        var categoria = new Categoria { Nombre = "Categoría cierre UI" };
        var comprador = new Cliente { Nombres = "Cliente cierre UI", Telefono = "555-0510" };
        var producto = new Producto
        {
            CodigoInterno = "SKU-UI-510", Nombre = "Producto cierre UI", Categoria = categoria,
            PrecioSugerido = 150m,
        };
        db.AddRange(categoria, comprador, producto);
        await db.SaveChangesAsync();
        var pedidos = new PedidoService(db);
        var pedido = await pedidos.CrearAsync(new PedidoInput(
            "PED-UI-CIERRE", new DateOnly(2026, 9, 3), TipoPedido.Catalogo,
            CanalVenta.WhatsApp, comprador.Id, null,
            [new DetallePedidoInput(producto.Id, 1, 150m, null)]));
        Assert.True(pedido.IsSuccess, pedido.ErrorMessage);
        var ventas = new VentaService(db);
        var venta = await ventas.RegistrarDesdePedidoAsync(new VentaInput(
            pedido.Value!.Id, "VEN-UI-CIERRE", new DateOnly(2026, 9, 3), null,
            [new DetalleVentaInput(null, producto.Id, 50m, 150m, null)]));
        Assert.True(venta.IsSuccess, venta.ErrorMessage);

        var registrada = await ObtenerPaginaAsync(cliente, $"/pedidos/{pedido.Value.Id}");
        Assert.DoesNotMatch("<button[^>]*>\\s*Cancelar pedido\\s*</button>", registrada);
        Assert.Contains($"href=\"/ventas/{venta.Value!.Id}\"", registrada);
        var cancelacion = await ventas.CancelarAsync(venta.Value.Id);
        Assert.True(cancelacion.IsSuccess, cancelacion.ErrorMessage);
        var cancelada = await ObtenerPaginaAsync(cliente, $"/pedidos/{pedido.Value.Id}");
        Assert.Matches("<button[^>]*>\\s*Cancelar pedido\\s*</button>", cancelada);
        Assert.Contains("Cancelada", cancelada);
        Assert.Contains("Pendiente", cancelada);
        Assert.Contains($"href=\"/ventas/{venta.Value.Id}\"", cancelada);
        Assert.DoesNotContain($"/ventas/nueva?pedido={pedido.Value.Id}", cancelada);
    }

    [Theory]
    [InlineData("es-GT")]
    [InlineData("es-ES")]
    [InlineData("en-US")]
    public void Moneda_EsConsistenteEnTodosLosModulosAunqueCambieCulturaDelProceso(string cultura)
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultura);
            Func<decimal, string>[] formatos =
            [ClientePresentacion.Moneda, ProductoPresentacion.PrecioSugerido, PedidoPresentacion.Moneda,
                VentaPresentacion.Moneda, CompraPresentacion.Moneda, InventarioPresentacion.Moneda];
            Assert.All(formatos, formato =>
            {
                Assert.Equal("Q 1,234.56", formato(1234.56m));
                Assert.Equal("Q 0.00", formato(0m));
            });
            Assert.Equal("Envío del hijo", CompraPresentacion.Origen(OrigenCompra.EnvioHermano));
            Assert.Equal(CompraPresentacion.Origen(OrigenCompra.EnvioHermano),
                InventarioPresentacion.Origen(OrigenCompra.EnvioHermano));
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmacionDeCancelacion_BloqueaSegundoClickAntesDelDialogoYLiberaGuardia(bool reserva)
    {
        await using var test = await TestDatabase.CreateAsync();
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-GUARDIA-UI");
        var pedidos = new PedidoService(test.Db);
        var inventario = new InventarioService(test.Db);
        var unidad = await test.CrearUnidadDisponibleAsync("COM-GUARDIA-UI");
        Assert.True((await inventario.ReservarAsync(unidad.Id, pedido.Detalles.Single().Id)).IsSuccess);
        var dto = (await inventario.ListarAsync()).Single(x => x.Id == unidad.Id);
        var pagina = new PedidoDetalle();
        var js = new ConfirmacionPendiente();
        Establecer(pagina, "Pedido", (await pedidos.ObtenerPorIdAsync(pedido.Id)).Value!);
        Establecer(pagina, "Id", pedido.Id);
        Establecer(pagina, "JS", js);
        Establecer(pagina, "Logger", NullLogger<PedidoDetalle>.Instance);
        // Los servicios no se inyectan: rechazar el diálogo no debe alcanzar ninguna mutación.
        var metodo = reserva ? "CancelarReservaAsync" : "CancelarPedidoAsync";
        object?[] argumentos = reserva ? [dto] : [];
        var primera = InvocarAsync(pagina, metodo, argumentos);
        Assert.True(Obtener<bool>(pagina, "Procesando"));
        Assert.Equal(1, js.Llamadas);
        await InvocarAsync(pagina, metodo, argumentos);
        Assert.Equal(1, js.Llamadas);
        js.Respuesta.SetResult(false);
        await primera;
        Assert.False(Obtener<bool>(pagina, "Procesando"));
        Assert.Null(Obtener<string?>(pagina, "ErrorOperacion"));
        Assert.Equal(EstadoPedido.Pendiente, (await pedidos.ObtenerPorIdAsync(pedido.Id)).Value!.Estado);
        Assert.Equal(pedido.Detalles.Single().Id,
            (await inventario.ListarAsync()).Single(x => x.Id == unidad.Id).DetallePedidoReservaId);
    }

    private static HttpClient CrearCliente(AplicacionAutenticacionFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

    private static async Task<string> ObtenerPaginaAsync(HttpClient cliente, string ruta,
        HttpStatusCode esperado = HttpStatusCode.OK)
    {
        using var respuesta = await cliente.GetAsync(ruta);
        Assert.Equal(esperado, respuesta.StatusCode);
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
    }

    private static async Task IniciarSesionAsync(HttpClient cliente)
    {
        var login = await ObtenerPaginaAsync(cliente, "/login");
        var etiqueta = Regex.Match(login, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>");
        Assert.True(etiqueta.Success);
        var token = Regex.Match(etiqueta.Value, "value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        using var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["correo"] = AplicacionAutenticacionFactory.CorreoUsuario,
            ["contrasena"] = AplicacionAutenticacionFactory.ContrasenaValida,
            ["__RequestVerificationToken"] = token,
        });
        using var respuesta = await cliente.PostAsync("/account/login", formulario);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/", respuesta.Headers.Location?.OriginalString);
    }

    private static void Establecer(object instancia, string propiedad, object valor) =>
        instancia.GetType().GetProperty(propiedad,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(instancia, valor);

    private static T Obtener<T>(object instancia, string propiedad) =>
        (T)instancia.GetType().GetProperty(propiedad,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(instancia)!;

    private static Task InvocarAsync(object instancia, string metodo, object?[] argumentos) =>
        (Task)instancia.GetType().GetMethod(metodo,
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instancia, argumentos)!;

    private sealed class ConfirmacionPendiente : IJSRuntime
    {
        public int Llamadas { get; private set; }
        public TaskCompletionSource<bool> Respuesta { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier,
            CancellationToken cancellationToken, object?[]? args)
        {
            Assert.Equal("confirm", identifier);
            Llamadas++;
            return (TValue)(object)await Respuesta.Task;
        }
    }
}
