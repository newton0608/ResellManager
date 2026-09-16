using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ResellManager.Application.Common;
using ResellManager.Infrastructure.Storage;

namespace ResellManager.Tests;

[Collection("Integración web")]
public sealed class AutenticacionIntegracionTests : PruebaWebAislada
{
    [Fact]
    public async Task UsuarioNoAutenticado_NoPuedeAccederAlInicioProtegido()
    {
        using var cliente = CrearCliente();

        var respuesta = await cliente.GetAsync("/");

        AssertRedirigeAlLogin(respuesta);
    }

    [Fact]
    public async Task LoginValido_AutenticaYPermiteAccederAlInicio()
    {
        using var cliente = CrearCliente();

        var respuestaLogin = await IniciarSesionAsync(cliente, AplicacionAutenticacionFactory.ContrasenaValida);

        Assert.Equal(HttpStatusCode.Redirect, respuestaLogin.StatusCode);
        Assert.Equal("/", respuestaLogin.Headers.Location?.OriginalString);

        var respuestaInicio = await cliente.GetAsync("/");
        var contenido = await respuestaInicio.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, respuestaInicio.StatusCode);
        Assert.Contains(AplicacionAutenticacionFactory.CorreoUsuario, contenido);
        Assert.Contains("Cerrar sesión", contenido);
    }

    [Fact]
    public async Task LoginInvalido_SeRechazaYSigueSinSesion()
    {
        using var cliente = CrearCliente();

        var respuestaLogin = await IniciarSesionAsync(cliente, "Contrasena-Incorrecta1!");

        Assert.Equal(HttpStatusCode.Redirect, respuestaLogin.StatusCode);
        Assert.Equal("/login?error=credenciales", respuestaLogin.Headers.Location?.OriginalString);

        var respuestaInicio = await cliente.GetAsync("/");
        AssertRedirigeAlLogin(respuestaInicio);
    }

    [Fact]
    public async Task Logout_InvalidaLaSesion()
    {
        using var cliente = CrearCliente();
        await IniciarSesionAsync(cliente, AplicacionAutenticacionFactory.ContrasenaValida);

        var paginaInicio = await cliente.GetAsync("/");
        var token = await ObtenerTokenAntiforgeryAsync(paginaInicio);
        using var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["confirmacion"] = "true",
            ["__RequestVerificationToken"] = token
        });

        var respuestaLogout = await cliente.PostAsync("/account/logout", formulario);

        Assert.Equal(HttpStatusCode.Redirect, respuestaLogout.StatusCode);
        Assert.Equal("/login?sesionCerrada=true", respuestaLogout.Headers.Location?.OriginalString);

        var respuestaInicio = await cliente.GetAsync("/");
        AssertRedirigeAlLogin(respuestaInicio);
    }

    [Fact]
    public async Task AplicacionPrivada_NoExponeAutorregistroPublico()
    {
        using var cliente = CrearCliente();

        var paginaLogin = await cliente.GetStringAsync("/login");
        Assert.DoesNotContain("Crear cuenta", paginaLogin, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Registrarse", paginaLogin, StringComparison.OrdinalIgnoreCase);

        var rutas = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText);

        Assert.DoesNotContain("/account/register", rutas);
    }

    [Fact]
    public async Task NavegacionBase_ProtegeYRenderizaLosModulosPrevistos()
    {
        var modulos = new Dictionary<string, string>
        {
            ["/clientes"] = "Clientes",
            ["/productos"] = "Productos",
            ["/inventario"] = "Inventario",
            ["/pedidos"] = "Pedidos",
            ["/ventas"] = "Ventas",
            ["/pagos"] = "Pagos",
            ["/compras"] = "Compras",
            ["/proveedores"] = "Proveedores",
            ["/categorias"] = "Categorías"
        };

        using var clienteAnonimo = CrearCliente();
        foreach (var ruta in modulos.Keys)
        {
            AssertRedirigeAlLogin(await clienteAnonimo.GetAsync(ruta));
        }

        using var clienteAutenticado = CrearCliente();
        await IniciarSesionAsync(clienteAutenticado, AplicacionAutenticacionFactory.ContrasenaValida);

        foreach (var (ruta, nombre) in modulos)
        {
            var respuesta = await clienteAutenticado.GetAsync(ruta);
            var contenido = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            Assert.Contains(nombre, contenido);
            var contenidoEsperado = ruta switch
            {
                "/clientes" => "Nuevo cliente",
                "/productos" => "Nuevo producto",
                "/ventas" => "Nueva venta",
                "/pagos" => "Pagos y abonos",
                "/inventario" => "Buscar unidad",
                "/pedidos" => "Nuevo pedido",
                "/compras" => "Nueva compra",
                "/proveedores" => "Nuevo proveedor",
                "/categorias" => "Nueva categoría",
                _ => "se implementarán en una fase posterior"
            };
            Assert.Contains(contenidoEsperado, contenido);
        }
    }

    [Fact]
    public async Task ConsultaDeComprobante_RequiereAutenticacion()
    {
        using var cliente = CrearCliente();

        var respuesta = await cliente.GetAsync("/comprobantes/1");
        var endpoint = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(x => x.RoutePattern.RawText == "/comprobantes/{compraId:int}");

        AssertRedirigeAlLogin(respuesta);
        Assert.NotNull(endpoint.Metadata.GetMetadata<IAuthorizeData>());
    }

    [Fact]
    public async Task UsuarioAutenticado_PuedeAbrirComprobantePorIdDeCompra()
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF");
        int compraId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
            var categoria = new Categoria { Nombre = $"Categoría comprobante {sufijo}" };
            var proveedor = new Proveedor { Nombre = $"Proveedor comprobante {sufijo}" };
            var producto = new Producto
            {
                CodigoInterno = $"PROD-CMP-{sufijo}",
                Nombre = $"Producto comprobante {sufijo}",
                PrecioSugerido = 50m,
                Categoria = categoria,
            };
            db.AddRange(categoria, proveedor, producto);
            await db.SaveChangesAsync();

            await using var stream = new MemoryStream(pdf);
            var registro = scope.ServiceProvider
                .GetRequiredService<IRegistroCompraConComprobanteService>();
            var resultado = await registro.RegistrarAsync(
                new CompraInput(
                    CodigosInternos.CrearCodigoCompra(),
                    new DateOnly(2026, 9, 1),
                    null,
                    OrigenCompra.Catalogo,
                    proveedor.Id,
                    null,
                    [new DetalleCompraInput(producto.Id, 1, 20m)],
                    null
                ),
                new DatosComprobanteCompraInput(
                    "FAC-WEB",
                    new DateOnly(2026, 9, 1),
                    null
                ),
                new ArchivoComprobanteInput(stream, "factura.pdf", "application/pdf")
            );
            Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
            compraId = resultado.Value!.Id;
        }

        using var cliente = CrearCliente();
        await IniciarSesionAsync(cliente, AplicacionAutenticacionFactory.ContrasenaValida);

        var respuesta = await cliente.GetAsync($"/comprobantes/{compraId}");
        var contenido = await respuesta.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("sandbox", respuesta.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal(pdf, contenido);
    }

    [Fact]
    public async Task InicioAutenticado_RenderizaDashboardConDatosReales()
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var codigoVenta = $"VEN-DASH-{sufijo}";
        var nombreCliente = $"Cliente Dashboard {sufijo}";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
            var categoria = new Categoria { Nombre = $"Categoría Dashboard {sufijo}" };
            var cliente = new Cliente { Nombres = nombreCliente, Telefono = "555-5900" };
            var producto = new Producto
            {
                CodigoInterno = $"PROD-DASH-{sufijo}",
                Nombre = $"Producto Dashboard {sufijo}",
                PrecioSugerido = 400m,
                Categoria = categoria,
            };
            db.AddRange(categoria, cliente, producto);
            await db.SaveChangesAsync();

            var pedido = new Pedido
            {
                CodigoInterno = $"PED-DASH-{sufijo}",
                Fecha = new DateOnly(2099, 12, 30),
                TipoPedido = TipoPedido.Catalogo,
                CanalVenta = CanalVenta.Facebook,
                Estado = EstadoPedido.Pendiente,
                ClienteId = cliente.Id,
                Detalles =
                [
                    new DetallePedido
                    {
                        ProductoId = producto.Id,
                        Cantidad = 1,
                        PrecioUnitario = 321.45m,
                    },
                ],
            };
            db.Pedidos.Add(pedido);
            await db.SaveChangesAsync();

            var venta = await new VentaService(db).RegistrarDesdePedidoAsync(
                new VentaInput(
                    pedido.Id,
                    codigoVenta,
                    new DateOnly(2099, 12, 31),
                    null,
                    [new DetalleVentaInput(null, producto.Id, 111.11m, 321.45m, null)]
                )
            );
            Assert.True(venta.IsSuccess, venta.ErrorMessage);

            var pago = await new PagoService(db).RegistrarAsync(
                new PagoInput(
                    cliente.Id,
                    new DateOnly(2099, 12, 31),
                    21.45m,
                    MetodoPago.Efectivo,
                    null,
                    null
                )
            );
            Assert.True(pago.IsSuccess, pago.ErrorMessage);
        }

        using var clienteHttp = CrearCliente();
        await IniciarSesionAsync(clienteHttp, AplicacionAutenticacionFactory.ContrasenaValida);

        var respuesta = await clienteHttp.GetAsync("/");
        var contenido = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("id=\"titulo-dashboard\">Inicio", contenido);
        Assert.Contains("Ganancia total", contenido);
        Assert.Contains(codigoVenta, contenido);
        Assert.Contains(nombreCliente, contenido);
        Assert.Contains("Q 321.45", contenido);
        Assert.Contains("Q 21.45", contenido);
        Assert.Contains("Efectivo", contenido);
        Assert.Contains("Facebook", contenido);
        Assert.Contains("Presencial", contenido);
        Assert.Contains("WhatsApp", contenido);
        Assert.Contains("Web", contenido);
        Assert.Contains("Otro", contenido);
        Assert.DoesNotContain("Panel en preparación", contenido);
    }

    [Fact]
    public async Task InventarioAutenticado_RenderizaUnidadYSeparaEstadoDeReserva()
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var codigoProducto = $"PROD-INV-{sufijo}";
        var codigoCompra = $"IMP-INV-{sufijo}";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
            var categoria = new Categoria { Nombre = "Categoría inventario web" };
            var proveedor = new Proveedor { Nombre = "Proveedor inventario web" };
            var cliente = new Cliente { Nombres = "Cliente inventario web", Telefono = "555-5500" };
            var producto = new Producto
            {
                CodigoInterno = codigoProducto,
                Nombre = "Producto inventario web",
                PrecioSugerido = 125m,
                Categoria = categoria,
            };
            db.AddRange(categoria, proveedor, cliente, producto);
            await db.SaveChangesAsync();

            var compra = await new CompraService(db).RegistrarAsync(
                new CompraInput(
                    codigoCompra,
                    new DateOnly(2026, 8, 20),
                    null,
                    OrigenCompra.Importacion,
                    proveedor.Id,
                    null,
                    [new DetalleCompraInput(producto.Id, 1, 50m)],
                    null));
            Assert.True(compra.IsSuccess, compra.ErrorMessage);

            var pedido = new Pedido
            {
                CodigoInterno = $"PED-INV-{sufijo}",
                Fecha = new DateOnly(2026, 8, 21),
                TipoPedido = TipoPedido.Apartado,
                CanalVenta = CanalVenta.Otro,
                Estado = EstadoPedido.Pendiente,
                ClienteId = cliente.Id,
                Detalles =
                [
                    new DetallePedido
                    {
                        ProductoId = producto.Id,
                        Cantidad = 1,
                        PrecioUnitario = 125m,
                    },
                ],
            };
            db.Pedidos.Add(pedido);
            await db.SaveChangesAsync();

            var unidad = await db.UnidadesInventario.SingleAsync(x =>
                x.DetalleCompra.Compra.CodigoInterno == codigoCompra);
            var inventario = scope.ServiceProvider.GetRequiredService<IInventarioService>();
            var transito = await inventario.CambiarEstadoAsync(
                unidad.Id,
                EstadoUnidadInventario.EnTransito);
            var reserva = await inventario.ReservarAsync(unidad.Id, pedido.Detalles.Single().Id);
            Assert.True(transito.IsSuccess, transito.ErrorMessage);
            Assert.True(reserva.IsSuccess, reserva.ErrorMessage);
        }

        using var clienteHttp = CrearCliente();
        await IniciarSesionAsync(clienteHttp, AplicacionAutenticacionFactory.ContrasenaValida);

        var respuesta = await clienteHttp.GetAsync("/inventario");
        var contenido = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains($"{codigoCompra}-01-001", contenido);
        Assert.Contains("Ver pedido de la reserva", contenido);
        Assert.Contains("Producto inventario web", contenido);
        Assert.Contains("Estado físico", contenido);
        Assert.Contains("En tránsito", contenido);
        Assert.Contains("Reserva", contenido);
        Assert.Contains("Reservada", contenido);
        Assert.Contains("Recepción de mercancía", contenido);
        Assert.DoesNotContain("Estado: Apartada", contenido, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertRedirigeAlLogin(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/login", respuesta.Headers.Location?.AbsolutePath);
    }

    private HttpClient CrearCliente()
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    private static async Task<HttpResponseMessage> IniciarSesionAsync(HttpClient cliente, string contrasena)
    {
        var paginaLogin = await cliente.GetAsync("/login");
        var token = await ObtenerTokenAntiforgeryAsync(paginaLogin);
        using var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["correo"] = AplicacionAutenticacionFactory.CorreoUsuario,
            ["contrasena"] = contrasena,
            ["__RequestVerificationToken"] = token
        });

        return await cliente.PostAsync("/account/login", formulario);
    }

    private static async Task<string> ObtenerTokenAntiforgeryAsync(HttpResponseMessage respuesta)
    {
        respuesta.EnsureSuccessStatusCode();
        var contenido = await respuesta.Content.ReadAsStringAsync();
        var etiqueta = Regex.Match(
            contenido,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>",
            RegexOptions.IgnoreCase);
        Assert.True(etiqueta.Success, "La página no incluyó el token antiforgery esperado.");

        var valor = Regex.Match(etiqueta.Value, "value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        Assert.True(valor.Success, "El input antiforgery no incluyó un valor.");

        return WebUtility.HtmlDecode(valor.Groups[1].Value);
    }
}

public sealed class AplicacionAutenticacionFactory : WebApplicationFactory<Program>
{
    public const string CorreoUsuario = "administrador@resellmanager.local";
    public const string ContrasenaValida = "Temporal-Segura1!";

    private readonly string _rutaBaseDatos =
        Path.Combine(Path.GetTempPath(), $"resellmanager-auth-{Guid.NewGuid():N}.db");
    private readonly string _rutaComprobantes =
        Path.Combine(Path.GetTempPath(), $"resellmanager-auth-comprobantes-{Guid.NewGuid():N}");

    internal string RutaBaseDatos => _rutaBaseDatos;
    internal string RutaComprobantes => _rutaComprobantes;
    private string CadenaConexion => $"Data Source={_rutaBaseDatos};Pooling=False";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuracion) =>
        {
            configuracion.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ResellManager"] = CadenaConexion,
                ["UsuarioInicial:Correo"] = CorreoUsuario,
                ["UsuarioInicial:Contrasena"] = ContrasenaValida,
                ["AlmacenamientoComprobantes:DirectorioBase"] = _rutaComprobantes
            });
        });
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter>(
            new ComprobarAislamientoAntesDeSolicitudes(CadenaConexion, _rutaComprobantes)));
    }

    private sealed class ComprobarAislamientoAntesDeSolicitudes(
        string cadenaConexionEsperada, string rutaComprobantesEsperada) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> siguiente) => app =>
        {
            using var scope = app.ApplicationServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
            var almacenamiento = scope.ServiceProvider
                .GetRequiredService<IOptions<AlmacenamientoComprobantesOptions>>().Value;
            if (db.Database.GetConnectionString() != cadenaConexionEsperada
                || almacenamiento.DirectorioBase != Path.GetFullPath(rutaComprobantesEsperada))
            {
                throw new InvalidOperationException(
                    "El host de pruebas no está aislado en sus rutas temporales de SQLite y comprobantes.");
            }

            siguiente(app);
        };
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(_rutaBaseDatos))
        {
            File.Delete(_rutaBaseDatos);
        }
        if (disposing && Directory.Exists(_rutaComprobantes))
        {
            Directory.Delete(_rutaComprobantes, recursive: true);
        }
    }
}
