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
            Assert.Contains("Código de producto", html);
            Assert.Contains("Referencia o SKU para buscar este producto", html);
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
                CodigoInterno = test.Producto.CodigoInterno,
                Nombre = "Producto del reintento",
                CategoriaId = test.Categoria.Id,
                PrecioSugerido = 100m,
            };
            modelo = modeloProducto;
            formulario = typeof(ProductoForm);
            corregirModelo = () => modeloProducto.CodigoInterno = "SKU-MANUAL-REINTENTO";
            mensajeEsperado = "El código interno ya está registrado.";
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
            var encontrados = await new ProductoService(test.Db).BuscarAsync("SKU-MANUAL-REINTENTO");
            Assert.Equal("SKU-MANUAL-REINTENTO", Assert.Single(encontrados).CodigoInterno);
        }
    }

    [Fact]
    public void CodigoDeProducto_ConservaCapturaManualYObligatoriedadConMensajeAmable()
    {
        var modelo = new ProductoFormModel { Nombre = "Producto", CategoriaId = 1 };
        Assert.Empty(modelo.CodigoInterno);
        var errores = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, true));
        Assert.Contains(errores, error => error.MemberNames.Contains(nameof(ProductoFormModel.CodigoInterno))
            && error.ErrorMessage == "El código de producto es obligatorio.");

        modelo.CodigoInterno = "SKU-ELEGIDO-POR-USUARIA";
        errores.Clear();
        Assert.True(Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, true));
        Assert.Equal("SKU-ELEGIDO-POR-USUARIA", modelo.ToInput().CodigoInterno);
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
