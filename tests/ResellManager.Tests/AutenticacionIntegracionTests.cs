using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ResellManager.Tests;

public sealed class AutenticacionIntegracionTests(AplicacionAutenticacionFactory factory)
    : IClassFixture<AplicacionAutenticacionFactory>
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
            Assert.Contains("se implementarán en una fase posterior", contenido);
        }
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuracion) =>
        {
            configuracion.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ResellManager"] = $"Data Source={_rutaBaseDatos}",
                ["UsuarioInicial:Correo"] = CorreoUsuario,
                ["UsuarioInicial:Contrasena"] = ContrasenaValida
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(_rutaBaseDatos))
        {
            File.Delete(_rutaBaseDatos);
        }
    }
}
