using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Infrastructure.Storage;
using SkiaSharp;

namespace ResellManager.Tests;

public sealed class Fase58AlmacenamientoComprobantesTests
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "foto.jpg", "image/jpeg", ".jpg")]
    [InlineData(SKEncodedImageFormat.Jpeg, "foto.jpeg", "image/jpeg", ".jpg")]
    [InlineData(SKEncodedImageFormat.Png, "foto.png", "image/png", ".png")]
    [InlineData(SKEncodedImageFormat.Webp, "foto.webp", "image/webp", ".webp")]
    public async Task ImagenAdmitida_SePreparaYConfirmaConTipoValidado(
        SKEncodedImageFormat formato,
        string nombre,
        string contentType,
        string extensionEsperada
    )
    {
        using var temporal = new DirectorioTemporal();
        var almacenamiento = CrearAlmacenamiento(temporal.Ruta);
        await using var stream = new MemoryStream(CrearImagen(formato, 800, 400));

        var preparado = await almacenamiento.PrepararAsync(
            new ArchivoComprobanteInput(stream, nombre, contentType)
        );
        var guardado = await almacenamiento.ConfirmarAsync(preparado.Value!);

        Assert.True(preparado.IsSuccess, preparado.ErrorMessage);
        Assert.True(guardado.IsSuccess, guardado.ErrorMessage);
        Assert.EndsWith(extensionEsperada, guardado.Value!.NombreArchivo);
        Assert.StartsWith("CMP-", guardado.Value.NombreArchivo);
        Assert.StartsWith("comprobantes/", guardado.Value.RutaRelativa);
        Assert.False(Path.IsPathRooted(guardado.Value.RutaRelativa));
        Assert.True(File.Exists(RutaFisica(temporal.Ruta, guardado.Value.RutaRelativa)));
    }

    [Fact]
    public async Task PdfValido_SeConservaSinModificar()
    {
        using var temporal = new DirectorioTemporal();
        var almacenamiento = CrearAlmacenamiento(temporal.Ruta);
        var contenido = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\n%%EOF");
        await using var stream = new MemoryStream(contenido);

        var preparado = await almacenamiento.PrepararAsync(
            new ArchivoComprobanteInput(stream, "factura.pdf", "application/pdf")
        );
        var guardado = await almacenamiento.ConfirmarAsync(preparado.Value!);
        var lectura = await almacenamiento.AbrirLecturaAsync(guardado.Value!.RutaRelativa);
        await using var archivo = lectura.Value!.Contenido;
        using var copia = new MemoryStream();
        await archivo.CopyToAsync(copia);

        Assert.True(preparado.IsSuccess, preparado.ErrorMessage);
        Assert.True(guardado.IsSuccess, guardado.ErrorMessage);
        Assert.Equal("application/pdf", lectura.Value.ContentType);
        Assert.Equal(contenido, copia.ToArray());
    }

    [Fact]
    public async Task ArchivoMayorA10Mb_SeRechazaConLimiteRealDelStream()
    {
        using var temporal = new DirectorioTemporal();
        var almacenamiento = CrearAlmacenamiento(temporal.Ruta);
        await using var stream = new MemoryStream(
            new byte[ReglasComprobanteCompra.TamanoMaximoBytes + 1]
        );

        var resultado = await almacenamiento.PrepararAsync(
            new ArchivoComprobanteInput(stream, "grande.pdf", "application/pdf")
        );

        Assert.False(resultado.IsSuccess);
        Assert.Contains("10 MB", resultado.ErrorMessage);
        Assert.Empty(Archivos(temporal.Ruta));
    }

    [Fact]
    public async Task ExtensionPermitidaConContenidoInvalido_SeRechazaYLimpia()
    {
        using var temporal = new DirectorioTemporal();
        var almacenamiento = CrearAlmacenamiento(temporal.Ruta);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("no es una imagen"));

        var resultado = await almacenamiento.PrepararAsync(
            new ArchivoComprobanteInput(stream, "aparente.jpg", "image/jpeg")
        );

        Assert.False(resultado.IsSuccess);
        Assert.Empty(Archivos(temporal.Ruta));
    }

    [Fact]
    public async Task NombreOriginalMalicioso_NoControlaNombreNiRuta()
    {
        using var temporal = new DirectorioTemporal();
        var almacenamiento = CrearAlmacenamiento(temporal.Ruta);
        await using var stream = new MemoryStream(
            Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF")
        );

        var preparado = await almacenamiento.PrepararAsync(
            new ArchivoComprobanteInput(
                stream,
                "../../factura-del-cliente.pdf",
                "application/pdf"
            )
        );

        Assert.True(preparado.IsSuccess, preparado.ErrorMessage);
        Assert.DoesNotContain("factura-del-cliente", preparado.Value!.RutaRelativa);
        Assert.DoesNotContain("..", preparado.Value.RutaRelativa);
        Assert.Matches("^comprobantes/CMP-[A-F0-9]{32}\\.pdf$", preparado.Value.RutaRelativa);
    }

    [Fact]
    public async Task PathTraversal_SeRechazaEnLecturaYEliminacion()
    {
        using var temporal = new DirectorioTemporal();
        var almacenamiento = CrearAlmacenamiento(temporal.Ruta);

        var lectura = await almacenamiento.AbrirLecturaAsync("../secreto.pdf");
        var eliminacion = await almacenamiento.EliminarAsync("../secreto.pdf");

        Assert.False(lectura.IsSuccess);
        Assert.False(eliminacion.IsSuccess);
    }

    [Fact]
    public async Task ImagenGrande_SeReduceYConservaRelacionDeAspecto()
    {
        using var temporal = new DirectorioTemporal();
        var almacenamiento = CrearAlmacenamiento(temporal.Ruta);
        await using var stream = new MemoryStream(
            CrearImagen(SKEncodedImageFormat.Jpeg, 2400, 1200)
        );

        var preparado = await almacenamiento.PrepararAsync(
            new ArchivoComprobanteInput(stream, "grande.jpg", "image/jpeg")
        );
        var guardado = await almacenamiento.ConfirmarAsync(preparado.Value!);
        using var imagen = SKBitmap.Decode(RutaFisica(temporal.Ruta, guardado.Value!.RutaRelativa));

        Assert.NotNull(imagen);
        Assert.Equal(1800, imagen.Width);
        Assert.Equal(900, imagen.Height);
        Assert.Equal(2d, imagen.Width / (double)imagen.Height, 2);
    }

    [Fact]
    public async Task ImagenPequena_NoSeAmplia()
    {
        using var temporal = new DirectorioTemporal();
        var almacenamiento = CrearAlmacenamiento(temporal.Ruta);
        await using var stream = new MemoryStream(
            CrearImagen(SKEncodedImageFormat.Png, 640, 320)
        );

        var preparado = await almacenamiento.PrepararAsync(
            new ArchivoComprobanteInput(stream, "pequena.png", "image/png")
        );
        var guardado = await almacenamiento.ConfirmarAsync(preparado.Value!);
        using var imagen = SKBitmap.Decode(RutaFisica(temporal.Ruta, guardado.Value!.RutaRelativa));

        Assert.NotNull(imagen);
        Assert.Equal(640, imagen.Width);
        Assert.Equal(320, imagen.Height);
    }

    [Fact]
    public async Task CompraSinComprobante_EsValidaPorOrquestador()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var temporal = new DirectorioTemporal();
        var almacenamiento = CrearAlmacenamiento(temporal.Ruta);
        var orquestador = new RegistroCompraConComprobanteService(
            new CompraService(test.Db),
            almacenamiento,
            NullLogger<RegistroCompraConComprobanteService>.Instance
        );

        var resultado = await orquestador.RegistrarAsync(
            test.Compra(OrigenCompra.Catalogo, CodigosInternos.CrearCodigoCompra()),
            null,
            null
        );

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Null(resultado.Value!.RutaComprobante);
        Assert.Empty(Archivos(temporal.Ruta));
    }

    [Fact]
    public async Task CompraConJpg_GuardaRutaRelativaFueraDeSqlite()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var temporal = new DirectorioTemporal();
        var almacenamiento = CrearAlmacenamiento(temporal.Ruta);
        var orquestador = new RegistroCompraConComprobanteService(
            new CompraService(test.Db),
            almacenamiento,
            NullLogger<RegistroCompraConComprobanteService>.Instance
        );
        await using var stream = new MemoryStream(
            CrearImagen(SKEncodedImageFormat.Jpeg, 1200, 800)
        );

        var resultado = await orquestador.RegistrarAsync(
            test.Compra(
                OrigenCompra.CompraLocal,
                CodigosInternos.CrearCodigoCompra(),
                new DateOnly(2026, 9, 1)
            ),
            new DatosComprobanteCompraInput(
                "FAC-192827",
                new DateOnly(2026, 9, 1),
                "Foto legible"
            ),
            new ArchivoComprobanteInput(stream, "IMG_7922.jpg", "image/jpeg")
        );
        var entidad = await test.Db.ComprobantesCompra.SingleAsync();

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal("FAC-192827", entidad.NumeroDocumento);
        Assert.False(Path.IsPathRooted(entidad.RutaDocumento));
        Assert.Matches("^comprobantes/CMP-[A-F0-9]{32}\\.jpg$", entidad.RutaDocumento);
        Assert.True(File.Exists(RutaFisica(temporal.Ruta, entidad.RutaDocumento)));
        Assert.DoesNotContain(
            typeof(ComprobanteCompra).GetProperties(),
            propiedad => propiedad.PropertyType == typeof(byte[])
        );
    }

    [Fact]
    public async Task FalloDeCompra_LimpiaArchivoConfirmado()
    {
        using var temporal = new DirectorioTemporal();
        var almacenamiento = CrearAlmacenamiento(temporal.Ruta);
        var compraFallida = new CompraControladaService(exito: false);
        var orquestador = new RegistroCompraConComprobanteService(
            compraFallida,
            almacenamiento,
            NullLogger<RegistroCompraConComprobanteService>.Instance
        );
        await using var stream = new MemoryStream(
            Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF")
        );

        var resultado = await orquestador.RegistrarAsync(
            CompraDummy(),
            new DatosComprobanteCompraInput(null, new DateOnly(2026, 9, 1), null),
            new ArchivoComprobanteInput(stream, "factura.pdf", "application/pdf")
        );

        Assert.False(resultado.IsSuccess);
        Assert.Equal(1, compraFallida.Intentos);
        Assert.Empty(Archivos(temporal.Ruta));
    }

    [Fact]
    public async Task FalloDeProcesamiento_NoCreaCompraNiDejaHuerfanos()
    {
        using var temporal = new DirectorioTemporal();
        var almacenamiento = CrearAlmacenamiento(temporal.Ruta);
        var compra = new CompraControladaService(exito: true);
        var orquestador = new RegistroCompraConComprobanteService(
            compra,
            almacenamiento,
            NullLogger<RegistroCompraConComprobanteService>.Instance
        );
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("contenido inválido"));

        var resultado = await orquestador.RegistrarAsync(
            CompraDummy(),
            new DatosComprobanteCompraInput(null, new DateOnly(2026, 9, 1), null),
            new ArchivoComprobanteInput(stream, "falsa.png", "image/png")
        );

        Assert.False(resultado.IsSuccess);
        Assert.Equal(0, compra.Intentos);
        Assert.Empty(Archivos(temporal.Ruta));
    }

    [Fact]
    public async Task ExcepcionPosteriorAlCommit_RecuperaCompraYConservaComprobante()
    {
        using var temporal = new DirectorioTemporal();
        var almacenamiento = CrearAlmacenamiento(temporal.Ruta);
        var compra = new CompraPersistidaConExcepcionService();
        var orquestador = new RegistroCompraConComprobanteService(
            compra,
            almacenamiento,
            NullLogger<RegistroCompraConComprobanteService>.Instance
        );
        await using var stream = new MemoryStream(
            Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF")
        );

        var resultado = await orquestador.RegistrarAsync(
            CompraDummy(),
            new DatosComprobanteCompraInput(null, new DateOnly(2026, 9, 1), null),
            new ArchivoComprobanteInput(stream, "factura.pdf", "application/pdf")
        );

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.NotNull(resultado.Value!.RutaComprobante);
        Assert.Single(Archivos(temporal.Ruta));
    }

    [Fact]
    public async Task FalloDeConfirmacion_LimpiaTemporalYNoCreaCompra()
    {
        var almacenamiento = new AlmacenamientoConfirmacionFallida();
        var compra = new CompraControladaService(exito: true);
        var orquestador = new RegistroCompraConComprobanteService(
            compra,
            almacenamiento,
            NullLogger<RegistroCompraConComprobanteService>.Instance
        );
        await using var stream = new MemoryStream(
            Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF")
        );

        var resultado = await orquestador.RegistrarAsync(
            CompraDummy(),
            new DatosComprobanteCompraInput(null, new DateOnly(2026, 9, 1), null),
            new ArchivoComprobanteInput(stream, "factura.pdf", "application/pdf")
        );

        Assert.False(resultado.IsSuccess);
        Assert.Equal(0, compra.Intentos);
        Assert.True(almacenamiento.TemporalEliminado);
    }

    [Fact]
    public async Task BaseDeDatos_ImpideSegundoComprobanteParaLaMismaCompra()
    {
        await using var test = await TestDatabase.CreateAsync();
        var input = test.Compra(
            OrigenCompra.Catalogo,
            CodigosInternos.CrearCodigoCompra()
        ) with
        {
            Comprobante = new ComprobanteCompraInput(
                "FAC-1",
                new DateOnly(2026, 9, 1),
                $"comprobantes/CMP-{Guid.NewGuid():N}.pdf".ToUpperInvariant(),
                null
            ),
        };
        var compra = await new CompraService(test.Db).RegistrarAsync(input);
        test.Db.ChangeTracker.Clear();
        test.Db.ComprobantesCompra.Add(
            new ComprobanteCompra
            {
                CompraId = compra.Value!.Id,
                Fecha = new DateOnly(2026, 9, 1),
                RutaDocumento = $"comprobantes/CMP-{Guid.NewGuid():N}.pdf".ToUpperInvariant(),
            }
        );

        await Assert.ThrowsAsync<DbUpdateException>(() => test.Db.SaveChangesAsync());
    }

    private static AlmacenamientoComprobantesLocal CrearAlmacenamiento(string ruta) =>
        new(
            Options.Create(new AlmacenamientoComprobantesOptions { DirectorioBase = ruta }),
            NullLogger<AlmacenamientoComprobantesLocal>.Instance
        );

    private static byte[] CrearImagen(SKEncodedImageFormat formato, int ancho, int alto)
    {
        using var bitmap = new SKBitmap(ancho, alto);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(248, 250, 252));
        using var paint = new SKPaint { Color = new SKColor(15, 118, 110), StrokeWidth = 12 };
        canvas.DrawLine(20, 20, ancho - 20, alto - 20, paint);
        using var imagen = SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(formato, 90);
        return datos.ToArray();
    }

    private static string RutaFisica(string raiz, string relativa) =>
        Path.Combine(raiz, relativa.Replace('/', Path.DirectorySeparatorChar));

    private static string[] Archivos(string raiz) =>
        Directory.Exists(raiz)
            ? Directory.GetFiles(raiz, "*", SearchOption.AllDirectories)
            : [];

    private static CompraInput CompraDummy() =>
        new(
            CodigosInternos.CrearCodigoCompra(),
            new DateOnly(2026, 9, 1),
            null,
            OrigenCompra.Catalogo,
            1,
            null,
            [new DetalleCompraInput(1, 1, 10m)],
            null
        );

    private sealed class DirectorioTemporal : IDisposable
    {
        public DirectorioTemporal()
        {
            Ruta = Path.Combine(
                Path.GetTempPath(),
                $"resellmanager-comprobantes-{Guid.NewGuid():N}"
            );
            Directory.CreateDirectory(Ruta);
        }

        public string Ruta { get; }

        public void Dispose()
        {
            if (Directory.Exists(Ruta))
                Directory.Delete(Ruta, recursive: true);
        }
    }

    private sealed class CompraControladaService(bool exito) : ICompraService
    {
        public int Intentos { get; private set; }

        public Task<ServiceResult<CompraDto>> RegistrarAsync(
            CompraInput input,
            CancellationToken ct = default
        )
        {
            Intentos++;
            return Task.FromResult(
                exito
                    ? ServiceResult<CompraDto>.Ok(
                        new CompraDto(
                            1,
                            input.CodigoInterno,
                            input.FechaCompra,
                            input.Origen,
                            input.Detalles.Sum(x => x.Cantidad * x.CostoUnitario),
                            input.Observaciones,
                            input.ProveedorId,
                            "Proveedor",
                            [],
                            input.Comprobante?.RutaDocumento
                        )
                    )
                    : ServiceResult<CompraDto>.Failure("Fallo controlado de compra.")
            );
        }

        public Task<ServiceResult<CompraDto>> ObtenerPorIdAsync(
            int id,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<IReadOnlyList<CompraDto>> ListarAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<ServiceResult<ComprobanteCompraDto>> ObtenerComprobanteAsync(
            int compraId,
            CancellationToken ct = default
        ) => throw new NotSupportedException();
    }

    private sealed class AlmacenamientoConfirmacionFallida : IAlmacenamientoComprobantes
    {
        public bool TemporalEliminado { get; private set; }

        public Task<ServiceResult<ComprobantePreparadoDto>> PrepararAsync(
            ArchivoComprobanteInput archivo,
            CancellationToken ct = default
        ) =>
            Task.FromResult(
                ServiceResult<ComprobantePreparadoDto>.Ok(
                    new ComprobantePreparadoDto(
                        $"TMP-{Guid.NewGuid():N}.pdf".ToUpperInvariant(),
                        $"comprobantes/CMP-{Guid.NewGuid():N}.pdf".ToUpperInvariant(),
                        $"CMP-{Guid.NewGuid():N}.pdf".ToUpperInvariant(),
                        "application/pdf",
                        10
                    )
                )
            );

        public Task<ServiceResult<ComprobanteGuardadoDto>> ConfirmarAsync(
            ComprobantePreparadoDto archivo,
            CancellationToken ct = default
        ) =>
            Task.FromResult(
                ServiceResult<ComprobanteGuardadoDto>.Failure("Fallo controlado de confirmación.")
            );

        public Task<ServiceResult> EliminarTemporalAsync(
            string identificadorTemporal,
            CancellationToken ct = default
        )
        {
            TemporalEliminado = true;
            return Task.FromResult(ServiceResult.Ok());
        }

        public Task<ServiceResult> EliminarAsync(
            string rutaRelativa,
            CancellationToken ct = default
        ) => Task.FromResult(ServiceResult.Ok());

        public Task<ServiceResult<ArchivoComprobanteLecturaDto>> AbrirLecturaAsync(
            string rutaRelativa,
            CancellationToken ct = default
        ) => throw new NotSupportedException();
    }

    private sealed class CompraPersistidaConExcepcionService : ICompraService
    {
        private CompraDto? compra;

        public Task<ServiceResult<CompraDto>> RegistrarAsync(
            CompraInput input,
            CancellationToken ct = default
        )
        {
            compra = new CompraDto(
                1,
                input.CodigoInterno,
                input.FechaCompra,
                input.Origen,
                input.Detalles.Sum(x => x.Cantidad * x.CostoUnitario),
                input.Observaciones,
                input.ProveedorId,
                "Proveedor",
                [],
                input.Comprobante?.RutaDocumento
            );
            throw new InvalidOperationException("Excepción simulada después del commit.");
        }

        public Task<IReadOnlyList<CompraDto>> ListarAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CompraDto>>(compra is null ? [] : [compra]);

        public Task<ServiceResult<CompraDto>> ObtenerPorIdAsync(
            int id,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<ServiceResult<ComprobanteCompraDto>> ObtenerComprobanteAsync(
            int compraId,
            CancellationToken ct = default
        ) => throw new NotSupportedException();
    }
}
