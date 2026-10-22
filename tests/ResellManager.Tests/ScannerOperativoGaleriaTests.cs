using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResellManager.Application.DTOs;
using ResellManager.Application.Services;
using ResellManager.Domain.Entities;
using ResellManager.Infrastructure.Services;
using ResellManager.Infrastructure.Storage;
using ResellManager.Web.Components.Productos;
using SkiaSharp;
using static ResellManager.Tests.ProductoLookupTests;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed partial class ScannerOperativoTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Compra_AltaScannerConGaleria_PreservaPortadaSubcategoriaYCompraInclusoAlReintentar(bool falloInicial)
    {
        await using var test = await DatosAsync(0);
        var hija = new Categoria { Nombre = "Hija sintética", CategoriaPadreId = test.Categoria.Id };
        test.Db.Categorias.Add(hija);
        await test.Db.SaveChangesAsync();
        using var fotos = new FotosScanner();
        await using var vista = await Vista.CrearAsync(test, compra: true, almacenamiento: fotos.Almacenamiento);
        var compra = await PrepararCompraAsync(vista, test);
        var antes = compra.ToInput("COM-PRUEBA");
        var comprobante = Get<IBrowserFile>(vista.Componente, "ArchivoSeleccionado");
        var datosComprobante = compra.ToDatosComprobante();
        await AbrirAltaConGaleriaAsync(vista);
        var alta = Get<ProductoFormModel>(vista.Panel, "Modelo");
        alta.CategoriaPrincipalId = test.Categoria.Id;
        alta.SeleccionarCategoriaPrincipal();
        alta.SubcategoriaId = hija.Id;
        alta.SeleccionarSubcategoria();
        await vista.AltaAsync("ElegirPortada", alta.Galeria[1]);
        var seleccion = alta.Galeria.ToArray();
        Assert.StartsWith("data:image/webp;base64,", seleccion[1].VistaPrevia);
        Assert.Null(alta.ImagenExternaUrl);
        if (falloInicial)
        {
            alta.PrecioSugerido = -1m;
            await vista.AltaAsync("GuardarAsync");
            Assert.NotNull(Get<string?>(vista.Panel, "Error"));
            Assert.Equal(seleccion, alta.Galeria);
            Assert.True(alta.Galeria[1].EsPortada);
            Assert.Equal(1, await test.Db.Productos.CountAsync());
            Assert.Empty(await test.Db.ProductoImagenes.ToListAsync());
            Assert.Empty(Directory.GetFiles(fotos.Ruta, "*.webp", SearchOption.AllDirectories));
            CompararCompra(antes, compra.ToInput("COM-PRUEBA"));
        }
        alta.PrecioSugerido = 87m;
        await vista.AltaAsync("GuardarAsync");
        var producto = await test.Db.Productos.AsNoTracking().SingleAsync(x => x.CodigoBarras == CodigoNuevo);
        var galeria = await test.Db.ProductoImagenes.AsNoTracking().Where(x => x.ProductoId == producto.Id)
            .OrderBy(x => x.Orden).ToArrayAsync();
        Assert.Equal(2, galeria.Length);
        Assert.Equal(galeria[1].RutaRelativa, producto.ImagenPrincipalRuta);
        Assert.Equal(hija.Id, producto.CategoriaId);
        Assert.Equal(4, Directory.GetFiles(fotos.Ruta, "*.webp", SearchOption.AllDirectories).Length);
        CompararCompra(antes with { Detalles = antes.Detalles.Select((d, i) => i == 1 ? d with { ProductoId = producto.Id } : d).ToArray() }, compra.ToInput("COM-PRUEBA"));
        Assert.Equal(datosComprobante, compra.ToDatosComprobante());
        Assert.Same(comprobante, Get<IBrowserFile>(vista.Componente, "ArchivoSeleccionado"));
        Assert.Empty(vista.Imagenes.Solicitudes);
        Assert.Empty(await test.Db.Compras.ToListAsync());
        Assert.Empty(await test.Db.UnidadesInventario.ToListAsync());
    }

    [Fact]
    public async Task Compra_AltaScannerConGaleria_RechazaCodigoRegistradoDuranteLaEdicion()
    {
        await using var test = await DatosAsync(0);
        using var fotos = new FotosScanner();
        await using var vista = await Vista.CrearAsync(test, compra: true, almacenamiento: fotos.Almacenamiento);
        var compra = await PrepararCompraAsync(vista, test);
        var antes = compra.ToInput("COM-PRUEBA");
        await AbrirAltaConGaleriaAsync(vista);
        var alta = Get<ProductoFormModel>(vista.Panel, "Modelo");
        alta.CategoriaId = test.Categoria.Id;
        var previo = await new ProductoService(test.Db).CrearAsync(
            new(CodigoNuevo, "Registrado concurrentemente", null, null, null, null, null, 0m, test.Categoria.Id));
        Assert.True(previo.IsSuccess, previo.ErrorMessage);
        await vista.AltaAsync("GuardarAsync");
        Assert.NotNull(Get<string?>(vista.Panel, "Error"));
        Assert.Equal(1, await test.Db.Productos.CountAsync(x => x.CodigoBarras == CodigoNuevo));
        Assert.Empty(await test.Db.ProductoImagenes.ToListAsync());
        Assert.Empty(Directory.GetFiles(fotos.Ruta, "*.webp", SearchOption.AllDirectories));
        Assert.Equal(2, alta.Galeria.Count);
        CompararCompra(antes, compra.ToInput("COM-PRUEBA"));
    }

    [Fact]
    public async Task Compra_CancelarAltaScannerConGaleria_NoPersisteYConservaComprobante()
    {
        await using var test = await DatosAsync(0);
        using var fotos = new FotosScanner();
        await using var vista = await Vista.CrearAsync(test, compra: true, almacenamiento: fotos.Almacenamiento);
        var compra = await PrepararCompraAsync(vista, test);
        var antes = compra.ToInput("COM-PRUEBA");
        var comprobante = Get<IBrowserFile>(vista.Componente, "ArchivoSeleccionado");
        await AbrirAltaConGaleriaAsync(vista);
        await vista.PanelAsync("CancelarAsync");
        CompararCompra(antes, compra.ToInput("COM-PRUEBA"));
        Assert.Same(comprobante, Get<IBrowserFile>(vista.Componente, "ArchivoSeleccionado"));
        Assert.Equal(1, await test.Db.Productos.CountAsync());
        Assert.Empty(await test.Db.ProductoImagenes.ToListAsync());
        Assert.Empty(Directory.GetFiles(fotos.Ruta, "*.webp", SearchOption.AllDirectories));
    }

    private static async Task AbrirAltaConGaleriaAsync(Vista vista)
    {
        vista.Externo.Servicio = new ProductoLookupService(vista.Consulta, [new ProveedorFalso("Sintético",
            new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, new ProductoLookupCandidato
            { CodigoBarras = CodigoNuevo, Nombre = "Producto sintético con galería", ImagenUrl = "https://images.example.com/no-descargar.png" }))]);
        await vista.DetectarCompraAsync(1, CodigoNuevo);
        var compra = Get<ResellManager.Web.Components.Compras.CompraFormModel>(vista.Componente, "Modelo");
        await vista.EjecutarAsync("RegistrarCodigoNoEncontrado", compra.Detalles[1]);
        await vista.AltaAsync("UsarCandidato");
        await vista.AltaAsync("SeleccionarImagenAsync", new InputFileChangeEventArgs(
            [new FotoScanner(SKEncodedImageFormat.Png), new FotoScanner(SKEncodedImageFormat.Webp)]));
    }

    private sealed class FotoScanner : IBrowserFile
    {
        private readonly byte[] contenido;
        public FotoScanner(SKEncodedImageFormat formato)
        {
            using var bitmap = new SKBitmap(16, 24);
            bitmap.Erase(SKColors.Blue);
            using var imagen = SKImage.FromBitmap(bitmap);
            using var datos = imagen.Encode(formato, 90);
            contenido = datos.ToArray();
        }
        public string Name => "foto-sintetica";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => contenido.Length;
        public string ContentType => "application/octet-stream";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new MemoryStream(contenido, writable: false);
    }

    private sealed class FotosScanner : IDisposable
    {
        public string Ruta { get; } = Path.Combine(Path.GetTempPath(), "resellmanager-scanner-galeria-" + Guid.NewGuid().ToString("N"));
        public AlmacenamientoImagenesProductoLocal Almacenamiento { get; }
        public FotosScanner()
        {
            Directory.CreateDirectory(Ruta);
            Almacenamiento = new(Options.Create(new AlmacenamientoImagenesProductoOptions { DirectorioBase = Ruta }),
                NullLogger<AlmacenamientoImagenesProductoLocal>.Instance);
        }
        public void Dispose() => Directory.Delete(Ruta, recursive: true);
    }
}
