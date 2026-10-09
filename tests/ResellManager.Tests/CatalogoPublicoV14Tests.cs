using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Infrastructure.Services;
using ResellManager.Infrastructure.Storage;

namespace ResellManager.Tests;

public sealed class CatalogoPublicoV14Tests
{
    [Fact]
    public async Task CategoriaRaizIncluyeHijas_YFiltrosMarcaTerminoSeCombinanConAnd()
    {
        await using var test = await TestDatabase.CreateAsync();
        var categoria = (await new CategoriaService(test.Db).CrearAsync(
            new CategoriaInput("Vitaminas", null, test.Categoria.Id))).Value!;
        var hija = await test.CrearProductoAsync("PRO-HIJA");
        hija.CategoriaId = categoria.Id;
        hija.Nombre = "Vitamina C";
        hija.Marca = "  ÁCME  ";
        test.Producto.Nombre = "Vitamina raíz";
        test.Producto.Marca = "Otra";
        var noPublicable = await test.CrearProductoAsync("PRO-PRIVADO");
        noPublicable.Marca = "Marca privada";
        await test.Db.SaveChangesAsync();
        await test.CrearUnidadDisponibleAsync("COM-RAIZ");
        await test.CrearUnidadDisponibleAsync("COM-HIJA", hija);
        var servicio = Servicio(test);
        Assert.Equal(2, (await servicio.ListarAsync(categoriaId: test.Categoria.Id)).Count);
        var porHija = Assert.Single(await servicio.ListarAsync(categoriaId: categoria.Id));
        Assert.Equal(hija.Id, porHija.Id);
        Assert.Equal(test.Categoria.Id, porHija.CategoriaPadreId);
        Assert.Equal("General", porHija.CategoriaPadreNombre);
        Assert.Equal("  ÁCME  ", porHija.Marca);
        var combinados = await servicio.ListarAsync("vitamina", test.Categoria.Id, marca: " ácme ");
        Assert.Equal(hija.Id, Assert.Single(combinados).Id);
        Assert.Empty(await servicio.ListarAsync("raíz", test.Categoria.Id, marca: "ÁCME"));
        Assert.Empty(await servicio.ListarAsync(categoriaId: int.MaxValue));
        Assert.Empty(await servicio.ListarAsync(marca: "Marca privada"));
        Assert.Empty(await servicio.ListarAsync(marca: "ÁCM"));
    }

    [Fact]
    public async Task GaleriaExponeSoloMetadatosOrdenados_YOcultaFotosDeProductosNoPublicables()
    {
        await using var test = await TestDatabase.CreateAsync();
        test.Producto.ImagenPrincipalRuta = $"productos/{test.Producto.Id}/imagen-principal-{Guid.NewGuid():N}.webp";
        var portada = new ProductoImagen { Id = Guid.NewGuid(), ProductoId = test.Producto.Id,
            RutaRelativa = test.Producto.ImagenPrincipalRuta, Orden = 1 };
        var reverso = new ProductoImagen { Id = Guid.NewGuid(), ProductoId = test.Producto.Id,
            RutaRelativa = $"productos/{test.Producto.Id}/imagen-principal-{Guid.NewGuid():N}.webp", Orden = 0 };
        test.Db.ProductoImagenes.AddRange(portada, reverso);
        await test.Db.SaveChangesAsync();
        Assert.False((await Servicio(test).ObtenerPorIdAsync(test.Producto.Id)).IsSuccess);
        await test.CrearUnidadDisponibleAsync("COM-GALERIA");
        var imagenes = (await Servicio(test).ObtenerPorIdAsync(test.Producto.Id)).Value!.Imagenes!;
        Assert.Equal(new[] { reverso.Id.ToString("N"), portada.Id.ToString("N") }, imagenes.Select(i => i.Id));
        Assert.False(imagenes[0].EsPortada);
        Assert.True(imagenes[1].EsPortada);
        Assert.False((await Servicio(test).AbrirImagenAsync(test.Producto.Id, "../secreto")).IsSuccess);
        Assert.False((await Servicio(test).AbrirImagenAsync(int.MaxValue, portada.Id.ToString("N"))).IsSuccess);
    }

    private static CatalogoPublicoService Servicio(TestDatabase test) => new(test.Db,
        new AlmacenamientoImagenesProductoLocal(Options.Create(new AlmacenamientoImagenesProductoOptions
        {
            DirectorioBase = Path.Combine(Path.GetTempPath(), "resellmanager-v14-no-files")
        }), NullLogger<AlmacenamientoImagenesProductoLocal>.Instance));
}
