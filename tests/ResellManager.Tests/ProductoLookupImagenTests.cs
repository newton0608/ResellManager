using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Infrastructure.Lookup;
using ResellManager.Infrastructure.Services;
using ResellManager.Infrastructure.Storage;
using ResellManager.Web.Components.Pages;
using ResellManager.Web.Components.Productos;
using SkiaSharp;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed class ProductoLookupImagenTests
{
    [Theory]
    [InlineData("http://images.example.com/p.png")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://localhost/p.png")]
    [InlineData("https://127.0.0.1/p.png")]
    [InlineData("https://169.254.169.254/p.png")]
    [InlineData("https://10.0.0.1/p.png")]
    [InlineData("https://[::1]/p.png")]
    [InlineData("https://[::ffff:192.168.1.1]/p.png")]
    [InlineData("https://printer.local/p.png")]
    [InlineData("https://user:pass@images.example.com/p.png")]
    [InlineData("https://images.example.com:444/p.png")]
    public async Task ImagenExterna_UrlInseguraNuncaHaceHttp(string url)
    {
        using var handler = new ProductoLookupProvidersTests.Handler(_ => throw new Exception("No debe hacer HTTP"));
        var servicio = new ImagenProductoExternaService(new HttpClient(handler), NullLogger<ImagenProductoExternaService>.Instance);
        Assert.False((await servicio.DescargarAsync(url)).IsSuccess);
        Assert.Empty(handler.Peticiones);
    }

    [Theory]
    [InlineData("100.64.0.1", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("198.18.0.1", false)]
    [InlineData("192.0.0.1", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("fc00::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("2002:a00:1::1", false)]
    [InlineData("93.184.216.34", true)]
    [InlineData("2606:4700:4700::1111", true)]
    public void PoliticaDeDestinos_RechazaRedesPrivadasReservadasYTransicion(string direccion, bool permitido)
    {
        Assert.Equal(permitido, DestinoImagenProductoSeguro.DireccionPublica(IPAddress.Parse(direccion)));
    }

    [Fact]
    public async Task HandlerReal_NoProxyNiRedirecciones_ValidaIpAntesDeConectar()
    {
        using var handler = DestinoImagenProductoSeguro.CrearHandler();
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
        Assert.False(handler.UseCookies);
        Assert.NotNull(handler.ConnectCallback);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://127.0.0.1/p.png");
        var destino = new DnsEndPoint("127.0.0.1", 443);
        await Assert.ThrowsAsync<HttpRequestException>(async () => await DestinoImagenProductoSeguro.ConectarAsync(destino, CancellationToken.None));
    }

    [Fact]
    public async Task Descarga_NoSigueRedireccionNiAceptaHtmlOMayorDe8Mb()
    {
        foreach (var response in new[]
        {
            new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://127.0.0.1/p.png") } },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>no es imagen</html>") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[1]) }
        })
        {
            if (response.Content is ByteArrayContent)
            {
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                response.Content.Headers.ContentLength = AlmacenamientoImagenesProductoLocal.TamanoMaximoBytes + 1;
            }
            using var handler = new ProductoLookupProvidersTests.Handler(_ => response);
            var servicio = new ImagenProductoExternaService(new HttpClient(handler), NullLogger<ImagenProductoExternaService>.Instance);
            Assert.False((await servicio.DescargarAsync("https://images.example.com/p.png")).IsSuccess);
            Assert.Single(handler.Peticiones);
        }
    }

    [Fact]
    public async Task ImagenManual_TienePrioridad_NoDescargaUrlExterna()
    {
        await using var db = await TestDatabase.CreateAsync();
        using var carpeta = new Carpeta();
        var descarga = new DescargaFalsa(() => throw new Exception("No debe descargar"));
        var servicio = Crear(db, carpeta.Ruta, descarga);
        await using var manual = Imagen();
        var resultado = await servicio.CrearAsistidoAsync(Input(db), manual, "https://images.example.com/p.png");
        Assert.True(resultado.Producto.IsSuccess, resultado.Producto.ErrorMessage);
        Assert.Null(resultado.AvisoImagen);
        Assert.Equal(0, descarga.Llamadas);
        Assert.EndsWith(".webp", resultado.Producto.Value!.ImagenPrincipalRuta);
        Assert.Single(carpeta.Archivos());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DescargaOContenidoInvalido_GuardaSinImagenConAvisoYSinHuerfanos(bool fallaDescarga)
    {
        await using var db = await TestDatabase.CreateAsync();
        using var carpeta = new Carpeta();
        var descarga = new DescargaFalsa(() => fallaDescarga
            ? ServiceResult<Stream>.Failure("Error de prueba")
            : ServiceResult<Stream>.Ok(new MemoryStream("contenido falso"u8.ToArray())));
        var resultado = await Crear(db, carpeta.Ruta, descarga).CrearAsistidoAsync(
            Input(db), null, "https://images.example.com/p.png");
        Assert.True(resultado.Producto.IsSuccess, resultado.Producto.ErrorMessage);
        Assert.NotNull(resultado.AvisoImagen);
        Assert.Null(resultado.Producto.Value!.ImagenPrincipalRuta);
        Assert.Equal(2, await db.Db.Productos.CountAsync());
        Assert.Empty(carpeta.Archivos());
    }

    [Fact]
    public async Task ConfirmacionFallida_ProductoSinImagenYTodaPreparacionLimpiada()
    {
        await using var db = await TestDatabase.CreateAsync();
        using var carpeta = new Carpeta();
        var almacenamiento = new ConfirmacionFallida(Almacenamiento(carpeta.Ruta));
        var servicio = new ProductoConImagenService(new ProductoService(db.Db), almacenamiento, db.Db,
            NullLogger<ProductoConImagenService>.Instance, new DescargaFalsa(() => ServiceResult<Stream>.Ok(Imagen())));
        var resultado = await servicio.CrearAsistidoAsync(Input(db), null, "https://images.example.com/p.png");
        Assert.True(resultado.Producto.IsSuccess, resultado.Producto.ErrorMessage);
        Assert.NotNull(resultado.AvisoImagen);
        Assert.Null(resultado.Producto.Value!.ImagenPrincipalRuta);
        Assert.Empty(carpeta.Archivos());
        Assert.Equal(2, await db.Db.Productos.CountAsync());
    }

    [Fact]
    public async Task ImagenExternaValida_SoloAlGuardar_AlmacenaWebpSinDependenciaDeUrl()
    {
        await using var db = await TestDatabase.CreateAsync();
        using var carpeta = new Carpeta();
        var descarga = new DescargaFalsa(() => ServiceResult<Stream>.Ok(Imagen()));
        var modelo = ProductoLookupTests.ModeloManual();
        modelo.CategoriaId = db.Categoria.Id;
        var importacion = new ProductoLookupImportacion();
        importacion.Aplicar(modelo, ProductoLookupTests.Candidato() with { ImagenUrl = "https://images.example.com/p.png" });
        Assert.Equal(0, descarga.Llamadas);
        Assert.Single(await db.Db.Productos.ToListAsync());
        Assert.Empty(carpeta.Archivos());
        var resultado = await Crear(db, carpeta.Ruta, descarga).CrearAsistidoAsync(modelo.ToInput(), null, modelo.ImagenExternaUrl);
        Assert.True(resultado.Producto.IsSuccess, resultado.Producto.ErrorMessage);
        Assert.Null(resultado.AvisoImagen);
        Assert.Equal(1, descarga.Llamadas);
        var archivo = Assert.Single(carpeta.Archivos());
        using var codec = SKCodec.Create(archivo);
        Assert.Equal(SKEncodedImageFormat.Webp, codec!.EncodedFormat);
        Assert.DoesNotContain("https", resultado.Producto.Value!.ImagenPrincipalRuta!);
        Assert.Equal(125.50m, resultado.Producto.Value.PrecioSugerido);
        Assert.Single(await db.Db.Categorias.ToListAsync());
    }

    [Fact]
    public async Task ProductoLocalRegistradoDuranteRevision_ImpideDuplicadoAlGuardarSinDescargarImagen()
    {
        await using var db = await TestDatabase.CreateAsync();
        db.Producto.CodigoBarras = ProductoLookupTests.Codigo;
        await db.Db.SaveChangesAsync();
        using var carpeta = new Carpeta();
        var descarga = new DescargaFalsa(() => throw new Exception("No debe descargar"));
        var resultado = await Crear(db, carpeta.Ruta, descarga).CrearAsistidoAsync(
            Input(db), null, "https://images.example.com/p.png");
        Assert.False(resultado.Producto.IsSuccess);
        Assert.Contains(db.Producto.Nombre, resultado.Producto.ErrorMessage);
        Assert.Equal(0, descarga.Llamadas);
        Assert.Single(await db.Db.Productos.ToListAsync());
        Assert.Empty(carpeta.Archivos());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pagina_AltaYEdicionConservanGuardarManual(bool edicion)
    {
        await using var db = await TestDatabase.CreateAsync();
        using var carpeta = new Carpeta();
        var productos = new ProductoService(db.Db);
        var imagenes = Crear(db, carpeta.Ruta, new DescargaFalsa(() => ServiceResult<Stream>.Failure("Sin imagen")));
        var page = new ProductoEdicion();
        Set(page, "ProductoService", productos);
        Set(page, "ProductoConImagenService", imagenes);
        Set(page, "AltaProductoAsistidaService", imagenes);
        Set(page, "CategoriaService", new CategoriaService(db.Db));
        Set(page, "Logger", NullLogger<ProductoEdicion>.Instance);
        var navigation = new Navegacion();
        Set(page, "Navigation", navigation);
        if (edicion) Set(page, "Id", db.Producto.Id);
        await CallAsync(page, "CargarAsync");
        var modelo = Get<ProductoFormModel>(page, "Modelo");
        modelo.Nombre = "Guardado manual";
        modelo.CategoriaId = db.Categoria.Id;
        modelo.PrecioSugerido = 75.50m;
        modelo.CodigoBarras = " CODE 128 ";
        await CallAsync(page, "GuardarAsync", modelo);
        Assert.Equal(edicion ? 1 : 2, await db.Db.Productos.CountAsync());
        var guardado = await db.Db.Productos.AsNoTracking().SingleAsync(x => x.Nombre == "Guardado manual");
        Assert.Equal(" CODE 128 ", guardado.CodigoBarras);
        Assert.Equal(75.50m, guardado.PrecioSugerido);
        Assert.Contains(edicion ? "producto-editado" : "producto-creado", navigation.Uri);
    }

    [Fact]
    public async Task RegistroDuranteDescarga_RecompruebaDentroDeTransaccion_NoDuplicaNiDejaImagen()
    {
        await using var db = await TestDatabase.CreateAsync();
        using var carpeta = new Carpeta();
        var descarga = new DescargaPendiente();
        var servicio = Crear(db, carpeta.Ruta, descarga);
        var guardado = servicio.CrearAsistidoAsync(Input(db), null, "https://images.example.com/p.png");
        await descarga.Iniciada.Task;
        var paralelo = await new ProductoService(db.Db).CrearAsync(Input(db) with { Nombre = "Registro paralelo" });
        Assert.True(paralelo.IsSuccess, paralelo.ErrorMessage);
        descarga.Respuesta.SetResult(ServiceResult<Stream>.Ok(Imagen()));
        var resultado = await guardado;
        Assert.False(resultado.Producto.IsSuccess);
        Assert.Contains("Registro paralelo", resultado.Producto.ErrorMessage);
        Assert.Equal(2, await db.Db.Productos.CountAsync());
        Assert.Single(await db.Db.Productos.Where(x => x.CodigoBarras == ProductoLookupTests.Codigo).ToListAsync());
        Assert.Empty(carpeta.Archivos());
    }

    private sealed class DescargaPendiente : IImagenProductoExternaService
    {
        public TaskCompletionSource Iniciada { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ServiceResult<Stream>> Respuesta { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<ServiceResult<Stream>> DescargarAsync(string url, CancellationToken ct = default)
        {
            Iniciada.SetResult();
            return Respuesta.Task;
        }
    }
    internal static ProductoConImagenService Crear(TestDatabase db, string carpeta, IImagenProductoExternaService descarga) =>
        new(new ProductoService(db.Db), Almacenamiento(carpeta), db.Db, NullLogger<ProductoConImagenService>.Instance, descarga);

    private static AlmacenamientoImagenesProductoLocal Almacenamiento(string carpeta) =>
        new(Options.Create(new AlmacenamientoImagenesProductoOptions { DirectorioBase = carpeta }),
            NullLogger<AlmacenamientoImagenesProductoLocal>.Instance);
    private static ProductoInput Input(TestDatabase db) =>
        new(ProductoLookupTests.Codigo, "Producto", null, null, null, null, null, 75, db.Categoria.Id);
    private static MemoryStream Imagen()
    {
        using var bitmap = new SKBitmap(32, 32);
        bitmap.Erase(SKColors.Blue);
        using var imagen = SKImage.FromBitmap(bitmap);
        using var bytes = imagen.Encode(SKEncodedImageFormat.Png, 100);
        return new MemoryStream(bytes.ToArray());
    }
    internal sealed class DescargaFalsa(Func<ServiceResult<Stream>> respuesta) : IImagenProductoExternaService
    {
        public int Llamadas { get; private set; }
        public Task<ServiceResult<Stream>> DescargarAsync(string url, CancellationToken ct = default)
        {
            Llamadas++;
            return Task.FromResult(respuesta());
        }
    }
    internal sealed class Carpeta : IDisposable
    {
        public string Ruta { get; } = Path.Combine(Path.GetTempPath(), "resell-lookup-" + Guid.NewGuid().ToString("N"));
        public string[] Archivos() => Directory.Exists(Ruta) ? Directory.GetFiles(Ruta, "*", SearchOption.AllDirectories) : [];
        public void Dispose()
        {
            if (Directory.Exists(Ruta)) Directory.Delete(Ruta, recursive: true);
        }
    }
    private sealed class ConfirmacionFallida(IAlmacenamientoImagenesProducto inner) : IAlmacenamientoImagenesProducto
    {
        public Task<ServiceResult<ImagenProductoPreparada>> PrepararAsync(Stream contenido, CancellationToken ct = default) => inner.PrepararAsync(contenido, ct);
        public Task<ServiceResult<ImagenProductoGuardada>> ConfirmarAsync(ImagenProductoPreparada preparada, int productoId, CancellationToken ct = default) =>
            Task.FromResult(ServiceResult<ImagenProductoGuardada>.Failure("Fallo simulado"));
        public Task<ServiceResult> EliminarTemporalAsync(string id, CancellationToken ct = default) => inner.EliminarTemporalAsync(id, ct);
        public Task<ServiceResult> EliminarAsync(string ruta, CancellationToken ct = default) => inner.EliminarAsync(ruta, ct);
        public Task<ServiceResult<ImagenProductoLectura>> AbrirLecturaAsync(string ruta, CancellationToken ct = default) => inner.AbrirLecturaAsync(ruta, ct);
    }
}
