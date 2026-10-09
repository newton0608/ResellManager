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

    [Fact]
    public async Task GaleriaManual_AgregaOchoSinGuardarYRechazaNovenaSinPerderSeleccion()
    {
        var modelo = ModeloManual();
        await using var vista = await Vista.CrearAsync(modelo);
        await vista.EjecutarAsync("SeleccionarImagenAsync", new InputFileChangeEventArgs(
            Enumerable.Range(0, 8).Select(_ => (IBrowserFile)new ArchivoImagen()).ToArray()));
        Assert.Equal(8, modelo.Galeria.Count);
        Assert.True(modelo.Galeria[0].EsPortada);
        Assert.Single(modelo.Galeria.Where(x => x.EsPortada));
        var anteriores = modelo.Galeria.ToArray();
        await vista.EjecutarAsync("SeleccionarImagenAsync", new InputFileChangeEventArgs([new ArchivoImagen()]));
        Assert.Equal(anteriores, modelo.Galeria);
        Assert.Contains("No se añadió ninguna de esta selección", await vista.HtmlAsync());
        Assert.Equal(0, vista.Guardados);
    }

    [Fact]
    public async Task GaleriaManual_SeleccionInvalidaEsAtomicaYConservaImagenExternaPendiente()
    {
        var modelo = ModeloManual();
        modelo.ImagenExternaUrl = ImagenExterna;
        await using var vista = await Vista.CrearAsync(modelo);
        await vista.EjecutarAsync("SeleccionarImagenAsync", new InputFileChangeEventArgs(
            [new ArchivoImagen(), new ArchivoImagen([1, 2, 3])]));
        Assert.Empty(modelo.Galeria);
        Assert.Equal(ImagenExterna, modelo.ImagenExternaUrl);
        Assert.Null(modelo.ImagenContenido);
        Assert.Contains("No se añadió ninguna de esta selección", await vista.HtmlAsync());
    }

    [Fact]
    public async Task GaleriaManual_ReordenaCambiaPortadaYEliminarSeleccionaPortadaRestante()
    {
        var modelo = ModeloManual();
        await using var vista = await Vista.CrearAsync(modelo);
        await vista.EjecutarAsync("SeleccionarImagenAsync", new InputFileChangeEventArgs(
            [new ArchivoImagen(), new ArchivoImagen()]));
        var primera = modelo.Galeria[0];
        var segunda = modelo.Galeria[1];
        await vista.EjecutarAsync("ElegirPortada", segunda);
        await vista.EjecutarAsync("MoverFoto", 1, -1);
        Assert.Same(segunda, modelo.Galeria[0]);
        Assert.True(segunda.EsPortada);
        Assert.False(primera.EsPortada);
        Assert.Equal(0, modelo.ToGaleriaEdicion().PortadaIndice);
        await vista.EjecutarAsync("QuitarFoto", segunda);
        Assert.True(Assert.Single(modelo.Galeria).EsPortada);
        await vista.EjecutarAsync("QuitarFoto", primera);
        Assert.Empty(modelo.Galeria);
        Assert.Null(modelo.ImagenContenido);
        Assert.Contains("Sin imagen seleccionada", await vista.HtmlAsync());
    }

    [Fact]
    public async Task GaleriaManual_LookupYDeshacerRestauranOrdenPortadaYFotografias()
    {
        var modelo = ModeloManual();
        await using var vista = await Vista.CrearAsync(modelo);
        await vista.EjecutarAsync("SeleccionarImagenAsync", new InputFileChangeEventArgs(
            [new ArchivoImagen(), new ArchivoImagen()]));
        await vista.EjecutarAsync("ElegirPortada", modelo.Galeria[1]);
        await vista.ImportarAsync();
        Assert.Null(modelo.ImagenExternaUrl);
        await vista.EjecutarAsync("QuitarImagen");
        await vista.EjecutarAsync("DeshacerImportacion");
        Assert.Equal(2, modelo.Galeria.Count);
        Assert.False(modelo.Galeria[0].EsPortada);
        Assert.True(modelo.Galeria[1].EsPortada);
        Assert.Contains("Fotografía 2 del producto", await vista.HtmlAsync());
        Assert.Equal(0, vista.Guardados);
    }

    [Fact]
    public async Task GaleriaManual_CincoFotosMuestranCadaTransferenciaRealYBloqueanMutaciones()
    {
        var modelo = ModeloManual();
        await using var vista = await Vista.CrearAsync(modelo);
        var archivos = Enumerable.Range(0, 5).Select(_ => new ArchivoControlado()).ToArray();
        var lectura = vista.EjecutarAsync("SeleccionarImagenAsync", new InputFileChangeEventArgs(archivos));
        for (var indice = 0; indice < archivos.Length; indice++)
        {
            await archivos[indice].Inicio.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var html = await vista.HtmlAsync();
            Assert.Contains($"Subiendo foto {indice + 1} de 5", html);
            Assert.Contains("role=\"status\" aria-live=\"polite\" aria-atomic=\"true\"", html);
            Assert.DoesNotContain("Guardando foto", html);
            Assert.Empty(modelo.Galeria); // El lote solo se incorpora al terminar las cinco lecturas.
            await vista.EjecutarAsync("QuitarImagen");
            await vista.EjecutarAsync("GuardarAsync");
            Assert.Equal(0, vista.Guardados);
            archivos[indice].Continuar.TrySetResult();
        }
        await lectura;
        Assert.Equal(5, modelo.Galeria.Count);
        Assert.Single(modelo.Galeria.Where(x => x.EsPortada));
        Assert.Contains("5 de 8 fotografías preparadas", await vista.HtmlAsync());
        Assert.DoesNotContain("Subiendo foto", await vista.HtmlAsync());
        await vista.EjecutarAsync("GuardarAsync");
        Assert.Equal(1, vista.Guardados);
    }

    [Fact]
    public async Task GaleriaManual_FalloDeTransferenciaConservaSeleccionOrdenPortadaYReintentaSinDuplicar()
    {
        var modelo = ModeloManual();
        await using var vista = await Vista.CrearAsync(modelo);
        await vista.EjecutarAsync("SeleccionarImagenAsync", new InputFileChangeEventArgs(
            [new ArchivoImagen(), new ArchivoImagen()]));
        await vista.EjecutarAsync("ElegirPortada", modelo.Galeria[1]);
        await vista.EjecutarAsync("MoverFoto", 1, -1);
        var anteriores = modelo.Galeria.ToArray();
        var fallida = new ArchivoReintentable();
        await vista.EjecutarAsync("SeleccionarImagenAsync", new InputFileChangeEventArgs([new ArchivoImagen(), fallida]));
        Assert.Equal(anteriores, modelo.Galeria);
        Assert.True(anteriores[0].EsPortada);
        Assert.Contains("Reintentar preparación", await vista.HtmlAsync());
        await vista.EjecutarAsync("GuardarAsync");
        Assert.Equal(0, vista.Guardados);
        await vista.EjecutarAsync("ReintentarImagenesAsync");
        Assert.Equal(4, modelo.Galeria.Count);
        Assert.Equal(anteriores, modelo.Galeria.Take(2));
        Assert.Same(anteriores[0], modelo.Galeria.Single(x => x.EsPortada));
        Assert.Equal(2, fallida.Lecturas);
        Assert.DoesNotContain("Reintentar preparación", await vista.HtmlAsync());
        await vista.EjecutarAsync("ReintentarImagenesAsync");
        Assert.Equal(4, modelo.Galeria.Count);
    }

    [Fact]
    public async Task GaleriaManual_DeshacerLookupTrasTransferenciaFallidaDescartaReintentoInvalidoYPermiteGuardar()
    {
        var modelo = ModeloManual();
        await using var vista = await Vista.CrearAsync(modelo);
        await vista.EjecutarAsync("SeleccionarImagenAsync", new InputFileChangeEventArgs(
            [new ArchivoImagen(), new ArchivoImagen()]));
        await vista.EjecutarAsync("ElegirPortada", modelo.Galeria[1]);
        await vista.EjecutarAsync("MoverFoto", 1, -1);
        var anteriores = modelo.Galeria.ToArray();
        await vista.ImportarAsync();
        var fallida = new ArchivoReintentable();
        await vista.EjecutarAsync("SeleccionarImagenAsync", new InputFileChangeEventArgs([new ArchivoImagen(), fallida]));
        Assert.Equal(anteriores, modelo.Galeria);
        Assert.Contains("Reintentar preparación", await vista.HtmlAsync());
        await vista.EjecutarAsync("GuardarAsync");
        Assert.Equal(0, vista.Guardados);

        await vista.EjecutarAsync("DeshacerImportacion");
        var html = await vista.HtmlAsync();
        Assert.Equal("Manual", modelo.Nombre);
        Assert.Equal(anteriores.Select(x => x.Archivo), modelo.Galeria.Select(x => x.Archivo));
        Assert.True(modelo.Galeria[0].EsPortada);
        Assert.False(modelo.Galeria[1].EsPortada);
        Assert.DoesNotContain("Reintentar preparación", html);
        Assert.DoesNotContain("No fue posible transferir", html);
        // Deshacer recrea InputFile: no debe intentar leer las referencias del lote anterior.
        await vista.EjecutarAsync("ReintentarImagenesAsync");
        Assert.Equal(1, fallida.Lecturas);
        Assert.Equal(2, modelo.Galeria.Count);
        await vista.EjecutarAsync("GuardarAsync");
        Assert.Equal(1, vista.Guardados);

        await vista.EjecutarAsync("SeleccionarImagenAsync", new InputFileChangeEventArgs([new ArchivoImagen()]));
        Assert.Equal(3, modelo.Galeria.Count);
        Assert.True(modelo.Galeria[0].EsPortada);
        await vista.EjecutarAsync("GuardarAsync");
        Assert.Equal(2, vista.Guardados);
    }

    [Fact]
    public async Task GaleriaManual_ArchivoMayorA8MbRechazaLoteAntesDeLeerYConservaFotosPrevias()
    {
        var modelo = ModeloManual();
        await using var vista = await Vista.CrearAsync(modelo);
        await vista.EjecutarAsync("SeleccionarImagenAsync", new InputFileChangeEventArgs([new ArchivoImagen()]));
        var anterior = Assert.Single(modelo.Galeria);
        var grande = new ArchivoImagen(tamano: 8 * 1024 * 1024 + 1);
        await vista.EjecutarAsync("SeleccionarImagenAsync", new InputFileChangeEventArgs([grande]));
        Assert.Equal(0, grande.Lecturas);
        Assert.Same(anterior, Assert.Single(modelo.Galeria));
        Assert.True(anterior.EsPortada);
        Assert.Contains("8 MB", await vista.HtmlAsync());
        Assert.DoesNotContain("Reintentar preparación", await vista.HtmlAsync());
        Assert.Equal(0, vista.Guardados);
    }

    [Fact]
    public async Task GaleriaManual_GuardadoAtomicoMuestraSpinnerYTextoSinContadorSimulado()
    {
        var modelo = ModeloManual();
        await using var vista = await Vista.CrearAsync(modelo, guardando: true);
        var html = await vista.HtmlAsync();
        Assert.Contains("Guardando fotografías…", html);
        Assert.Contains("motion-safe:animate-spin", html);
        Assert.DoesNotContain("Guardando foto 1", html);
        Assert.Equal(0, vista.Guardados);
    }

    [Fact]
    public async Task CategoriaDependiente_EditarHijaPreseleccionaRaizYLimpiaHijaAlCambiar()
    {
        var modelo = ModeloManual();
        modelo.CategoriaId = 3;
        var categorias = new CategoriaDto[] {
            new(1, "Raíz A", null), new(2, "Raíz B", null),
            new(3, "Hija A", null, 1, "Raíz A"), new(4, "Hija B", null, 2, "Raíz B") };
        await using var vista = await Vista.CrearAsync(modelo, categorias);
        Assert.Equal(1, modelo.CategoriaPrincipalId);
        Assert.Equal(3, modelo.SubcategoriaId);
        var html = await vista.HtmlAsync();
        Assert.Contains("Categoría principal", html);
        Assert.Contains("Subcategoría (opcional)", html);
        Assert.DoesNotContain(">Hija B</option>", html);
        modelo.CategoriaPrincipalId = 2;
        modelo.SeleccionarCategoriaPrincipal();
        await vista.EjecutarAsync("StateHasChanged");
        Assert.Null(modelo.SubcategoriaId);
        Assert.Equal(2, modelo.CategoriaId);
        Assert.Equal(2, modelo.ToInput().CategoriaPrincipalId);
        html = await vista.HtmlAsync();
        Assert.DoesNotContain(">Hija A</option>", html);
        Assert.Contains(">Hija B</option>", html);
        modelo.SubcategoriaId = 4;
        modelo.SeleccionarSubcategoria();
        Assert.Equal(4, modelo.ToInput().CategoriaId);
    }

    private sealed class ArchivoControlado : IBrowserFile
    {
        public TaskCompletionSource Inicio { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continuar { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Name => "prueba.png";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => ImagenManual.Length;
        public string ContentType => "image/png";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new TransferenciaControlada(this);

        private sealed class TransferenciaControlada(ArchivoControlado archivo) : MemoryStream(ImagenManual, false)
        {
            public override async Task CopyToAsync(Stream destino, int bufferSize, CancellationToken ct)
            {
                archivo.Inicio.TrySetResult();
                await archivo.Continuar.Task.WaitAsync(ct);
                await base.CopyToAsync(destino, bufferSize, ct);
            }
        }
    }

    private sealed class ArchivoReintentable : IBrowserFile
    {
        public int Lecturas { get; private set; }
        public string Name => "reintento.png";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => ImagenManual.Length;
        public string ContentType => "image/png";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            ++Lecturas == 1 ? new TransferenciaFallida() : new MemoryStream(ImagenManual, false);

        private sealed class TransferenciaFallida : MemoryStream
        {
            public override Task CopyToAsync(Stream destino, int bufferSize, CancellationToken ct) =>
                Task.FromException(new IOException("Interrupción de transferencia de prueba"));
        }
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

        public static async Task<Vista> CrearAsync(ProductoFormModel modelo, IReadOnlyList<CategoriaDto>? categorias = null, bool guardando = false)
        {
            var vista = new Vista();
            vista.raiz = await vista.renderer.Dispatcher.InvokeAsync(() =>
                vista.renderer.RenderComponentAsync<ProductoForm>(ParameterView.FromDictionary(new Dictionary<string, object?>()
                {
                    ["Modelo"] = modelo, ["Categorias"] = categorias ?? new CategoriaDto[] { new(1, "General", null) },
                    ["Guardando"] = guardando,
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
        private readonly byte[] contenido;
        private readonly long? tamano;
        public ArchivoImagen(byte[]? contenido = null, long? tamano = null)
        {
            this.contenido = contenido ?? ImagenManual;
            this.tamano = tamano;
        }
        public int Lecturas { get; private set; }
        public string Name => "manual.png";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => tamano ?? contenido.Length;
        public string ContentType => "image/png";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            Lecturas++;
            return new MemoryStream(contenido, writable: false);
        }
    }

    private sealed class JSInerte : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) =>
            throw new NotSupportedException("El renderizado estático no usa JavaScript.");
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new NotSupportedException("El renderizado estático no usa JavaScript.");
    }
}
