using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Infrastructure.Services;
using ResellManager.Infrastructure.Storage;
using SkiaSharp;

namespace ResellManager.Tests;

public sealed class ImagenPrincipalProductoTests
{
    [Fact]
    public async Task CrearSinImagen_ConservaProductoExistenteYRutaNula()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        var servicio = CrearServicio(test, carpeta.Ruta);
        var resultado = await servicio.CrearAsync(Input(test), null);
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Null(resultado.Value!.ImagenPrincipalRuta);
        Assert.Equal("Producto con foto", resultado.Value.Nombre);
        Assert.Equal(123m, resultado.Value.PrecioSugerido);
        Assert.Null((await test.Db.Productos.AsNoTracking().SingleAsync(x => x.Id == resultado.Value.Id)).ImagenPrincipalRuta);
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public async Task CrearImagenValida_GuardaSoloRutaRelativaYWebp(SKEncodedImageFormat formato)
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        var servicio = CrearServicio(test, carpeta.Ruta);
        await using var imagen = Imagen(formato: formato);
        var resultado = await servicio.CrearAsync(Input(test), imagen);
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        var ruta = resultado.Value!.ImagenPrincipalRuta!;
        Assert.StartsWith($"productos/{resultado.Value.Id}/imagen-principal-", ruta);
        Assert.EndsWith(".webp", ruta);
        Assert.False(Path.IsPathRooted(ruta));
        Assert.Equal(ruta, (await test.Db.Productos.AsNoTracking().SingleAsync(x => x.Id == resultado.Value.Id)).ImagenPrincipalRuta);
        var archivo = Archivo(carpeta.Ruta, ruta);
        Assert.True(File.Exists(archivo));
        using var codec = SKCodec.Create(archivo);
        Assert.Equal(SKEncodedImageFormat.Webp, codec!.EncodedFormat);
        Assert.True(codec.Info.Width <= 1200 && codec.Info.Height <= 1200);
    }

    [Fact]
    public async Task RechazaArchivoQueNoEsImagenSinCrearProducto()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        var servicio = CrearServicio(test, carpeta.Ruta);
        await using var falso = new MemoryStream("no es una imagen"u8.ToArray());
        var resultado = await servicio.CrearAsync(Input(test), falso);
        Assert.False(resultado.IsSuccess);
        Assert.Single(await test.Db.Productos.AsNoTracking().ToListAsync());
        Assert.Empty(Archivos(carpeta.Ruta));
    }

    [Fact]
    public async Task RechazaArchivoDemasiadoGrandeSinTemporales()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        var servicio = CrearServicio(test, carpeta.Ruta);
        await using var grande = new MemoryStream(new byte[AlmacenamientoImagenesProductoLocal.TamanoMaximoBytes + 1]);
        var resultado = await servicio.CrearAsync(Input(test), grande);
        Assert.False(resultado.IsSuccess);
        Assert.Contains("8 MB", resultado.ErrorMessage);
        Assert.Empty(Archivos(carpeta.Ruta));
    }

    [Fact]
    public async Task ReemplazarImagen_ConservaAnteriorHastaPersistirNuevaYDespuesLaElimina()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        var servicio = CrearServicio(test, carpeta.Ruta);
        await using var inicial = Imagen();
        var creado = await servicio.CrearAsync(Input(test), inicial);
        Assert.True(creado.IsSuccess, creado.ErrorMessage);
        var anterior = creado.Value!.ImagenPrincipalRuta!;
        await using var reemplazo = Imagen(SKColors.Blue);
        var editado = await servicio.EditarAsync(creado.Value.Id, Input(test) with { Nombre = "Actualizado" }, reemplazo, false);
        Assert.True(editado.IsSuccess, editado.ErrorMessage);
        Assert.NotEqual(anterior, editado.Value!.ImagenPrincipalRuta);
        Assert.False(File.Exists(Archivo(carpeta.Ruta, anterior)));
        Assert.True(File.Exists(Archivo(carpeta.Ruta, editado.Value.ImagenPrincipalRuta!)));
        Assert.Equal("Actualizado", editado.Value.Nombre);
    }

    [Fact]
    public async Task EliminarImagen_BorraReferenciaYArchivo()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        var servicio = CrearServicio(test, carpeta.Ruta);
        await using var inicial = Imagen();
        var creado = await servicio.CrearAsync(Input(test), inicial);
        var anterior = creado.Value!.ImagenPrincipalRuta!;
        var editado = await servicio.EditarAsync(creado.Value.Id, Input(test), null, true);
        Assert.True(editado.IsSuccess, editado.ErrorMessage);
        Assert.Null(editado.Value!.ImagenPrincipalRuta);
        Assert.Null((await test.Db.Productos.AsNoTracking().SingleAsync(x => x.Id == creado.Value.Id)).ImagenPrincipalRuta);
        Assert.False(File.Exists(Archivo(carpeta.Ruta, anterior)));
    }

    [Fact]
    public async Task FalloAlCrearTrasPreparar_NoDejaArchivoNiReferencia()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        var servicio = CrearServicio(test, carpeta.Ruta);
        await using var imagen = Imagen();
        var resultado = await servicio.CrearAsync(Input(test) with { CategoriaId = -1 }, imagen);
        Assert.False(resultado.IsSuccess);
        Assert.Single(await test.Db.Productos.AsNoTracking().ToListAsync());
        Assert.Empty(Archivos(carpeta.Ruta));
    }

    [Fact]
    public async Task FalloAlEditar_PreservaImagenAnteriorYProducto()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        var servicio = CrearServicio(test, carpeta.Ruta);
        await using var inicial = Imagen();
        var creado = await servicio.CrearAsync(Input(test), inicial);
        var anterior = creado.Value!.ImagenPrincipalRuta!;
        await using var reemplazo = Imagen(SKColors.Green);
        var resultado = await servicio.EditarAsync(creado.Value.Id, Input(test) with { CategoriaId = -1 }, reemplazo, false);
        Assert.False(resultado.IsSuccess);
        Assert.Equal(anterior, (await test.Db.Productos.AsNoTracking().SingleAsync(x => x.Id == creado.Value.Id)).ImagenPrincipalRuta);
        Assert.True(File.Exists(Archivo(carpeta.Ruta, anterior)));
        Assert.Single(Archivos(carpeta.Ruta));
    }

    [Fact]
    public async Task FalloDeConfirmacion_NoPersisteRutaNiDejaTemporales()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        var almacenamiento = new ConfirmacionFallida(CrearAlmacenamiento(carpeta.Ruta));
        var servicio = new ProductoConImagenService(new ProductoService(test.Db),
            almacenamiento, test.Db, NullLogger<ProductoConImagenService>.Instance);
        await using var imagen = Imagen();
        var resultado = await servicio.CrearAsync(Input(test), imagen);
        Assert.False(resultado.IsSuccess);
        Assert.Single(await test.Db.Productos.AsNoTracking().ToListAsync());
        Assert.Empty(Archivos(carpeta.Ruta));
    }

    [Fact]
    public async Task AlmacenamientoRechazaTraversal()
    {
        using var carpeta = new CarpetaTemporal();
        var almacenamiento = CrearAlmacenamiento(carpeta.Ruta);
        var resultado = await almacenamiento.AbrirLecturaAsync("productos/1/../../secreto.webp");
        Assert.False(resultado.IsSuccess);
    }

    private static ProductoInput Input(TestDatabase test) =>
        new(null, "Producto con foto", null, null, null, null, null, 123m, test.Categoria.Id);

    private static ProductoConImagenService CrearServicio(TestDatabase test, string ruta) =>
        new(new ProductoService(test.Db), CrearAlmacenamiento(ruta), test.Db,
            NullLogger<ProductoConImagenService>.Instance);

    private static AlmacenamientoImagenesProductoLocal CrearAlmacenamiento(string ruta) =>
        new(Options.Create(new AlmacenamientoImagenesProductoOptions { DirectorioBase = ruta }),
            NullLogger<AlmacenamientoImagenesProductoLocal>.Instance);

    private static MemoryStream Imagen(SKColor? color = null, SKEncodedImageFormat formato = SKEncodedImageFormat.Jpeg)
    {
        using var bitmap = new SKBitmap(1800, 900);
        bitmap.Erase(color ?? SKColors.Red);
        using var imagen = SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(formato, 85);
        return new MemoryStream(datos.ToArray());
    }

    private static string Archivo(string baseDir, string relativa)
    {
        var partes = relativa.Split('/');
        return Path.Combine(baseDir, partes[1], partes[2]);
    }

    private static string[] Archivos(string ruta) =>
        Directory.Exists(ruta) ? Directory.GetFiles(ruta, "*", SearchOption.AllDirectories) : [];

    private sealed class ConfirmacionFallida(IAlmacenamientoImagenesProducto interior)
        : IAlmacenamientoImagenesProducto
    {
        public Task<ServiceResult<ImagenProductoPreparada>> PrepararAsync(Stream contenido, CancellationToken ct = default)
            => interior.PrepararAsync(contenido, ct);
        public Task<ServiceResult<ImagenProductoGuardada>> ConfirmarAsync(
            ImagenProductoPreparada preparada, int productoId, CancellationToken ct = default)
            => Task.FromResult(ServiceResult<ImagenProductoGuardada>.Failure("Disco no disponible."));
        public Task<ServiceResult> EliminarTemporalAsync(string identificadorTemporal, CancellationToken ct = default)
            => interior.EliminarTemporalAsync(identificadorTemporal, ct);
        public Task<ServiceResult> EliminarAsync(string rutaRelativa, CancellationToken ct = default)
            => interior.EliminarAsync(rutaRelativa, ct);
        public Task<ServiceResult<ImagenProductoLectura>> AbrirLecturaAsync(string rutaRelativa, CancellationToken ct = default)
            => interior.AbrirLecturaAsync(rutaRelativa, ct);
    }

    private sealed class CarpetaTemporal : IDisposable
    {
        public string Ruta { get; } = Path.Combine(Path.GetTempPath(), "resellmanager-productos-" + Guid.NewGuid().ToString("N"));
        public void Dispose()
        {
            if (Directory.Exists(Ruta)) Directory.Delete(Ruta, recursive: true);
        }
    }
}
