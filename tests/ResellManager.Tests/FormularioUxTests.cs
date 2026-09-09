using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.DTOs;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Clientes;
using ResellManager.Web.Components.Pages;
using ResellManager.Web.Components.Productos;

namespace ResellManager.Tests;

[Collection("Integración web")]
public sealed class FormularioUxTests
{
    [Theory]
    [InlineData("/clientes?saldo=pendiente", "Quitar filtro de deuda")]
    [InlineData("/pedidos?estado=activos", "Quitar filtro de estado")]
    [InlineData("/inventario?estado=vendida", "Estado físico: Vendida")]
    [InlineData("/inventario?estado=disponible", "Estado físico: Disponible")]
    [InlineData("/", "Acciones rápidas")]
    public async Task RutasContextuales_RenderizanFiltroYAccesos(string ruta, string esperado)
    {
        using var factory = new AplicacionAutenticacionFactory();
        using var cliente = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        await IniciarSesionAsync(cliente);
        using var respuesta = await cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.Contains(esperado, html);
        if (ruta == "/")
        {
            Assert.Contains("href=\"/pagos\">Registrar abono", html);
            Assert.Contains("href=\"/ventas/nueva?modo=directa\">Venta directa", html);
            Assert.Contains("href=\"/pedidos/nuevo\">Registrar pedido", html);
            Assert.Contains("href=\"/clientes\">Buscar cliente", html);
            Assert.Contains("Pendiente de entregar", html);
            Assert.DoesNotContain("Pedidos activos", html);
        }
    }

    [Fact]
    public async Task PedidoNuevo_RenderizaSoloTiposManuales()
    {
        using var factory = new AplicacionAutenticacionFactory();
        using var cliente = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        await IniciarSesionAsync(cliente);
        using var respuesta = await cliente.GetAsync("/pedidos/nuevo");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var html = await respuesta.Content.ReadAsStringAsync();
        var selector = Regex.Match(html, "<select[^>]*id=\"pedido-tipo\"[^>]*>(.*?)</select>", RegexOptions.Singleline);
        Assert.True(selector.Success);
        var opciones = Regex.Matches(selector.Groups[1].Value, "<option value=\"([^\"]+)\"")
            .Select(x => x.Groups[1].Value);
        Assert.Equal(new[] { "Importacion", "Catalogo", "Apartado" }, opciones);
    }

    [Theory]
    [InlineData("/clientes/nuevo")]
    [InlineData("/productos/nuevo")]
    [InlineData("/categorias/nueva")]
    public async Task FormularioNuevo_NoMuestraAlertaDeGuardadoNiValidacionesAntesDeEnviar(string ruta)
    {
        using var factory = new AplicacionAutenticacionFactory();
        using var cliente = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        await IniciarSesionAsync(cliente);
        using var respuesta = await cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());

        Assert.Contains("class=\"entity-form\"", html);
        Assert.DoesNotContain("ErrorGuardado", html);
        Assert.DoesNotContain("class=\"mensaje-error\"", html);
        Assert.DoesNotContain("class=\"validation-message\"", html);
        if (ruta == "/productos/nuevo")
        {
            Assert.Contains("No hay categorías disponibles.", html);
            Assert.Contains("Crea una categoría antes de registrar un producto.", html);
            Assert.Contains("href=\"/categorias\">Ir a categorías</a>", html);
            Assert.DoesNotContain("producto-codigo-interno", html);
            Assert.Contains("Código de barras", html);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ErrorRealDelServicio_SeMuestraYSeLimpiaTrasReintentoCorrecto(bool producto)
    {
        await using var test = await TestDatabase.CreateAsync();
        object pagina;
        object modelo;
        Type formulario;
        Action corregirModelo;
        string mensajeEsperado;
        IReadOnlyList<CategoriaDto> categorias = await new CategoriaService(test.Db).ListarAsync();
        var navigation = new NavegacionPrueba();

        if (producto)
        {
            pagina = new ProductoEdicion();
            var modeloProducto = new ProductoFormModel
            {
                Nombre = "",
                CategoriaId = test.Categoria.Id,
                PrecioSugerido = 100m,
            };
            modelo = modeloProducto;
            formulario = typeof(ProductoForm);
            corregirModelo = () => modeloProducto.Nombre = "Producto del reintento";
            mensajeEsperado = "El nombre es obligatorio.";
            Establecer(pagina, "ProductoService", new ProductoService(test.Db));
            Establecer(pagina, "CategoriasDisponibles", categorias);
            Establecer(pagina, "Logger", NullLogger<ProductoEdicion>.Instance);
        }
        else
        {
            pagina = new ClienteEdicion();
            var modeloCliente = new ClienteFormModel { Telefono = "555-0111" };
            modelo = modeloCliente;
            formulario = typeof(ClienteForm);
            corregirModelo = () => modeloCliente.Nombres = "Cliente del reintento";
            mensajeEsperado = "Nombres y teléfono son obligatorios.";
            Establecer(pagina, "ClienteService", new ClienteService(test.Db));
            Establecer(pagina, "Logger", NullLogger<ClienteEdicion>.Instance);
        }
        Establecer(pagina, "Navigation", navigation);
        Assert.Null(Obtener<string?>(pagina, "ErrorGuardado"));

        await GuardarAsync(pagina, modelo);
        Assert.Equal(mensajeEsperado, Obtener<string?>(pagina, "ErrorGuardado"));
        Assert.False(Obtener<bool>(pagina, "Guardando"));
        var htmlError = await RenderizarFormularioAsync(formulario, modelo,
            Obtener<string?>(pagina, "ErrorGuardado"), categorias);
        Assert.Contains($"<div class=\"mensaje-error\" role=\"alert\">{mensajeEsperado}</div>", htmlError);
        Assert.DoesNotContain("ErrorGuardado", htmlError);

        corregirModelo();
        await GuardarAsync(pagina, modelo);
        Assert.Null(Obtener<string?>(pagina, "ErrorGuardado"));
        Assert.False(Obtener<bool>(pagina, "Guardando"));
        Assert.Contains(producto ? "/productos?mensaje=producto-creado" : "/clientes?mensaje=cliente-creado",
            navigation.Uri);
        var htmlCorrecto = await RenderizarFormularioAsync(formulario, modelo,
            Obtener<string?>(pagina, "ErrorGuardado"), categorias);
        Assert.DoesNotContain("class=\"mensaje-error\"", htmlCorrecto);

        if (producto)
        {
            var servicio = new ProductoService(test.Db);
            var creado = Assert.Single(await servicio.BuscarAsync("Producto del reintento"));
            Assert.Matches("^PRO-[A-F0-9]{32}$", creado.CodigoInterno);
            Assert.Equal(creado.Id, Assert.Single(await servicio.BuscarAsync(creado.CodigoInterno)).Id);
        }
    }

    [Fact]
    public void Producto_SeValidaSinCodigoInternoYCodigoBarrasSigueManualOpcional()
    {
        var modelo = new ProductoFormModel { Nombre = "Producto", CategoriaId = 1 };
        Assert.Null(typeof(ProductoFormModel).GetProperty("CodigoInterno"));
        Assert.Null(typeof(ProductoInput).GetProperty("CodigoInterno"));
        var errores = new List<ValidationResult>();
        Assert.True(Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, true));
        Assert.Null(modelo.ToInput().CodigoBarras);
        modelo.CodigoBarras = "7401234567890";
        Assert.Equal("7401234567890", modelo.ToInput().CodigoBarras);
    }

    private static async Task<string> RenderizarFormularioAsync(Type formulario, object modelo,
        string? error, IReadOnlyList<CategoriaDto> categorias)
    {
        await using var servicios = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(servicios, servicios.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parametros = new Dictionary<string, object?> { ["Modelo"] = modelo, ["ErrorMessage"] = error };
            if (formulario == typeof(ProductoForm))
                parametros["Categorias"] = categorias;
            var resultado = await renderer.RenderComponentAsync(formulario, ParameterView.FromDictionary(parametros));
            return WebUtility.HtmlDecode(resultado.ToHtmlString());
        });
    }

    private static async Task IniciarSesionAsync(HttpClient cliente)
    {
        var html = await cliente.GetStringAsync("/login");
        var etiqueta = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>");
        Assert.True(etiqueta.Success);
        var token = WebUtility.HtmlDecode(Regex.Match(etiqueta.Value, "value=\"([^\"]+)\"").Groups[1].Value);
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

    private static Task GuardarAsync(object pagina, object modelo) =>
        (Task)pagina.GetType().GetMethod("GuardarAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(pagina, [modelo])!;

    private sealed class NavegacionPrueba : NavigationManager
    {
        public NavegacionPrueba() => Initialize("https://localhost/", "https://localhost/");
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }
}
