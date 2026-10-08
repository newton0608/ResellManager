using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using ResellManager.Application.DTOs;
using ResellManager.Web.Catalogo;
using ResellManager.Web.Components.Catalogo;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

[Collection("Integración web")]
public sealed class CatalogoUiTests
{
    private static readonly ProductoCatalogoDto Perfume = new(7, "Perfume floral", 2, "Perfumes", 249m, true, true);
    private static readonly ProductoCatalogoDto Camisa = new(8, "Camisa de lino", 1, "Ropa", 175m, false, true);
    private static readonly ProductoCatalogoDetalleDto Detalle = new(7, "Perfume floral", "Una fragancia fresca.",
        "Marca floral", null, null, null, 100m, null, "Frasco", 2, "Perfumes", 249m, true, true);

    [Fact]
    public async Task Listado_RenderizaTarjetasPrecioPublicoCategoriasYEnlacesSinDatosAdministrativos()
    {
        var api = new ApiPrueba([Perfume, Camisa]);
        using var modelo = Modelo(api);
        await modelo.CargarAsync();
        var html = await RenderVistaAsync(modelo);

        foreach (var texto in new[] { Perfume.Nombre, Camisa.Nombre, "Q 249.00", "Q 175.00", "Perfumes", "Ropa",
            "href=\"/catalogo/7\"", "src=\"/api/catalogo/productos/7/imagen\"", "Ver producto" }) Assert.Contains(texto, html);
        Assert.DoesNotContain("<table", html);
        Assert.DoesNotContain("src=\"/api/catalogo/productos/8/imagen\"", html);
        foreach (var privado in new[] { "Código interno", "Código de barras", "Costo", "Proveedor", "src=\"/productos/7/imagen\"", "Editar producto" })
            Assert.DoesNotContain(privado, html);
        Assert.Equal(new[] { 2, 1 }, modelo.Categorias.Select(c => c.Id));
    }

    [Fact]
    public async Task Busqueda_AplicaDebounceYEnviaSoloElUltimoTerminoSinFiltrarLocalmente()
    {
        var api = new ApiPrueba([Perfume, Camisa]);
        using var modelo = Modelo(api);
        await modelo.CargarAsync();
        api.Consultas.Clear();
        // La respuesta del backend es la fuente de verdad, aunque no coincida con el texto por nombre.
        api.Buscar = (_, _, _) => Task.FromResult<IReadOnlyList<ProductoCatalogoDto>>([Camisa]);
        var primera = modelo.BuscarAsync("p");
        var ultima = modelo.BuscarAsync("  perfume  ");
        await Task.WhenAll(primera, ultima);

        Assert.Equal(("perfume", (int?)null), Assert.Single(api.Consultas));
        Assert.Equal(Camisa, Assert.Single(modelo.Productos));
        Assert.Equal(2, modelo.Categorias.Count);
        Assert.False(modelo.Cargando);
    }

    [Fact]
    public async Task Categoria_SeEnviaJuntoConTerminoYConservaOpcionesHastaLimpiar()
    {
        var api = new ApiPrueba([Perfume, Camisa]);
        using var modelo = Modelo(api);
        await modelo.CargarAsync();
        await modelo.BuscarAsync("floral");
        api.Consultas.Clear();
        api.Buscar = (_, _, _) => Task.FromResult<IReadOnlyList<ProductoCatalogoDto>>([]);
        await modelo.FiltrarCategoriaAsync(2);

        Assert.Equal(("floral", (int?)2), Assert.Single(api.Consultas));
        Assert.Equal(2, modelo.Categorias.Count);
        var html = await RenderVistaAsync(modelo);
        Assert.Contains("No encontramos productos con estos filtros", html);
        Assert.Contains("value=\"2\" selected", html);
        Assert.Contains("Ver todos los productos", html);

        api.Buscar = (_, _, _) => Task.FromResult<IReadOnlyList<ProductoCatalogoDto>>([Camisa]);
        await modelo.LimpiarAsync();
        Assert.Null(modelo.CategoriaId);
        Assert.Equal(string.Empty, modelo.Termino);
        Assert.Equal(Camisa, Assert.Single(modelo.Productos));
        Assert.Single(modelo.Categorias);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(999)]
    public async Task CategoriaInicial_ConservaSeleccionYConsultaBackendConOpcionesPublicas(int categoriaId)
    {
        var api = new ApiPrueba([Perfume, Camisa]);
        using var modelo = Modelo(api);
        modelo.EstablecerFiltros(null, categoriaId);
        await modelo.CargarAsync();
        Assert.Equal(new (string?, int?)[] { (null, null), (string.Empty, categoriaId) }, api.Consultas);
        var html = await RenderVistaAsync(modelo);
        Assert.Contains($"value=\"{categoriaId}\" selected", html);
        Assert.Equal(2, modelo.Categorias.Count);
        if (categoriaId == 999) Assert.Contains("Categoría no disponible", html);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RespuestaAntigua_NoSobrescribeResultadosNiErrorActual(bool fallaAntigua)
    {
        var api = new ApiPrueba([Perfume]);
        using var modelo = Modelo(api);
        await modelo.CargarAsync();
        var atrasada = new TaskCompletionSource<IReadOnlyList<ProductoCatalogoDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Buscar = (termino, _, _) => termino == "vieja" ? atrasada.Task : Task.FromResult<IReadOnlyList<ProductoCatalogoDto>>([Camisa]);
        modelo.EstablecerFiltros("vieja", null);
        var primera = modelo.CargarAsync();
        modelo.EstablecerFiltros("actual", null);
        await modelo.CargarAsync();
        if (fallaAntigua) atrasada.SetException(new JSException("Respuesta antigua fallida"));
        else atrasada.SetResult([Perfume]);
        await primera;

        Assert.Equal(Camisa, Assert.Single(modelo.Productos));
        Assert.Null(modelo.Error);
        Assert.False(modelo.Cargando);
    }

    [Fact]
    public async Task CatalogoVacio_MuestraMensajePublicoSinAccionesAdministrativas()
    {
        using var modelo = Modelo(new ApiPrueba([]));
        await modelo.CargarAsync();
        var html = await RenderVistaAsync(modelo);
        Assert.Contains("Por ahora no hay productos disponibles", html);
        Assert.DoesNotContain("Registrar", html);
        Assert.DoesNotContain("<article", html);
    }

    [Fact]
    public async Task CargaInicial_MuestraEstadoAccesibleAntesDeConsultarApi()
    {
        var api = new ApiPrueba([]);
        using var modelo = Modelo(api);
        var html = await RenderVistaAsync(modelo);
        Assert.Contains("Cargando productos…", html);
        Assert.Contains("aria-busy=\"true\"", html);
        Assert.Contains("role=\"status\"", html);
        Assert.Empty(api.Consultas);
    }

    [Fact]
    public async Task ErrorDeRed_MuestraReintentoSinDetallesTecnicosYSeRecupera()
    {
        var api = new ApiPrueba([]) { Buscar = (_, _, _) => throw new JSException("ERROR-PRIVADO /ruta/fisica.db") };
        using var modelo = Modelo(api);
        await modelo.CargarAsync();
        var html = await RenderVistaAsync(modelo);
        Assert.Contains("role=\"alert\"", html);
        Assert.Contains("Reintentar", html);
        Assert.DoesNotContain("ERROR-PRIVADO", html);
        Assert.DoesNotContain("/ruta/fisica.db", html);
        api.Buscar = (_, _, _) => Task.FromResult<IReadOnlyList<ProductoCatalogoDto>>([Perfume]);
        await modelo.CargarAsync();
        Assert.Null(modelo.Error);
        Assert.Equal(Perfume, Assert.Single(modelo.Productos));
    }

    [Fact]
    public async Task ProductoSinImagen_RenderizaPlaceholderSinSolicitarEndpoint()
    {
        var html = await RenderAsync<ImagenCatalogo>(new() { ["ProductoId"] = 8, ["Nombre"] = Camisa.Nombre, ["TieneImagen"] = false });
        Assert.Contains("Imagen no disponible", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("/productos/", html);
    }

    [Fact]
    public async Task Detalle_ComercialMuestraNombrePrecioDescripcionYMedidasSinCamposVacios()
    {
        var html = await RenderAsync<CatalogoDetalleVista>(new() { ["Producto"] = Detalle });
        foreach (var texto in new[] { Detalle.Nombre, "Q 249.00", Detalle.Descripcion!, Detalle.Marca!, "100 ml", "Frasco", "Disponible" })
            Assert.Contains(texto, html);
        Assert.DoesNotContain(">Modelo<", html);
        Assert.DoesNotContain("Sin información", html);
        Assert.DoesNotContain("Costo", html);
        Assert.DoesNotContain("Precio sugerido", html);
    }

    [Fact]
    public async Task Detalle404_MuestraMensajeAmigableYRegresoAlCatalogo()
    {
        var pagina = new CatalogoDetalle();
        Set(pagina, "ProductoId", 999);
        Set(pagina, "Cliente", new ApiPrueba([]));
        Set(pagina, "Logger", NullLogger<CatalogoDetalle>.Instance);
        await CallAsync(pagina, "CargarAsync");
        var html = await RenderAsync<CatalogoDetalleVista>(new()
        {
            ["Producto"] = Get<ProductoCatalogoDetalleDto?>(pagina, "Producto"),
            ["Cargando"] = Get<bool>(pagina, "Cargando"), ["NoDisponible"] = Get<bool>(pagina, "NoDisponible")
        });
        Assert.Contains("Este producto ya no está disponible", html);
        Assert.Contains("href=\"/catalogo\"", html);
        Assert.DoesNotContain("Reintentar", html);
    }

    [Fact]
    public async Task Marca_JerarquiaOpcionesPublicasEstablesYLimpiarTresFiltros()
    {
        var api = new ApiPrueba([
            Perfume with { Marca = " Acmé ", CategoriaPadreId = 3, CategoriaPadreNombre = "Belleza" },
            Camisa with { Marca = "acmé" }]);
        using var modelo = Modelo(api);
        await modelo.CargarAsync();
        Assert.Single(modelo.Marcas);
        Assert.Contains(modelo.Categorias, c => c.Id == 3 && c.PadreId == null);
        Assert.Contains(modelo.Categorias, c => c.Id == 2 && c.PadreId == 3);
        modelo.EstablecerFiltros("floral", 3, "Acmé");
        await modelo.CargarAsync();
        var html = await RenderVistaAsync(modelo);
        Assert.Contains("catalogo-marca", html);
        Assert.Contains("Belleza", html);
        Assert.Single(modelo.Marcas);
        Assert.True(modelo.TieneFiltros);
        await modelo.LimpiarAsync();
        Assert.Null(modelo.Marca);
        Assert.Null(modelo.CategoriaId);
        Assert.Equal(string.Empty, modelo.Termino);
    }

    [Fact]
    public async Task Galeria_RenderizaIdsOpacosMiniaturasDiferidasYVisorAccesible()
    {
        var fotos = new[] { new ImagenCatalogoDto("opaque1", 0, true), new ImagenCatalogoDto("opaque2", 1, false) };
        var html = await RenderAsync<CatalogoDetalleVista>(new() { ["Producto"] = Detalle with { Imagenes = fotos }, ["EnlaceWhatsApp"] = "https://wa.me/50255550123?text=hola" });
        Assert.Contains("/api/catalogo/productos/7/imagenes/opaque1", html);
        Assert.Contains("loading=\"lazy\"", html);
        Assert.Contains("data-gallery-dialog", html);
        Assert.Contains("Cerrar visor", html);
        Assert.Contains("Restablecer zoom", html);
        Assert.Contains("Consultar por WhatsApp", html);
        Assert.DoesNotContain("Ruta", html);
        var oculto = await RenderAsync<CatalogoDetalleVista>(new() { ["Producto"] = Detalle });
        Assert.DoesNotContain("Consultar por WhatsApp", oculto);
    }

    [Theory]
    [InlineData(0, "Q 0.00")]
    [InlineData(249, "Q 249.00")]
    [InlineData(2499.5, "Q 2,499.50")]
    public void PrecioPublico_UsaQuetzalesDosDecimalesYSinPromociones(decimal precio, string esperado) =>
        Assert.Equal(esperado, CatalogoPresentacion.Precio(precio));

    [Theory]
    [InlineData("/catalogo")]
    [InlineData("/catalogo?termino=perfume&categoriaId=2")]
    [InlineData("/catalogo/999")]
    public async Task RutasPublicas_AccedenSinAutenticacionYUsanLayoutDeCatalogo(string ruta)
    {
        using var factory = new AplicacionAutenticacionFactory();
        using var cliente = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost")
        });
        using var respuesta = await cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Null(respuesta.Headers.Location);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.Contains("Navegación del catálogo", html);
        Assert.DoesNotContain("app-sidebar", html);
        Assert.DoesNotContain("Acceso privado", html);
        Assert.DoesNotContain("href=\"/compras\"", html);
        if (ruta.Contains("termino=")) Assert.Contains("value=\"perfume\"", html);
    }

    [Fact]
    public async Task ClientePublico_ReutilizaModuloYDelegaLecturasDeDtoY404()
    {
        var js = new JsPrueba([Perfume]);
        await using var cliente = new CatalogoPublicoClient(js);
        Assert.Equal(Perfume, Assert.Single(await cliente.ListarAsync("azul & floral", 2)));
        Assert.Null(await cliente.ObtenerDetalleAsync(999));
        Assert.Equal(1, js.Importaciones);
        Assert.Equal("listar", js.Llamadas[0].Metodo);
        Assert.Equal("azul & floral", js.Llamadas[0].Argumentos![0]);
        Assert.Equal(2, js.Llamadas[0].Argumentos![1]);
        Assert.Equal("detalle", js.Llamadas[1].Metodo);
        Assert.Equal(999, js.Llamadas[1].Argumentos![0]);
    }

    private static Task<string> RenderVistaAsync(CatalogoListadoModelo modelo) => RenderAsync<CatalogoVista>(new()
    {
        ["Modelo"] = modelo,
        ["OnLimpiar"] = EventCallback.Factory.Create(modelo, modelo.LimpiarAsync),
        ["OnReintentar"] = EventCallback.Factory.Create(modelo, modelo.CargarAsync)
    });

    private static CatalogoListadoModelo Modelo(ApiPrueba api) => new(api, NullLogger.Instance);
    private static async Task<string> RenderAsync<T>(Dictionary<string, object?> parametros) where T : IComponent
    {
        await using var servicios = new ServiceCollection().AddLogging().AddSingleton<IJSRuntime>(new JsPrueba([])).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(servicios, servicios.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
            WebUtility.HtmlDecode((await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parametros))).ToHtmlString()));
    }

    private sealed class ApiPrueba(IReadOnlyList<ProductoCatalogoDto> productos) : ICatalogoPublicoClient
    {
        public List<(string? Termino, int? Categoria)> Consultas { get; } = [];
        public Func<string?, int?, CancellationToken, Task<IReadOnlyList<ProductoCatalogoDto>>> Buscar { get; set; } =
            (_, _, _) => Task.FromResult(productos);
        public Task<IReadOnlyList<ProductoCatalogoDto>> ListarAsync(string? termino = null, int? categoriaId = null, CancellationToken ct = default, string? marca = null)
        { Consultas.Add((termino, categoriaId)); return Buscar(termino, categoriaId, ct); }
        public Task<ProductoCatalogoDetalleDto?> ObtenerDetalleAsync(int productoId, CancellationToken ct = default) =>
            Task.FromResult<ProductoCatalogoDetalleDto?>(null);
    }

    private sealed class JsPrueba(ProductoCatalogoDto[] productos) : IJSRuntime, IJSObjectReference
    {
        public int Importaciones;
        public List<(string Metodo, object?[]? Argumentos)> Llamadas { get; } = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args)
        {
            if (identifier == "import") { Importaciones++; return ValueTask.FromResult((TValue)(object)this); }
            Llamadas.Add((identifier, args));
            return ValueTask.FromResult(identifier == "listar" ? (TValue)(object)productos : default!);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
