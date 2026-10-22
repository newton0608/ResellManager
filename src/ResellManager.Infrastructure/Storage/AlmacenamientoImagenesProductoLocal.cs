using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using SkiaSharp;

namespace ResellManager.Infrastructure.Storage;

public sealed class AlmacenamientoImagenesProductoLocal : IAlmacenamientoImagenesProducto
{
    public const long TamanoMaximoBytes = 8 * 1024 * 1024;
    public const int LadoMaximo = 1200;
    public const int LadoMaximoGaleria = 2000;
    public const int LadoMaximoMiniatura = 240;
    private const int PixelesMaximos = 25_000_000;
    private readonly string directorioBase;
    private readonly string directorioTemporal;
    private readonly ILogger<AlmacenamientoImagenesProductoLocal> logger;

    public AlmacenamientoImagenesProductoLocal(
        IOptions<AlmacenamientoImagenesProductoOptions> options,
        ILogger<AlmacenamientoImagenesProductoLocal> logger)
    {
        if (string.IsNullOrWhiteSpace(options.Value.DirectorioBase))
            throw new InvalidOperationException("Debe configurarse el directorio de imágenes de producto.");
        directorioBase = Path.GetFullPath(options.Value.DirectorioBase);
        directorioTemporal = Path.Combine(directorioBase, ".temporales");
        this.logger = logger;
    }

    public Task<ServiceResult<ImagenProductoPreparada>> PrepararAsync(Stream contenido, CancellationToken ct = default)
        => PrepararConLimiteAsync(contenido, LadoMaximo, 82, ct);

    public Task<ServiceResult<ImagenProductoPreparada>> PrepararGaleriaAsync(Stream contenido, CancellationToken ct = default)
        => PrepararConLimiteAsync(contenido, LadoMaximoGaleria, 88, ct);

    private async Task<ServiceResult<ImagenProductoPreparada>> PrepararConLimiteAsync(
        Stream contenido, int ladoMaximo, int calidad, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        var rutaOriginal = Path.Combine(directorioTemporal, $"RAW-{id}.tmp");
        var identificador = $"TMP-{id}.webp";
        var rutaProcesada = Path.Combine(directorioTemporal, identificador);
        var rutaMiniatura = RutaMiniatura(rutaProcesada);
        var preparada = false;
        try
        {
            Directory.CreateDirectory(directorioTemporal);
            await using (var salida = new FileStream(rutaOriginal, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 64 * 1024, FileOptions.Asynchronous))
            {
                var buffer = new byte[64 * 1024];
                long total = 0;
                while (true)
                {
                    var leidos = await contenido.ReadAsync(buffer, ct);
                    if (leidos == 0) break;
                    total += leidos;
                    if (total > TamanoMaximoBytes)
                        throw new InvalidDataException("La imagen supera el límite máximo de 8 MB.");
                    await salida.WriteAsync(buffer.AsMemory(0, leidos), ct);
                }
                if (total == 0)
                    throw new InvalidDataException("La imagen está vacía.");
            }

            await ValidarYProcesarAsync(rutaOriginal, rutaProcesada, ladoMaximo, calidad, ct, rutaMiniatura);
            preparada = true;
            return ServiceResult<ImagenProductoPreparada>.Ok(new ImagenProductoPreparada(identificador));
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidDataException ex)
        {
            return ServiceResult<ImagenProductoPreparada>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No fue posible preparar la imagen de producto.");
            return ServiceResult<ImagenProductoPreparada>.Failure("No fue posible procesar la imagen.");
        }
        finally
        {
            IntentarEliminar(rutaOriginal);
            if (!preparada)
            {
                IntentarEliminar(rutaProcesada);
                IntentarEliminar(rutaMiniatura);
            }
        }
    }

    public Task<ServiceResult<ImagenProductoGuardada>> ConfirmarAsync(
        ImagenProductoPreparada preparada, int productoId, CancellationToken ct = default)
    {
        string? definitiva = null;
        var confirmada = false;
        var miniaturaMovida = false;
        var imagenMovida = false;
        try
        {
            ct.ThrowIfCancellationRequested();
            if (productoId <= 0)
                throw new InvalidOperationException("Producto no válido.");
            var temporal = ResolverTemporal(preparada.IdentificadorTemporal);
            var nombre = $"imagen-principal-{Guid.NewGuid():N}.webp";
            var relativa = $"productos/{productoId}/{nombre}";
            definitiva = ResolverDefinitiva(relativa);
            if (!File.Exists(temporal))
                return Task.FromResult(ServiceResult<ImagenProductoGuardada>.Failure("La imagen preparada ya no está disponible."));
            Directory.CreateDirectory(Path.GetDirectoryName(definitiva)!);
            File.Move(RutaMiniatura(temporal), RutaMiniatura(definitiva), overwrite: false);
            miniaturaMovida = true;
            File.Move(temporal, definitiva, overwrite: false);
            imagenMovida = true;
            confirmada = true;
            return Task.FromResult(ServiceResult<ImagenProductoGuardada>.Ok(new ImagenProductoGuardada(relativa)));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "No fue posible confirmar la imagen del producto {ProductoId}.", productoId);
            return Task.FromResult(ServiceResult<ImagenProductoGuardada>.Failure("No fue posible guardar la imagen."));
        }
        finally
        {
            if (!confirmada && definitiva is not null)
            {
                if (imagenMovida) IntentarEliminar(definitiva);
                if (miniaturaMovida) IntentarEliminar(RutaMiniatura(definitiva));
            }
        }
    }

    public Task<ServiceResult> EliminarTemporalAsync(string identificadorTemporal, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var ruta = ResolverTemporal(identificadorTemporal);
            File.Delete(ruta);
            File.Delete(RutaMiniatura(ruta));
            return Task.FromResult(ServiceResult.Ok());
        }
        catch (FileNotFoundException) { return Task.FromResult(ServiceResult.Ok()); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "No fue posible eliminar una imagen temporal.");
            return Task.FromResult(ServiceResult.Failure("No fue posible limpiar la imagen temporal."));
        }
    }

    public Task<ServiceResult> EliminarAsync(string rutaRelativa, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var ruta = ResolverDefinitiva(rutaRelativa);
            if (File.Exists(ruta)) File.Delete(ruta);
            File.Delete(RutaMiniatura(ruta));
            return Task.FromResult(ServiceResult.Ok());
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "No fue posible eliminar la imagen {RutaRelativa}.", rutaRelativa);
            return Task.FromResult(ServiceResult.Failure("No fue posible eliminar la imagen."));
        }
    }

    public Task<ServiceResult<ImagenProductoLectura>> AbrirLecturaAsync(string rutaRelativa, CancellationToken ct = default)
        => AbrirAsync(rutaRelativa, false, ct);

    public Task<ServiceResult<ImagenProductoLectura>> AbrirMiniaturaAsync(string rutaRelativa, CancellationToken ct = default)
        => AbrirAsync(rutaRelativa, true, ct);

    private Task<ServiceResult<ImagenProductoLectura>> AbrirAsync(
        string rutaRelativa, bool miniatura, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var ruta = ResolverDefinitiva(rutaRelativa);
            var esMiniatura = miniatura && File.Exists(RutaMiniatura(ruta));
            if (esMiniatura) ruta = RutaMiniatura(ruta);
            if (!File.Exists(ruta))
                return Task.FromResult(ServiceResult<ImagenProductoLectura>.Failure("Imagen no disponible."));
            var stream = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return Task.FromResult(ServiceResult<ImagenProductoLectura>.Ok(
                new ImagenProductoLectura(stream, "image/webp", esMiniatura)));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "No fue posible abrir la imagen de producto.");
            return Task.FromResult(ServiceResult<ImagenProductoLectura>.Failure("Imagen no disponible."));
        }
    }

    // La publicación por rename impide servir miniaturas incompletas y permite reanudar el lote.
    public async Task<ServiceResult<bool>> GenerarMiniaturaAsync(string rutaRelativa, CancellationToken ct = default)
    {
        string? temporal = null;
        try
        {
            ct.ThrowIfCancellationRequested();
            var original = ResolverDefinitiva(rutaRelativa);
            var destino = RutaMiniatura(original);
            if (!File.Exists(original)) return ServiceResult<bool>.Failure("Imagen no disponible.");
            if (File.Exists(destino)) return ServiceResult<bool>.Ok(false);
            temporal = destino + $".{Guid.NewGuid():N}.tmp";
            await ValidarYProcesarAsync(original, temporal, LadoMaximoMiniatura, 80, ct);
            ct.ThrowIfCancellationRequested();
            // Un reemplazo/eliminación concurrente no debe dejar una miniatura huérfana.
            if (!File.Exists(original)) return ServiceResult<bool>.Failure("Imagen no disponible.");
            try { File.Move(temporal, destino, overwrite: false); }
            catch (IOException) when (File.Exists(destino)) { return ServiceResult<bool>.Ok(false); }
            if (!File.Exists(original))
            {
                IntentarEliminar(destino);
                return ServiceResult<bool>.Failure("Imagen no disponible.");
            }
            return ServiceResult<bool>.Ok(true);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "No fue posible generar una miniatura de producto.");
            return ServiceResult<bool>.Failure("No fue posible generar la miniatura.");
        }
        finally { if (temporal is not null) IntentarEliminar(temporal); }
    }

    private static string RutaMiniatura(string ruta) => Path.ChangeExtension(ruta, ".miniatura-v1.webp");

    private static async Task ValidarYProcesarAsync(string origen, string destino, int ladoMaximo, int calidad,
        CancellationToken ct, string? miniatura = null)
    {
        var encabezado = new byte[12];
        await using (var archivo = File.OpenRead(origen))
            _ = await archivo.ReadAsync(encabezado, ct);
        SKEncodedImageFormat? formato = encabezado[0] == 0xff && encabezado[1] == 0xd8 && encabezado[2] == 0xff
            ? SKEncodedImageFormat.Jpeg
            : encabezado.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a })
                ? SKEncodedImageFormat.Png
                : encabezado.AsSpan(0, 4).SequenceEqual("RIFF"u8) && encabezado.AsSpan(8, 4).SequenceEqual("WEBP"u8)
                    ? SKEncodedImageFormat.Webp : null;
        if (formato is null)
            throw new InvalidDataException("Selecciona una imagen JPEG, PNG o WebP válida.");

        ct.ThrowIfCancellationRequested();
        using var codec = SKCodec.Create(origen);
        if (codec is null || codec.EncodedFormat != formato
            || codec.Info.Width <= 0 || codec.Info.Height <= 0
            || (long)codec.Info.Width * codec.Info.Height > PixelesMaximos)
            throw new InvalidDataException("La imagen está dañada o tiene dimensiones demasiado grandes.");
        using var bitmap = SKBitmap.Decode(codec);
        if (bitmap is null)
            throw new InvalidDataException("La imagen está dañada o no es válida.");
        using var orientada = AplicarOrientacion(bitmap, codec.EncodedOrigin);
        var fuente = orientada ?? bitmap;
        await CodificarAsync(fuente, destino, ladoMaximo, calidad, ct);
        if (miniatura is not null)
            await CodificarAsync(fuente, miniatura, LadoMaximoMiniatura, 80, ct);
    }

    private static async Task CodificarAsync(SKBitmap fuente, string destino, int ladoMaximo, int calidad, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var escala = Math.Min(1d, (double)ladoMaximo / Math.Max(fuente.Width, fuente.Height));
        var ancho = Math.Max(1, (int)Math.Round(fuente.Width * escala));
        var alto = Math.Max(1, (int)Math.Round(fuente.Height * escala));
        using var redimensionada = escala == 1d ? null : fuente.Resize(
            new SKImageInfo(ancho, alto, fuente.ColorType, fuente.AlphaType),
            new SKSamplingOptions(SKCubicResampler.Mitchell));
        if (escala != 1d && redimensionada is null)
            throw new InvalidDataException("No fue posible redimensionar la imagen.");
        using var imagen = SKImage.FromBitmap(redimensionada ?? fuente);
        using var datos = imagen.Encode(SKEncodedImageFormat.Webp, calidad);
        if (datos is null || datos.Size > TamanoMaximoBytes)
            throw new InvalidDataException("No fue posible convertir la imagen a WebP.");
        await using var salida = new FileStream(destino, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 64 * 1024, FileOptions.Asynchronous);
        datos.SaveTo(salida);
        await salida.FlushAsync(ct);
    }

    private static SKBitmap? AplicarOrientacion(SKBitmap origen, SKEncodedOrigin orientacion)
    {
        if (orientacion == SKEncodedOrigin.TopLeft) return null;
        var intercambia = orientacion is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var ancho = intercambia ? origen.Height : origen.Width;
        var alto = intercambia ? origen.Width : origen.Height;
        var destino = new SKBitmap(new SKImageInfo(ancho, alto, origen.ColorType, origen.AlphaType, origen.ColorSpace));
        try
        {
            var matriz = orientacion switch
            {
                SKEncodedOrigin.TopRight => Matriz(-1, 0, origen.Width, 0, 1, 0),
                SKEncodedOrigin.BottomRight => Matriz(-1, 0, origen.Width, 0, -1, origen.Height),
                SKEncodedOrigin.BottomLeft => Matriz(1, 0, 0, 0, -1, origen.Height),
                SKEncodedOrigin.LeftTop => Matriz(0, 1, 0, 1, 0, 0),
                SKEncodedOrigin.RightTop => Matriz(0, -1, origen.Height, 1, 0, 0),
                SKEncodedOrigin.RightBottom => Matriz(0, -1, origen.Height, -1, 0, origen.Width),
                SKEncodedOrigin.LeftBottom => Matriz(0, 1, 0, -1, 0, origen.Width),
                _ => throw new InvalidDataException("La orientación de la imagen no es compatible.")
            };
            using var canvas = new SKCanvas(destino);
            canvas.Clear(SKColors.Transparent);
            canvas.SetMatrix(matriz);
            using var paint = new SKPaint();
            canvas.DrawBitmap(origen, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), paint);
            canvas.Flush();
            return destino;
        }
        catch { destino.Dispose(); throw; }
    }

    private static SKMatrix Matriz(float sx, float kx, float tx, float ky, float sy, float ty) =>
        new() { ScaleX = sx, SkewX = kx, TransX = tx, SkewY = ky, ScaleY = sy, TransY = ty, Persp0 = 0, Persp1 = 0, Persp2 = 1 };

    private string ResolverTemporal(string nombre)
    {
        if (!Regex.IsMatch(nombre, @"^TMP-[0-9a-f]{32}\.webp\z", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Identificador temporal no válido.");
        return Path.Combine(directorioTemporal, nombre);
    }

    private string ResolverDefinitiva(string ruta)
    {
        var coincidencia = Regex.Match(ruta, @"^productos/([1-9][0-9]*)/(imagen-principal-[0-9a-f]{32}\.webp)\z",
            RegexOptions.CultureInvariant);
        if (!coincidencia.Success)
            throw new InvalidOperationException("Ruta de imagen no válida.");
        return Path.Combine(directorioBase, coincidencia.Groups[1].Value, coincidencia.Groups[2].Value);
    }

    private void IntentarEliminar(string ruta)
    {
        try { if (File.Exists(ruta)) File.Delete(ruta); }
        catch (Exception ex) { logger.LogError(ex, "No fue posible limpiar un archivo temporal de imagen."); }
    }
}
