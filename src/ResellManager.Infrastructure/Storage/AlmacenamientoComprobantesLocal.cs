using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using SkiaSharp;

namespace ResellManager.Infrastructure.Storage;

public sealed class AlmacenamientoComprobantesLocal : IAlmacenamientoComprobantes
{
    public const long TamanoMaximoBytes = ReglasComprobanteCompra.TamanoMaximoBytes;
    public const int LadoMaximoImagen = 1800;
    public const int CalidadImagen = 85;

    private const string CarpetaComprobantes = "comprobantes";
    private const string CarpetaTemporales = ".temporales-comprobantes";
    private const int PixelesMaximos = 50_000_000;

    private readonly string directorioBase;
    private readonly string directorioComprobantes;
    private readonly string directorioTemporales;
    private readonly ILogger<AlmacenamientoComprobantesLocal> logger;

    public AlmacenamientoComprobantesLocal(
        IOptions<AlmacenamientoComprobantesOptions> options,
        ILogger<AlmacenamientoComprobantesLocal> logger
    )
    {
        if (string.IsNullOrWhiteSpace(options.Value.DirectorioBase))
            throw new InvalidOperationException("Debe configurarse el directorio de comprobantes.");

        directorioBase = Path.GetFullPath(options.Value.DirectorioBase);
        directorioComprobantes = Path.Combine(directorioBase, CarpetaComprobantes);
        directorioTemporales = Path.Combine(directorioBase, CarpetaTemporales);
        this.logger = logger;
    }

    public async Task<ServiceResult<ComprobantePreparadoDto>> PrepararAsync(
        ArchivoComprobanteInput archivo,
        CancellationToken ct = default
    )
    {
        var idOperacion = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var rutaEntrada = Path.Combine(directorioTemporales, $"RAW-{idOperacion}.tmp");
        string? rutaPreparada = null;
        var conservarPreparado = false;

        try
        {
            Directory.CreateDirectory(directorioTemporales);
            var tamanoEntrada = await CopiarConLimiteAsync(archivo.Contenido, rutaEntrada, ct);
            if (tamanoEntrada == 0)
                return ServiceResult<ComprobantePreparadoDto>.Failure("El archivo está vacío.");

            var tipo = await DetectarTipoAsync(rutaEntrada, ct);
            if (tipo is null)
                return ServiceResult<ComprobantePreparadoDto>.Failure(
                    "El formato no es válido. Adjunta una imagen JPG, JPEG, PNG, WebP o un PDF."
                );

            var nombreFinal = $"CMP-{Guid.NewGuid():N}".ToUpperInvariant() + tipo.Extension;
            var identificadorTemporal = $"TMP-{idOperacion}{tipo.Extension}";
            rutaPreparada = Path.Combine(directorioTemporales, identificadorTemporal);

            if (tipo.FormatoImagen.HasValue)
                await ProcesarImagenAsync(rutaEntrada, rutaPreparada, tipo, ct);
            else
                File.Move(rutaEntrada, rutaPreparada, overwrite: false);

            var info = new FileInfo(rutaPreparada);
            var rutaRelativa = $"{CarpetaComprobantes}/{nombreFinal}";
            conservarPreparado = true;
            return ServiceResult<ComprobantePreparadoDto>.Ok(
                new ComprobantePreparadoDto(
                    identificadorTemporal,
                    rutaRelativa,
                    nombreFinal,
                    tipo.ContentType,
                    info.Length
                )
            );
        }
        catch (ArchivoComprobanteException ex)
        {
            return ServiceResult<ComprobantePreparadoDto>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "No fue posible preparar el comprobante {NombreOriginal} ({ContentType}).",
                Path.GetFileName(archivo.NombreOriginal),
                archivo.ContentType
            );
            return ServiceResult<ComprobantePreparadoDto>.Failure(
                "No fue posible procesar el comprobante. Verifica el archivo e intenta nuevamente."
            );
        }
        finally
        {
            IntentarEliminarTemporal(rutaEntrada);
            if (!conservarPreparado && rutaPreparada is not null)
                IntentarEliminarTemporal(rutaPreparada);
        }
    }

    public Task<ServiceResult<ComprobanteGuardadoDto>> ConfirmarAsync(
        ComprobantePreparadoDto archivo,
        CancellationToken ct = default
    )
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var rutaTemporal = ResolverTemporal(archivo.IdentificadorTemporal);
            var rutaFinal = ResolverDefinitiva(archivo.RutaRelativa);
            Directory.CreateDirectory(directorioComprobantes);

            if (!File.Exists(rutaTemporal))
                return Task.FromResult(
                    ServiceResult<ComprobanteGuardadoDto>.Failure(
                        "El archivo temporal del comprobante ya no está disponible."
                    )
                );
            if (File.Exists(rutaFinal))
                return Task.FromResult(
                    ServiceResult<ComprobanteGuardadoDto>.Failure(
                        "No fue posible confirmar el comprobante porque su destino ya existe."
                    )
                );

            File.Move(rutaTemporal, rutaFinal, overwrite: false);
            var info = new FileInfo(rutaFinal);
            return Task.FromResult(
                ServiceResult<ComprobanteGuardadoDto>.Ok(
                    new ComprobanteGuardadoDto(
                        archivo.RutaRelativa,
                        archivo.NombreArchivo,
                        archivo.ContentType,
                        info.Length
                    )
                )
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No fue posible confirmar el comprobante preparado.");
            return Task.FromResult(
                ServiceResult<ComprobanteGuardadoDto>.Failure(
                    "No fue posible guardar definitivamente el comprobante. Intenta nuevamente."
                )
            );
        }
    }

    public Task<ServiceResult> EliminarTemporalAsync(
        string identificadorTemporal,
        CancellationToken ct = default
    )
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            EliminarArchivoSiExiste(ResolverTemporal(identificadorTemporal));
            return Task.FromResult(ServiceResult.Ok());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No fue posible limpiar un comprobante temporal.");
            return Task.FromResult(
                ServiceResult.Failure("No fue posible limpiar el archivo temporal.")
            );
        }
    }

    public Task<ServiceResult> EliminarAsync(
        string rutaRelativa,
        CancellationToken ct = default
    )
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            EliminarArchivoSiExiste(ResolverDefinitiva(rutaRelativa));
            return Task.FromResult(ServiceResult.Ok());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No fue posible eliminar un comprobante definitivo.");
            return Task.FromResult(ServiceResult.Failure("No fue posible limpiar el comprobante."));
        }
    }

    public Task<ServiceResult<ArchivoComprobanteLecturaDto>> AbrirLecturaAsync(
        string rutaRelativa,
        CancellationToken ct = default
    )
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var ruta = ResolverDefinitiva(rutaRelativa);
            if (!File.Exists(ruta))
                return Task.FromResult(
                    ServiceResult<ArchivoComprobanteLecturaDto>.Failure(
                        "El archivo del comprobante no está disponible."
                    )
                );

            var tipo = TipoPorExtension(Path.GetExtension(ruta));
            if (tipo is null)
                return Task.FromResult(
                    ServiceResult<ArchivoComprobanteLecturaDto>.Failure(
                        "El tipo del comprobante almacenado no es válido."
                    )
                );

            var stream = new FileStream(
                ruta,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan
            );
            return Task.FromResult(
                ServiceResult<ArchivoComprobanteLecturaDto>.Ok(
                    new ArchivoComprobanteLecturaDto(stream, tipo.ContentType, stream.Length)
                )
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No fue posible abrir un comprobante para consulta.");
            return Task.FromResult(
                ServiceResult<ArchivoComprobanteLecturaDto>.Failure(
                    "No fue posible abrir el comprobante."
                )
            );
        }
    }

    private static async Task<long> CopiarConLimiteAsync(
        Stream origen,
        string destino,
        CancellationToken ct
    )
    {
        await using var salida = new FileStream(
            destino,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );
        var buffer = new byte[64 * 1024];
        long total = 0;

        while (true)
        {
            var leidos = await origen.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (leidos == 0)
                break;

            total += leidos;
            if (total > TamanoMaximoBytes)
                throw new ArchivoComprobanteException(
                    "El comprobante supera el límite máximo de 10 MB."
                );

            await salida.WriteAsync(buffer.AsMemory(0, leidos), ct);
        }

        return total;
    }

    private static async Task<TipoComprobante?> DetectarTipoAsync(
        string ruta,
        CancellationToken ct
    )
    {
        var encabezado = new byte[12];
        await using var stream = new FileStream(
            ruta,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );
        var leidos = await stream.ReadAsync(encabezado.AsMemory(), ct);

        if (leidos >= 5 && encabezado.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            return Tipos.Pdf;
        if (
            leidos >= 8
            && encabezado.AsSpan(0, 8).SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }
            )
        )
            return Tipos.Png;
        if (
            leidos >= 3
            && encabezado[0] == 0xFF
            && encabezado[1] == 0xD8
            && encabezado[2] == 0xFF
        )
            return Tipos.Jpeg;
        if (
            leidos >= 12
            && encabezado.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && encabezado.AsSpan(8, 4).SequenceEqual("WEBP"u8)
        )
            return Tipos.Webp;

        return null;
    }

    private static async Task ProcesarImagenAsync(
        string entrada,
        string salida,
        TipoComprobante tipo,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        using var bitmap = SKBitmap.Decode(entrada);
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            throw new ArchivoComprobanteException(
                "La imagen seleccionada está dañada o no es válida."
            );
        if ((long)bitmap.Width * bitmap.Height > PixelesMaximos)
            throw new ArchivoComprobanteException("La imagen tiene dimensiones demasiado grandes.");

        var (ancho, alto) = CalcularDimensiones(bitmap.Width, bitmap.Height);
        using var redimensionada =
            ancho == bitmap.Width && alto == bitmap.Height
                ? null
                : bitmap.Resize(
                    new SKImageInfo(ancho, alto, bitmap.ColorType, bitmap.AlphaType),
                    new SKSamplingOptions(SKCubicResampler.Mitchell)
                );
        if (ancho != bitmap.Width && redimensionada is null)
            throw new ArchivoComprobanteException("No fue posible redimensionar la imagen.");

        using var imagen = SKImage.FromBitmap(redimensionada ?? bitmap);
        using var datos = imagen.Encode(tipo.FormatoImagen!.Value, tipo.Calidad);
        if (datos is null)
            throw new ArchivoComprobanteException("No fue posible convertir la imagen.");

        await using var stream = new FileStream(
            salida,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );
        datos.SaveTo(stream);
        await stream.FlushAsync(ct);
    }

    private static (int Ancho, int Alto) CalcularDimensiones(int ancho, int alto)
    {
        var ladoMayor = Math.Max(ancho, alto);
        if (ladoMayor <= LadoMaximoImagen)
            return (ancho, alto);

        var escala = (double)LadoMaximoImagen / ladoMayor;
        return (
            Math.Max(1, (int)Math.Round(ancho * escala)),
            Math.Max(1, (int)Math.Round(alto * escala))
        );
    }

    private string ResolverTemporal(string identificador)
    {
        if (!EsIdentificadorTemporalValido(identificador))
            throw new InvalidOperationException("Identificador temporal no válido.");

        return AsegurarDentroDe(
            directorioTemporales,
            Path.Combine(directorioTemporales, identificador)
        );
    }

    private string ResolverDefinitiva(string rutaRelativa)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa) || Path.IsPathRooted(rutaRelativa))
            throw new InvalidOperationException("Ruta de comprobante no válida.");

        var normalizada = rutaRelativa.Replace('\\', '/');
        var segmentos = normalizada.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (
            segmentos.Length != 2
            || !string.Equals(segmentos[0], CarpetaComprobantes, StringComparison.Ordinal)
            || !EsNombreDefinitivoValido(segmentos[1])
        )
            throw new InvalidOperationException("Ruta de comprobante no válida.");

        return AsegurarDentroDe(
            directorioComprobantes,
            Path.Combine(directorioBase, segmentos[0], segmentos[1])
        );
    }

    private static string AsegurarDentroDe(string directorio, string ruta)
    {
        var baseCompleta = Path.GetFullPath(directorio) + Path.DirectorySeparatorChar;
        var rutaCompleta = Path.GetFullPath(ruta);
        var comparacion = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!rutaCompleta.StartsWith(baseCompleta, comparacion))
            throw new InvalidOperationException(
                "La ruta intenta salir del almacenamiento administrado."
            );

        return rutaCompleta;
    }

    private static bool EsIdentificadorTemporalValido(string valor)
    {
        var extension = Path.GetExtension(valor);
        var nombre = Path.GetFileNameWithoutExtension(valor);
        return valor == Path.GetFileName(valor)
            && nombre.StartsWith("TMP-", StringComparison.Ordinal)
            && nombre.Length == 36
            && Guid.TryParseExact(nombre[4..], "N", out _)
            && TipoPorExtension(extension) is not null;
    }

    private static bool EsNombreDefinitivoValido(string valor)
    {
        var extension = Path.GetExtension(valor);
        var nombre = Path.GetFileNameWithoutExtension(valor);
        return valor == Path.GetFileName(valor)
            && nombre.StartsWith("CMP-", StringComparison.Ordinal)
            && nombre.Length == 36
            && Guid.TryParseExact(nombre[4..], "N", out _)
            && TipoPorExtension(extension) is not null;
    }

    private static TipoComprobante? TipoPorExtension(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".jpg" => Tipos.Jpeg,
            ".png" => Tipos.Png,
            ".webp" => Tipos.Webp,
            ".pdf" => Tipos.Pdf,
            _ => null,
        };

    private static void EliminarArchivoSiExiste(string ruta)
    {
        if (File.Exists(ruta))
            File.Delete(ruta);
    }

    private void IntentarEliminarTemporal(string ruta)
    {
        try
        {
            EliminarArchivoSiExiste(ruta);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No fue posible limpiar el archivo temporal procesado.");
        }
    }

    private sealed record TipoComprobante(
        string Extension,
        string ContentType,
        SKEncodedImageFormat? FormatoImagen,
        int Calidad
    );

    private static class Tipos
    {
        public static readonly TipoComprobante Jpeg =
            new(".jpg", "image/jpeg", SKEncodedImageFormat.Jpeg, CalidadImagen);
        public static readonly TipoComprobante Png =
            new(".png", "image/png", SKEncodedImageFormat.Png, 100);
        public static readonly TipoComprobante Webp =
            new(".webp", "image/webp", SKEncodedImageFormat.Webp, CalidadImagen);
        public static readonly TipoComprobante Pdf = new(".pdf", "application/pdf", null, 0);
    }

    private sealed class ArchivoComprobanteException(string message) : Exception(message);
}
