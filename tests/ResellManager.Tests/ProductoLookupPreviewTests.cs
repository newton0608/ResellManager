using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Web.Components.Productos;
using static ResellManager.Tests.ProductoLookupTests;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed class ProductoLookupPreviewTests
{
    private const string ImagenExterna = "https://images.example.com/producto.png";
    private static readonly byte[] ImagenManual = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jS6kAAAAASUVORK5CYII=");
    private static string PreviewManual => $"data:image/png;base64,{Convert.ToBase64String(ImagenManual)}";

    [Fact]
    public async Task AceptarImagenExterna_MuestraPreviewSinGuardar_DeshacerRestauraAusencia()
    {
        var modelo = ModeloManual();
        await using var vista = await Vista.CrearAsync(modelo);
        Assert.Contains("Sin imagen seleccionada", await vista.HtmlAsync());

        await vista.EjecutarAsync("BuscarProductoAsync");
        Assert.Null(modelo.ImagenExternaUrl);
        Assert.Contains("Sin imagen seleccionada", await vista.HtmlAsync());

        await vista.EjecutarAsync("UsarCandidato");
        var html = await vista.HtmlAsync();
        Assert.Contains($"src=\"{ImagenExterna}\"", html);
        Assert.Contains("referrerpolicy=\"no-referrer\"", html);
        Assert.Contains("Imagen externa pendiente.", html);
        Assert.DoesNotContain("Sin imagen seleccionada", html);
        Assert.Equal(ImagenExterna, modelo.ImagenExternaUrl);
        Assert.Null(modelo.ImagenPrincipalRuta);
        Assert.Null(modelo.ImagenArchivo);
        Assert.Null(modelo.ImagenContenido);
        Assert.Equal(0, vista.Guardados);

        await vista.EjecutarAsync("DeshacerImportacion");
        html = await vista.HtmlAsync();
        Assert.Contains("Sin imagen seleccionada", html);
        Assert.DoesNotContain(ImagenExterna, html);
        Assert.Null(modelo.ImagenExternaUrl);
        Assert.Equal(0, vista.Guardados);
    }

    [Theory]
    [InlineData("http://images.example.com/producto.png")]
    [InlineData("https://images.example.com:444/producto.png")]
    [InlineData("https://usuario:clave@images.example.com/producto.png")]
    [InlineData("https://localhost/producto.png")]
    [InlineData("https://10.0.0.1/producto.png")]
    [InlineData("https://127.0.0.1/producto.png")]
    [InlineData("https://[::1]/producto.png")]
    [InlineData("https://servidor.internal/producto.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("/productos/7/imagen")]
    public async Task UrlExternaNoPermitida_NoRenderizaImagenYPermiteQuitar(string url)
    {
        var modelo = ModeloManual();
        modelo.ImagenExternaUrl = url;
        await using var vista = await Vista.CrearAsync(modelo);
        var html = await vista.HtmlAsync();
        Assert.Contains("Sin imagen seleccionada", html);
        Assert.DoesNotContain("<img ", html);
        Assert.Contains("Quitar imagen externa", html);

        await vista.EjecutarAsync("QuitarImagen");
        Assert.Null(modelo.ImagenExternaUrl);
        Assert.DoesNotContain("Imagen externa pendiente.", await vista.HtmlAsync());
        Assert.Equal(0, vista.Guardados);
    }

    [Fact]
    public async Task QuitarImagenExterna_EliminaPreview_DeshacerUltimaImportacionRestauraPreviewAnterior()
    {
        var modelo = ModeloManual();
        modelo.ImagenExternaUrl = "https://images.example.com/anterior.png";
        await using var vista = await Vista.CrearAsync(modelo);
        await vista.ImportarAsync();
        await vista.EjecutarAsync("QuitarImagen");
        var html = await vista.HtmlAsync();
        Assert.Null(modelo.ImagenExternaUrl);
        Assert.DoesNotContain("<img ", html);
        Assert.Contains("Sin imagen seleccionada", html);

        await vista.EjecutarAsync("DeshacerImportacion");
        html = await vista.HtmlAsync();
        Assert.Contains("src=\"https://images.example.com/anterior.png\"", html);
        Assert.Equal("https://images.example.com/anterior.png", modelo.ImagenExternaUrl);
        Assert.DoesNotContain(ImagenExterna, html);
        Assert.Equal(0, vista.Guardados);
    }

    [Fact]
    public async Task ImagenManualAnterior_TienePrioridadYDeshacerRestauraArchivoBytesYPreview()
    {
        var modelo = ModeloManual();
        await using var vista = await Vista.CrearAsync(modelo);
        var archivo = new ArchivoImagen();
        await vista.EjecutarAsync("SeleccionarImagenAsync", new InputFileChangeEventArgs([archivo]));
        await vista.ImportarAsync();
        Assert.Null(modelo.ImagenExternaUrl);
        Assert.Same(archivo, modelo.ImagenArchivo);
        Assert.Equal(ImagenManual, modelo.ImagenContenido);
        Assert.Contains($"src=\"{PreviewManual}\"", await vista.HtmlAsync());

        await vista.EjecutarAsync("QuitarImagen");
        Assert.Contains("Sin imagen seleccionada", await vista.HtmlAsync());
        await vista.EjecutarAsync("DeshacerImportacion");
        Assert.Same(archivo, modelo.ImagenArchivo);
        Assert.Equal(ImagenManual, modelo.ImagenContenido);
        Assert.Contains($"src=\"{PreviewManual}\"", await vista.HtmlAsync());
        Assert.Equal(0, vista.Guardados);
    }

    [Fact]
    public async Task ImagenManualPosterior_ReemplazaPreviewExterna_DeshacerRestauraEstadoAnteriorAImportar()
    {
        var modelo = ModeloManual();
        await using var vista = await Vista.CrearAsync(modelo);
        await vista.ImportarAsync();
        await vista.EjecutarAsync("SeleccionarImagenAsync", new InputFileChangeEventArgs([new ArchivoImagen()]));
        var html = await vista.HtmlAsync();
        Assert.Contains($"src=\"{PreviewManual}\"", html);
        Assert.DoesNotContain(ImagenExterna, html);
        Assert.DoesNotContain("Imagen externa pendiente.", html);
        Assert.Null(modelo.ImagenExternaUrl);

        await vista.EjecutarAsync("DeshacerImportacion");
        Assert.Contains("Sin imagen seleccionada", await vista.HtmlAsync());
        Assert.Null(modelo.ImagenArchivo);
        Assert.Null(modelo.ImagenContenido);
        Assert.Equal(0, vista.Guardados);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImagenManualEnModelo_SuprimePreviewExternaAunqueNoHayaPreviewLocal(bool conArchivo)
    {
        var modelo = ModeloManual();
        modelo.ImagenExternaUrl = ImagenExterna;
        if (conArchivo) modelo.ImagenArchivo = new ArchivoImagen();
        else modelo.ImagenContenido = ImagenManual;
        await using var vista = await Vista.CrearAsync(modelo);
        var html = await vista.HtmlAsync();
        Assert.DoesNotContain(ImagenExterna, html);
        Assert.DoesNotContain("Imagen externa pendiente.", html);
        Assert.Equal(ImagenExterna, modelo.ImagenExternaUrl);
    }

    [Fact]
    public async Task ImagenGuardada_ConservaRutaLocalYEliminar_DeshacerRestauraLaPreviewOriginal()
    {
        var modelo = ModeloManual();
        modelo.ImagenPrincipalRuta = "productos/7/principal.webp";
        await using var vista = await Vista.CrearAsync(modelo);
        Assert.Contains("src=\"/productos/7/imagen\"", await vista.HtmlAsync());
        await vista.ImportarAsync();
        Assert.Contains($"src=\"{ImagenExterna}\"", await vista.HtmlAsync());

        await vista.EjecutarAsync("QuitarImagen");
        Assert.True(modelo.EliminarImagenPrincipal);
        Assert.DoesNotContain("<img ", await vista.HtmlAsync());
        await vista.EjecutarAsync("DeshacerImportacion");
        Assert.False(modelo.EliminarImagenPrincipal);
        Assert.Null(modelo.ImagenExternaUrl);
        Assert.Contains("src=\"/productos/7/imagen\"", await vista.HtmlAsync());
        Assert.Equal(0, vista.Guardados);
    }

    private sealed class Vista : IAsyncDisposable
    {
        private readonly ServiceProvider servicios;
        private readonly HtmlRenderer renderer;
        private readonly Activador activador;
        private HtmlRootComponent raiz;
        public int Guardados { get; private set; }

        private Vista()
        {
            activador = new Activador();
            var proveedor = new ProveedorFalso("Prueba", new ProductoLookupRespuesta(
                EstadoLookupProveedor.Encontrado, Candidato() with { ImagenUrl = ImagenExterna }));
            servicios = new ServiceCollection().AddLogging().AddSingleton<IJSRuntime, JSInerte>()
                .AddSingleton<IProductoLookupService>(Servicio(proveedor))
                .AddSingleton<IComponentActivator>(activador).BuildServiceProvider();
            renderer = new HtmlRenderer(servicios, servicios.GetRequiredService<ILoggerFactory>());
        }

        public static async Task<Vista> CrearAsync(ProductoFormModel modelo)
        {
            var vista = new Vista();
            vista.raiz = await vista.renderer.Dispatcher.InvokeAsync(() =>
                vista.renderer.RenderComponentAsync<ProductoForm>(ParameterView.FromDictionary(new Dictionary<string, object?>()
                {
                    ["Modelo"] = modelo, ["Categorias"] = new CategoriaDto[] { new(1, "General", null) },
                    ["LookupHabilitado"] = true, ["ProductoId"] = 7,
                    ["OnGuardar"] = EventCallback.Factory.Create<ProductoFormModel>(vista, _ => vista.Guardados++)
                })));
            return vista;
        }

        public Task<string> HtmlAsync() => renderer.Dispatcher.InvokeAsync(() =>
            WebUtility.HtmlDecode(raiz.ToHtmlString()));

        public Task EjecutarAsync(string metodo, params object[] args) => renderer.Dispatcher.InvokeAsync(async () =>
        {
            if (Call(activador.Formulario, metodo, args) is Task tarea) await tarea;
            Call(activador.Formulario, "StateHasChanged");
        });

        public async Task ImportarAsync()
        {
            await EjecutarAsync("BuscarProductoAsync");
            await EjecutarAsync("UsarCandidato");
        }

        public async ValueTask DisposeAsync()
        {
            await renderer.DisposeAsync();
            await servicios.DisposeAsync();
        }
    }

    private sealed class Activador : IComponentActivator
    {
        public ProductoForm Formulario { get; private set; } = null!;
        public IComponent CreateInstance(Type tipo)
        {
            var componente = (IComponent)Activator.CreateInstance(tipo)!;
            if (componente is ProductoForm formulario) Formulario = formulario;
            return componente;
        }
    }

    private sealed class ArchivoImagen : IBrowserFile
    {
        public string Name => "manual.png";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => ImagenManual.Length;
        public string ContentType => "image/png";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new MemoryStream(ImagenManual, writable: false);
    }

    private sealed class JSInerte : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) =>
            throw new NotSupportedException("El renderizado estático no usa JavaScript.");
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new NotSupportedException("El renderizado estático no usa JavaScript.");
    }
}
