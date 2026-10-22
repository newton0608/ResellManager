using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Infrastructure.Services;
using ResellManager.Infrastructure.Storage;
using SkiaSharp;

namespace ResellManager.Tests;

public sealed class ProductoMiniaturasTests : IDisposable
{
    private readonly string carpeta = Path.Combine(Path.GetTempPath(), $"rm-miniaturas-{Guid.NewGuid():N}");
    private AlmacenamientoImagenesProductoLocal Almacenamiento => new(
        Options.Create(new AlmacenamientoImagenesProductoOptions { DirectorioBase = carpeta }),
        NullLogger<AlmacenamientoImagenesProductoLocal>.Instance);

    [Theory]
    [InlineData(600, 900, 160, 240)]
    [InlineData(900, 600, 240, 160)]
    [InlineData(40, 20, 40, 20)]
    public async Task NuevaFoto_GeneraAmbasVersionesSinRecorteNiAmpliacion_YLimpiaElPar(int ancho, int alto, int esperadoAncho, int esperadoAlto)
    {
        using var entrada = Imagen(ancho, alto);
        var almacenamiento = Almacenamiento;
        var preparada = await almacenamiento.PrepararGaleriaAsync(entrada);
        Assert.True(preparada.IsSuccess, preparada.ErrorMessage);
        Assert.Equal(2, Archivos().Length);
        var guardada = await almacenamiento.ConfirmarAsync(preparada.Value!, 1);
        Assert.True(guardada.IsSuccess, guardada.ErrorMessage);
        Assert.Equal(2, Archivos().Length);
        Assert.DoesNotContain(Archivos(), x => x.Contains(".temporales"));
        var ruta = guardada.Value!.RutaRelativa;
        var miniatura = await almacenamiento.AbrirMiniaturaAsync(ruta);
        Assert.True(miniatura.IsSuccess, miniatura.ErrorMessage);
        Assert.True(miniatura.Value!.EsMiniatura);
        using (miniatura.Value.Contenido)
        using (var bitmap = SKBitmap.Decode(miniatura.Value.Contenido))
        {
            Assert.Equal(esperadoAncho, bitmap.Width);
            Assert.Equal(esperadoAlto, bitmap.Height);
            Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);
            Assert.True(bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2).Alpha > 200);
        }
        var grande = await almacenamiento.AbrirLecturaAsync(ruta);
        using (grande.Value!.Contenido)
        using (var bitmap = SKBitmap.Decode(grande.Value.Contenido))
        {
            Assert.Equal(ancho, bitmap.Width);
            Assert.Equal(alto, bitmap.Height);
        }
        Assert.True((await almacenamiento.EliminarAsync(ruta)).IsSuccess);
        Assert.Empty(Archivos());
    }

    [Fact]
    public async Task ProductoExistente_GeneraDesdeArchivoPersistido_SinCambiarOriginalNiReferencias_YEsReanudable()
    {
        await using var test = await TestDatabase.CreateAsync();
        var ruta = $"productos/{test.Producto.Id}/imagen-principal-{Guid.NewGuid():N}.webp";
        var original = Archivo(ruta);
        Directory.CreateDirectory(Path.GetDirectoryName(original)!);
        using var foto = Imagen(600, 900, SKEncodedImageFormat.Webp);
        await File.WriteAllBytesAsync(original, foto.ToArray());
        test.Producto.ImagenPrincipalRuta = ruta;
        // La misma referencia en portada/galería se procesa una sola vez.
        test.Db.ProductoImagenes.Add(new ProductoImagen { ProductoId = test.Producto.Id, RutaRelativa = ruta, Orden = 0 });
        await test.Db.SaveChangesAsync();
        var hash = SHA256.HashData(await File.ReadAllBytesAsync(original));
        var almacenamiento = Almacenamiento;
        var antes = await almacenamiento.AbrirMiniaturaAsync(ruta);
        Assert.False(antes.Value!.EsMiniatura);
        using (antes.Value.Contenido) Assert.Equal(hash, SHA256.HashData(antes.Value.Contenido));

        var proceso = new ProductoMiniaturasService(test.Db, almacenamiento, NullLogger<ProductoMiniaturasService>.Instance);
        Assert.Equal(new ResultadoMiniaturas(1, 0, 0), await proceso.GenerarPendientesAsync());
        var derivados = Archivos().Single(x => x.EndsWith(".miniatura-v1.webp"));
        var hashMiniatura = SHA256.HashData(await File.ReadAllBytesAsync(derivados));
        var fecha = File.GetLastWriteTimeUtc(derivados);
        Assert.Equal(new ResultadoMiniaturas(0, 1, 0), await proceso.GenerarPendientesAsync());
        Assert.Equal(fecha, File.GetLastWriteTimeUtc(derivados));
        Assert.Equal(hashMiniatura, SHA256.HashData(await File.ReadAllBytesAsync(derivados)));
        Assert.Equal(hash, SHA256.HashData(await File.ReadAllBytesAsync(original)));
        Assert.Equal(ruta, await test.Db.Productos.AsNoTracking().Where(x => x.Id == test.Producto.Id).Select(x => x.ImagenPrincipalRuta).SingleAsync());
        Assert.Single(await test.Db.ProductoImagenes.AsNoTracking().ToListAsync());
        var lectura = await almacenamiento.AbrirMiniaturaAsync(ruta);
        Assert.True(lectura.Value!.EsMiniatura);
        using (lectura.Value.Contenido)
        using (var bitmap = SKBitmap.Decode(lectura.Value.Contenido))
        {
            Assert.Equal(160, bitmap.Width);
            Assert.Equal(240, bitmap.Height);
            Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);
        }
    }

    [Fact]
    public async Task OrientacionExif_SeAplicaTambienALaMiniatura()
    {
        using var jpeg = Imagen(600, 300, SKEncodedImageFormat.Jpeg);
        var bytes = jpeg.ToArray();
        // APP1 Exif: orientación 6 (90 grados), TIFF little endian.
        byte[] exif = [255,225,0,34,69,120,105,102,0,0,73,73,42,0,8,0,0,0,1,0,18,1,3,0,1,0,0,0,6,0,0,0,0,0,0,0];
        using var entrada = new MemoryStream(bytes[..2].Concat(exif).Concat(bytes[2..]).ToArray());
        var almacenamiento = Almacenamiento;
        var preparada = await almacenamiento.PrepararAsync(entrada);
        Assert.True(preparada.IsSuccess, preparada.ErrorMessage);
        var guardada = await almacenamiento.ConfirmarAsync(preparada.Value!, 1);
        var lectura = await almacenamiento.AbrirMiniaturaAsync(guardada.Value!.RutaRelativa);
        using (lectura.Value!.Contenido)
        using (var bitmap = SKBitmap.Decode(lectura.Value.Contenido))
        {
            Assert.Equal(120, bitmap.Width);
            Assert.Equal(240, bitmap.Height);
        }
    }

    [Fact]
    public async Task FalloOCancelacion_NoPublicaMiniaturaParcial_YRechazaTraversal()
    {
        var almacenamiento = Almacenamiento;
        var ruta = "productos/1/imagen-principal-" + Guid.NewGuid().ToString("N") + ".webp";
        Directory.CreateDirectory(Path.GetDirectoryName(Archivo(ruta))!);
        await File.WriteAllTextAsync(Archivo(ruta), "archivo dañado");
        Assert.False((await almacenamiento.GenerarMiniaturaAsync(ruta)).IsSuccess);
        Assert.Single(Archivos());
        Assert.False((await almacenamiento.GenerarMiniaturaAsync("productos/1/../../fuera.webp")).IsSuccess);
        Assert.False((await almacenamiento.AbrirMiniaturaAsync("productos/1/../../fuera.webp")).IsSuccess);
        using var ct = new CancellationTokenSource();
        ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => almacenamiento.GenerarMiniaturaAsync(ruta, ct.Token));
        Assert.Single(Archivos());
    }

    private static MemoryStream Imagen(int ancho, int alto, SKEncodedImageFormat formato = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(ancho, alto);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { Color = SKColors.Blue };
        canvas.DrawRect(ancho / 4f, alto / 4f, ancho / 2f, alto / 2f, paint);
        using var imagen = SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(formato, 88);
        return new(datos.ToArray());
    }

    private string Archivo(string ruta) => Path.Combine(carpeta, ruta.Split('/')[1], ruta.Split('/')[2]);
    private string[] Archivos() => Directory.Exists(carpeta) ? Directory.GetFiles(carpeta, "*", SearchOption.AllDirectories) : [];
    public void Dispose() { if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true); }
}
