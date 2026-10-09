using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;
using ResellManager.Infrastructure.Storage;
using SkiaSharp;

namespace ResellManager.Tests;

public sealed class ProductoGaleriaTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    public async Task Crear_AdmiteCeroAOchoFotosConUnaPortadaYOrdenEstable(int cantidad)
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        var servicio = Servicio(test.Db, Almacenamiento(carpeta.Ruta));
        using var fotos = new Fotos(cantidad);
        var resultado = await servicio.CrearGaleriaAsync(Input(test), fotos.Streams, cantidad == 0 ? 0 : cantidad - 1);
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        var entidad = await test.Db.Productos.AsNoTracking().Include(x => x.Imagenes).SingleAsync(x => x.Id == resultado.Value!.Id);
        Assert.Equal(cantidad, entidad.Imagenes.Count);
        Assert.Equal(Enumerable.Range(0, cantidad), entidad.Imagenes.OrderBy(x => x.Orden).Select(x => x.Orden));
        Assert.Equal(cantidad == 0 ? 0 : 1, entidad.Imagenes.Count(x => x.RutaRelativa == entidad.ImagenPrincipalRuta));
        var galeria = await servicio.ObtenerGaleriaAsync(entidad.Id);
        Assert.Equal(cantidad, galeria.Value!.Count);
        Assert.Equal(cantidad, Archivos(carpeta.Ruta).Length);
    }

    [Fact]
    public async Task CrearNovena_RechazaTodoAntesDeProcesarOPersistir()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        using var fotos = new Fotos(9);
        var resultado = await Servicio(test.Db, Almacenamiento(carpeta.Ruta)).CrearGaleriaAsync(Input(test), fotos.Streams);
        Assert.False(resultado.IsSuccess);
        Assert.Contains("8", resultado.ErrorMessage);
        Assert.Single(await test.Db.Productos.AsNoTracking().ToListAsync());
        Assert.Empty(await test.Db.ProductoImagenes.AsNoTracking().ToListAsync());
        Assert.All(fotos.Streams, x => Assert.Equal(0, x.Position));
        Assert.Empty(Archivos(carpeta.Ruta));
    }

    [Fact]
    public async Task EditarNovena_RechazaCambioDeDatosYConservaOchoArchivos()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        var servicio = Servicio(test.Db, Almacenamiento(carpeta.Ruta));
        using var fotos = new Fotos(8);
        var creado = await servicio.CrearGaleriaAsync(Input(test), fotos.Streams);
        var existentes = (await servicio.ObtenerGaleriaAsync(creado.Value!.Id)).Value!;
        using var nuevas = new Fotos(1);
        var referencias = existentes.Select(x => new ImagenProductoEdicion(x.Id)).Append(new(NuevaImagenIndice: 0)).ToList();
        var resultado = await servicio.EditarGaleriaAsync(creado.Value.Id, Input(test) with { Nombre = "No persistir" },
            new(referencias), nuevas.Streams);
        Assert.False(resultado.IsSuccess);
        Assert.Equal("Producto galería", (await new ProductoService(test.Db).ObtenerPorIdAsync(creado.Value.Id)).Value!.Nombre);
        Assert.Equal(8, await test.Db.ProductoImagenes.CountAsync());
        Assert.Equal(8, Archivos(carpeta.Ruta).Length);
        Assert.Equal(0, nuevas.Streams[0].Position);
    }

    [Fact]
    public async Task ImagenDosInvalida_RechazaAltaYCompensaLaPrimeraPreparada()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        using var fotos = new Fotos(1);
        using var invalida = new MemoryStream("<svg><script>malicioso</script></svg>"u8.ToArray());
        var resultado = await Servicio(test.Db, Almacenamiento(carpeta.Ruta))
            .CrearGaleriaAsync(Input(test), [fotos.Streams[0], invalida]);
        Assert.False(resultado.IsSuccess);
        Assert.Single(await test.Db.Productos.AsNoTracking().ToListAsync());
        Assert.Empty(await test.Db.ProductoImagenes.ToListAsync());
        Assert.Empty(Archivos(carpeta.Ruta));
    }

    [Fact]
    public async Task ImagenDosInvalida_EditarConservaProductoPortadaYArchivos()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        var servicio = Servicio(test.Db, Almacenamiento(carpeta.Ruta));
        using var inicial = new Fotos(1);
        var creado = await servicio.CrearGaleriaAsync(Input(test), inicial.Streams);
        var portada = Assert.Single((await servicio.ObtenerGaleriaAsync(creado.Value!.Id)).Value!);
        using var nueva = new Fotos(1);
        using var invalida = new MemoryStream("no imagen"u8.ToArray());
        var resultado = await servicio.EditarGaleriaAsync(creado.Value.Id, Input(test) with { Nombre = "No persistir" },
            new([new(portada.Id), new(NuevaImagenIndice: 0), new(NuevaImagenIndice: 1)]),
            [nueva.Streams[0], invalida]);
        Assert.False(resultado.IsSuccess);
        Assert.Equal(creado.Value.ImagenPrincipalRuta, (await new ProductoService(test.Db).ObtenerPorIdAsync(creado.Value.Id)).Value!.ImagenPrincipalRuta);
        Assert.Equal("Producto galería", (await new ProductoService(test.Db).ObtenerPorIdAsync(creado.Value.Id)).Value!.Nombre);
        Assert.Single(await test.Db.ProductoImagenes.ToListAsync());
        Assert.Single(Archivos(carpeta.Ruta));
    }

    [Fact]
    public async Task FalloSegundaConfirmacion_RollbackSinFilasArchivosNiTemporales()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        using var fotos = new Fotos(2);
        var almacenamiento = new FalloConfirmacion(Almacenamiento(carpeta.Ruta), cancelar: false);
        var resultado = await Servicio(test.Db, almacenamiento).CrearGaleriaAsync(Input(test), fotos.Streams);
        Assert.False(resultado.IsSuccess);
        Assert.Single(await test.Db.Productos.AsNoTracking().ToListAsync());
        Assert.Empty(await test.Db.ProductoImagenes.ToListAsync());
        Assert.Empty(Archivos(carpeta.Ruta));
        // La misma unidad de trabajo debe poder guardar posteriormente sin resucitar entidades revertidas.
        using var siguiente = new Fotos(1);
        Assert.True((await Servicio(test.Db, Almacenamiento(carpeta.Ruta)).CrearGaleriaAsync(Input(test), siguiente.Streams)).IsSuccess);
        Assert.Equal(2, await test.Db.Productos.CountAsync());
        Assert.Single(await test.Db.ProductoImagenes.ToListAsync());
    }

    [Fact]
    public async Task CancelacionTrasConfirmarPrimera_CompensaTodosLosArchivos()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        using var fotos = new Fotos(2);
        var almacenamiento = new FalloConfirmacion(Almacenamiento(carpeta.Ruta), cancelar: true);
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Servicio(test.Db, almacenamiento).CrearGaleriaAsync(Input(test), fotos.Streams));
        Assert.Single(await test.Db.Productos.AsNoTracking().ToListAsync());
        Assert.Empty(await test.Db.ProductoImagenes.ToListAsync());
        Assert.Empty(Archivos(carpeta.Ruta));
    }

    [Fact]
    public async Task FalloCommit_RollbackSinPersistenciaParcialNiArchivos()
    {
        await using var test = await TestDatabase.CreateAsync();
        var options = new DbContextOptionsBuilder<ResellManagerDbContext>()
            .UseSqlite(test.Db.Database.GetDbConnection()).AddInterceptors(new CommitFallido()).Options;
        await using var db = new ResellManagerDbContext(options);
        using var carpeta = new CarpetaTemporal();
        using var fotos = new Fotos(2);
        var resultado = await Servicio(db, Almacenamiento(carpeta.Ruta)).CrearGaleriaAsync(Input(test), fotos.Streams);
        Assert.False(resultado.IsSuccess);
        Assert.Single(await db.Productos.AsNoTracking().ToListAsync());
        Assert.Empty(await db.ProductoImagenes.ToListAsync());
        Assert.Empty(Archivos(carpeta.Ruta));
    }

    [Fact]
    public async Task Editar_AgregaOrdenMixtoCambiaPortadaEliminaYConservaCompatibilidad()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        var servicio = Servicio(test.Db, Almacenamiento(carpeta.Ruta));
        using var inicial = new Fotos(3);
        var creado = await servicio.CrearGaleriaAsync(Input(test), inicial.Streams);
        var galeria = (await servicio.ObtenerGaleriaAsync(creado.Value!.Id)).Value!;
        var rutaEliminada = creado.Value.ImagenPrincipalRuta!;
        using var nuevas = new Fotos(1);
        var editado = await servicio.EditarGaleriaAsync(creado.Value.Id, Input(test),
            new([new(galeria[2].Id), new(NuevaImagenIndice: 0), new(galeria[1].Id)], 2), nuevas.Streams);
        Assert.True(editado.IsSuccess, editado.ErrorMessage);
        var final = (await servicio.ObtenerGaleriaAsync(creado.Value.Id)).Value!;
        Assert.Equal(galeria[2].Id, final[0].Id);
        Assert.Equal(galeria[1].Id, final[2].Id);
        Assert.Equal(final[2], Assert.Single(final.Where(x => x.EsPortada)));
        Assert.Equal(3, Archivos(carpeta.Ruta).Length);
        Assert.False(File.Exists(Archivo(carpeta.Ruta, rutaEliminada)));
        var entidad = await test.Db.Productos.AsNoTracking().Include(x => x.Imagenes).SingleAsync(x => x.Id == creado.Value.Id);
        Assert.Equal(entidad.Imagenes.Single(x => x.Id == final[2].Id).RutaRelativa, editado.Value!.ImagenPrincipalRuta);
        var foto = await servicio.AbrirImagenAsync(creado.Value.Id, final[1].Id);
        Assert.True(foto.IsSuccess);
        await foto.Value!.Contenido.DisposeAsync();
        var vacio = await servicio.EditarGaleriaAsync(creado.Value.Id, Input(test), new([]), []);
        Assert.True(vacio.IsSuccess);
        Assert.Null(vacio.Value!.ImagenPrincipalRuta);
        Assert.Empty(await test.Db.ProductoImagenes.ToListAsync());
        Assert.Empty(Archivos(carpeta.Ruta));
    }

    [Theory]
    [InlineData("ajena")]
    [InlineData("repetida")]
    [InlineData("indiceInvalido")]
    [InlineData("portadaInvalida")]
    public async Task ReferenciasInvalidas_RechazaSinAlterarProducto(string caso)
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        var servicio = Servicio(test.Db, Almacenamiento(carpeta.Ruta));
        using var inicial = new Fotos(1);
        var creado = await servicio.CrearGaleriaAsync(Input(test), inicial.Streams);
        var foto = Assert.Single((await servicio.ObtenerGaleriaAsync(creado.Value!.Id)).Value!);
        var edicion = caso switch
        {
            "ajena" => new GaleriaProductoEdicion([new(Guid.NewGuid())]),
            "repetida" => new([new(foto.Id), new(foto.Id)]),
            "indiceInvalido" => new([new(foto.Id), new(NuevaImagenIndice: 2)]),
            _ => new([new(foto.Id)], 1)
        };
        var resultado = await servicio.EditarGaleriaAsync(creado.Value.Id, Input(test) with { Nombre = "No persistir" }, edicion, []);
        Assert.False(resultado.IsSuccess);
        Assert.Equal(creado.Value.ImagenPrincipalRuta, (await new ProductoService(test.Db).ObtenerPorIdAsync(creado.Value.Id)).Value!.ImagenPrincipalRuta);
        Assert.False((await servicio.AbrirImagenAsync(test.Producto.Id, foto.Id)).IsSuccess);
        Assert.Single(Archivos(carpeta.Ruta));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EliminarFoto_NoBorraArchivoAunReferenciadoPorOtroProducto(bool referenciaGaleria)
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        var servicio = Servicio(test.Db, Almacenamiento(carpeta.Ruta));
        using var inicial = new Fotos(1);
        var creado = await servicio.CrearGaleriaAsync(Input(test), inicial.Streams);
        var otro = await test.Db.Productos.FindAsync(test.Producto.Id);
        if (referenciaGaleria)
            test.Db.ProductoImagenes.Add(new ProductoImagen { ProductoId = otro!.Id, RutaRelativa = creado.Value!.ImagenPrincipalRuta!, Orden = 0 });
        else otro!.ImagenPrincipalRuta = creado.Value!.ImagenPrincipalRuta;
        await test.Db.SaveChangesAsync();
        var resultado = await servicio.EditarGaleriaAsync(creado.Value!.Id, Input(test), new([]), []);
        Assert.True(resultado.IsSuccess);
        Assert.True(File.Exists(Archivo(carpeta.Ruta, creado.Value.ImagenPrincipalRuta!)));
    }

    [Fact]
    public async Task ImagenLegada_SiguePortadaYSeIntegraAlAgregarFotoSinDuplicarArchivo()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        var servicio = Servicio(test.Db, Almacenamiento(carpeta.Ruta));
        using var inicial = new Fotos(1);
        var creado = await servicio.CrearAsync(Input(test), inicial.Streams[0]);
        await test.Db.ProductoImagenes.ExecuteDeleteAsync(); // Simula una referencia v1.3 sin backfill.
        test.Db.ChangeTracker.Clear();
        var antigua = Assert.Single((await servicio.ObtenerGaleriaAsync(creado.Value!.Id)).Value!);
        Assert.True(antigua.EsPortada);
        using var nueva = new Fotos(1);
        var resultado = await servicio.EditarGaleriaAsync(creado.Value.Id, Input(test),
            new([new(antigua.Id), new(NuevaImagenIndice: 0)]), nueva.Streams);
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal(creado.Value.ImagenPrincipalRuta, resultado.Value!.ImagenPrincipalRuta);
        Assert.Equal(2, await test.Db.ProductoImagenes.CountAsync());
        Assert.Equal(2, Archivos(carpeta.Ruta).Length);
    }

    [Fact]
    public async Task PipelineGaleria_ConservaDetalleHasta2000SinEstirarYMetodoLegado1200()
    {
        using var carpeta = new CarpetaTemporal();
        var almacenamiento = Almacenamiento(carpeta.Ruta);
        foreach (var (galeria, ancho, esperado) in new[] { (true, 3000, 2000), (false, 3000, 1200), (true, 512, 512) })
        {
            using var original = Fotos.Imagen(ancho, ancho / 2);
            var preparada = galeria ? await almacenamiento.PrepararGaleriaAsync(original) : await almacenamiento.PrepararAsync(original);
            Assert.True(preparada.IsSuccess);
            var confirmada = await almacenamiento.ConfirmarAsync(preparada.Value!, 1);
            Assert.True(confirmada.IsSuccess);
            using var codec = SKCodec.Create(Archivo(carpeta.Ruta, confirmada.Value!.RutaRelativa));
            Assert.Equal(esperado, codec!.Info.Width);
            Assert.Equal(esperado / 2, codec.Info.Height);
            Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        }
    }

    [Fact]
    public async Task FalloDespuesDelCommitSinNuevas_RecuperaResultadoYEliminaArchivoSinReferencia()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        using var fotos = new Fotos(2);
        var creado = await Servicio(test.Db, Almacenamiento(carpeta.Ruta)).CrearGaleriaAsync(Input(test), fotos.Streams);
        var imagenes = (await Servicio(test.Db, Almacenamiento(carpeta.Ruta)).ObtenerGaleriaAsync(creado.Value!.Id)).Value!;
        var options = new DbContextOptionsBuilder<ResellManagerDbContext>()
            .UseSqlite(test.Db.Database.GetDbConnection()).AddInterceptors(new ConfirmacionCommitFallida()).Options;
        await using var db = new ResellManagerDbContext(options);
        var resultado = await Servicio(db, Almacenamiento(carpeta.Ruta)).EditarGaleriaAsync(creado.Value.Id,
            Input(test) with { Nombre = "Persistido antes del error" }, new([new(imagenes[1].Id)]), []);
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal("Persistido antes del error", resultado.Value!.Nombre);
        Assert.Single(await db.ProductoImagenes.ToListAsync());
        Assert.Single(Archivos(carpeta.Ruta));
        Assert.False(File.Exists(Archivo(carpeta.Ruta, creado.Value.ImagenPrincipalRuta!)));
    }

    [Fact]
    public async Task LecturaAdministrativa_RechazaRutaFisicaTraversalODeOtroProducto()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        using var inicial = new Fotos(1);
        var servicio = Servicio(test.Db, Almacenamiento(carpeta.Ruta));
        var creado = await servicio.CrearGaleriaAsync(Input(test), inicial.Streams);
        foreach (var ruta in new[] { creado.Value!.ImagenPrincipalRuta!, "productos/1/../../secreto.webp", Path.Combine(carpeta.Ruta, "secreto.webp") })
        {
            var imagen = new ProductoImagen { ProductoId = test.Producto.Id, RutaRelativa = ruta, Orden = 0 };
            test.Db.ProductoImagenes.Add(imagen);
            await test.Db.SaveChangesAsync();
            Assert.False((await servicio.AbrirImagenAsync(test.Producto.Id, imagen.Id)).IsSuccess);
            test.Db.ProductoImagenes.Remove(imagen);
            await test.Db.SaveChangesAsync();
        }
        Assert.Single(Archivos(carpeta.Ruta));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AltaGaleriaAsistida_CodigoDuplicadoAntesODurantePreparacionNoPersisteNiDejaArchivos(bool durantePreparacion)
    {
        await using var test = await TestDatabase.CreateAsync();
        using var carpeta = new CarpetaTemporal();
        const string codigo = "7501234567890";
        if (!durantePreparacion)
        {
            test.Producto.CodigoBarras = codigo;
            await test.Db.SaveChangesAsync();
        }
        IAlmacenamientoImagenesProducto almacenamiento = Almacenamiento(carpeta.Ruta);
        if (durantePreparacion)
            almacenamiento = new AccionDurantePreparacion(almacenamiento, async () =>
            {
                test.Producto.CodigoBarras = codigo;
                await test.Db.SaveChangesAsync();
            });
        using var fotos = new Fotos(2);
        var resultado = await Servicio(test.Db, almacenamiento).CrearGaleriaAsync(
            Input(test) with { CodigoBarras = codigo }, fotos.Streams, validarCodigoBarras: true);
        Assert.False(resultado.IsSuccess);
        Assert.Contains("El código ya pertenece", resultado.ErrorMessage);
        Assert.Single(await test.Db.Productos.AsNoTracking().ToListAsync());
        Assert.Empty(await test.Db.ProductoImagenes.AsNoTracking().ToListAsync());
        Assert.Empty(Archivos(carpeta.Ruta));
        if (!durantePreparacion) Assert.All(fotos.Streams, x => Assert.Equal(0, x.Position));
    }
    [Fact]
    public async Task FalloCommitYConsultaDeRecuperacion_DevuelveErrorControladoYConservaArchivosSinReferenciaVerificable()
    {
        await using var test = await TestDatabase.CreateAsync();
        var fallo = new EstadoFalloRecuperacion();
        var options = new DbContextOptionsBuilder<ResellManagerDbContext>()
            .UseSqlite(test.Db.Database.GetDbConnection())
            .AddInterceptors(new CommitActivaFalloConsultas(fallo), new ConsultasFallidas(fallo)).Options;
        await using var db = new ResellManagerDbContext(options);
        using var carpeta = new CarpetaTemporal();
        using var fotos = new Fotos(2);
        var resultado = await Servicio(db, Almacenamiento(carpeta.Ruta)).CrearGaleriaAsync(Input(test), fotos.Streams);
        Assert.False(resultado.IsSuccess);
        Assert.Contains("No fue posible guardar", resultado.ErrorMessage);
        // Se preservan los archivos al no poder comprobar un commit ambiguo; el estado real solo se vuelve legible después.
        Assert.Equal(2, Archivos(carpeta.Ruta).Length);
        Assert.DoesNotContain(Archivos(carpeta.Ruta), x => x.Contains(".temporales", StringComparison.Ordinal));
        fallo.FallarConsultas = false;
        Assert.Single(await db.Productos.AsNoTracking().ToListAsync());
        Assert.Empty(await db.ProductoImagenes.AsNoTracking().ToListAsync());
    }
    private static ProductoInput Input(TestDatabase test) =>
        new(null, "Producto galería", null, null, null, null, null, 123m, test.Categoria.Id);

    private static ProductoConImagenService Servicio(ResellManagerDbContext db, IAlmacenamientoImagenesProducto almacenamiento) =>
        new(new ProductoService(db), almacenamiento, db, NullLogger<ProductoConImagenService>.Instance);

    private static AlmacenamientoImagenesProductoLocal Almacenamiento(string ruta) =>
        new(Options.Create(new AlmacenamientoImagenesProductoOptions { DirectorioBase = ruta }),
            NullLogger<AlmacenamientoImagenesProductoLocal>.Instance);

    private static string Archivo(string ruta, string relativa) => Path.Combine(ruta, relativa.Split('/')[1], relativa.Split('/')[2]);
    private static string[] Archivos(string ruta) => Directory.Exists(ruta) ? Directory.GetFiles(ruta, "*", SearchOption.AllDirectories) : [];

    private sealed class Fotos(int cantidad) : IDisposable
    {
        public IReadOnlyList<Stream> Streams { get; } = Enumerable.Range(0, cantidad).Select(_ => (Stream)Imagen()).ToList();
        public static MemoryStream Imagen(int ancho = 32, int alto = 48)
        {
            using var bitmap = new SKBitmap(ancho, alto);
            bitmap.Erase(SKColors.Blue);
            using var imagen = SKImage.FromBitmap(bitmap);
            using var datos = imagen.Encode(SKEncodedImageFormat.Png, 100);
            return new MemoryStream(datos.ToArray());
        }
        public void Dispose() { foreach (var stream in Streams) stream.Dispose(); }
    }

    private sealed class CarpetaTemporal : IDisposable
    {
        public string Ruta { get; } = Path.Combine(Path.GetTempPath(), "resellmanager-galeria-" + Guid.NewGuid().ToString("N"));
        public void Dispose() { if (Directory.Exists(Ruta)) Directory.Delete(Ruta, true); }
    }

    private sealed class EstadoFalloRecuperacion
    {
        public bool FallarConsultas { get; set; }
    }

    private sealed class CommitActivaFalloConsultas(EstadoFalloRecuperacion estado) : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            estado.FallarConsultas = true;
            throw new InvalidOperationException("Fallo de commit y conexión de recuperación simulado.");
        }
    }

    private sealed class ConsultasFallidas(EstadoFalloRecuperacion estado) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (estado.FallarConsultas) throw new InvalidOperationException("Base de datos inaccesible durante recuperación simulada.");
            return new(result);
        }
    }
    private sealed class ConfirmacionCommitFallida : DbTransactionInterceptor
    {
        public override Task TransactionCommittedAsync(
            DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Fallo posterior a commit confirmado simulado.");
    }
    private sealed class CommitFallido : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Fallo de commit simulado.");
    }

    private sealed class AccionDurantePreparacion(IAlmacenamientoImagenesProducto interior, Func<Task> accion) : IAlmacenamientoImagenesProducto
    {
        private bool realizada;
        public Task<ServiceResult<ImagenProductoPreparada>> PrepararAsync(Stream contenido, CancellationToken ct = default) => interior.PrepararAsync(contenido, ct);
        public async Task<ServiceResult<ImagenProductoPreparada>> PrepararGaleriaAsync(Stream contenido, CancellationToken ct = default)
        {
            var resultado = await interior.PrepararGaleriaAsync(contenido, ct);
            if (!realizada && resultado.IsSuccess)
            {
                realizada = true;
                await accion();
            }
            return resultado;
        }
        public Task<ServiceResult<ImagenProductoGuardada>> ConfirmarAsync(ImagenProductoPreparada preparada, int productoId, CancellationToken ct = default)
            => interior.ConfirmarAsync(preparada, productoId, ct);
        public Task<ServiceResult> EliminarTemporalAsync(string identificadorTemporal, CancellationToken ct = default) => interior.EliminarTemporalAsync(identificadorTemporal, ct);
        public Task<ServiceResult> EliminarAsync(string rutaRelativa, CancellationToken ct = default) => interior.EliminarAsync(rutaRelativa, ct);
        public Task<ServiceResult<ImagenProductoLectura>> AbrirLecturaAsync(string rutaRelativa, CancellationToken ct = default) => interior.AbrirLecturaAsync(rutaRelativa, ct);
    }
    private sealed class FalloConfirmacion(IAlmacenamientoImagenesProducto interior, bool cancelar) : IAlmacenamientoImagenesProducto
    {
        private int confirmaciones;
        public Task<ServiceResult<ImagenProductoPreparada>> PrepararAsync(Stream contenido, CancellationToken ct = default) => interior.PrepararAsync(contenido, ct);
        public Task<ServiceResult<ImagenProductoPreparada>> PrepararGaleriaAsync(Stream contenido, CancellationToken ct = default) => interior.PrepararGaleriaAsync(contenido, ct);
        public Task<ServiceResult<ImagenProductoGuardada>> ConfirmarAsync(ImagenProductoPreparada preparada, int productoId, CancellationToken ct = default)
        {
            if (++confirmaciones == 2)
            {
                if (cancelar) throw new OperationCanceledException();
                return Task.FromResult(ServiceResult<ImagenProductoGuardada>.Failure("Fallo de disco simulado."));
            }
            return interior.ConfirmarAsync(preparada, productoId, ct);
        }
        public Task<ServiceResult> EliminarTemporalAsync(string identificadorTemporal, CancellationToken ct = default) => interior.EliminarTemporalAsync(identificadorTemporal, ct);
        public Task<ServiceResult> EliminarAsync(string rutaRelativa, CancellationToken ct = default) => interior.EliminarAsync(rutaRelativa, ct);
        public Task<ServiceResult<ImagenProductoLectura>> AbrirLecturaAsync(string rutaRelativa, CancellationToken ct = default) => interior.AbrirLecturaAsync(rutaRelativa, ct);
    }
}
