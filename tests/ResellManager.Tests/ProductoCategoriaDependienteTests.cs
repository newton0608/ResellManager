using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Productos;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed class ProductoCategoriaDependienteTests
{
    [Fact]
    public async Task CrearYEditar_RaizOHijaPersistenSoloCategoriaFinalConContextoValidado()
    {
        await using var test = await TestDatabase.CreateAsync();
        var hija = await Hija(test, test.Categoria.Id);
        var servicio = new ProductoService(test.Db);
        var raiz = await servicio.CrearAsync(Input(test.Categoria.Id, test.Categoria.Id));
        Assert.True(raiz.IsSuccess, raiz.ErrorMessage);
        Assert.Equal(test.Categoria.Id, raiz.Value!.CategoriaId);
        var editado = await servicio.EditarAsync(raiz.Value.Id, Input(hija.Id, test.Categoria.Id));
        Assert.True(editado.IsSuccess, editado.ErrorMessage);
        Assert.Equal(hija.Id, editado.Value!.CategoriaId);
        var sinHija = await servicio.EditarAsync(raiz.Value.Id, Input(test.Categoria.Id, test.Categoria.Id));
        Assert.True(sinHija.IsSuccess, sinHija.ErrorMessage);
        Assert.Equal(test.Categoria.Id, sinHija.Value!.CategoriaId);
        Assert.Equal(test.Categoria.Id, (await test.Db.Productos.AsNoTracking().SingleAsync(x => x.Id == raiz.Value.Id)).CategoriaId);
    }

    [Fact]
    public async Task CrearYEditar_RechazanOtraRaizPrincipalHijaEIdsInexistentesSinPersistir()
    {
        await using var test = await TestDatabase.CreateAsync();
        var hija = await Hija(test, test.Categoria.Id);
        var otra = new Categoria { Nombre = "Otra raíz" };
        test.Db.Categorias.Add(otra);
        await test.Db.SaveChangesAsync();
        var servicio = new ProductoService(test.Db);
        var invalidos = new[] { Input(hija.Id, otra.Id), Input(hija.Id, hija.Id),
            Input(hija.Id, int.MaxValue), Input(int.MaxValue, test.Categoria.Id),
            Input(test.Categoria.Id, 0) };
        foreach (var input in invalidos)
        {
            var alta = await servicio.CrearAsync(input);
            Assert.False(alta.IsSuccess);
            var edicion = await servicio.EditarAsync(test.Producto.Id, input with { Nombre = "No persistir" });
            Assert.False(edicion.IsSuccess);
            Assert.Equal(test.Categoria.Id, (await servicio.ObtenerPorIdAsync(test.Producto.Id)).Value!.CategoriaId);
        }
        Assert.Single(await test.Db.Productos.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Crear_ContratoAnteriorSinContextoAdmiteHijaValidaPeroRechazaTresNiveles()
    {
        await using var test = await TestDatabase.CreateAsync();
        var hija = await Hija(test, test.Categoria.Id);
        var servicio = new ProductoService(test.Db);
        var anterior = await servicio.CrearAsync(Input(hija.Id));
        Assert.True(anterior.IsSuccess, anterior.ErrorMessage);
        var nieta = await Hija(test, hija.Id); // Simula datos malformados; no pasa por el servicio de categorías.
        var rechazado = await servicio.CrearAsync(Input(nieta.Id));
        Assert.False(rechazado.IsSuccess);
        Assert.Contains("raíz", rechazado.ErrorMessage);
        Assert.Equal(2, await test.Db.Productos.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AltaDesdeCompra_PersisteRaizOHijaConElFormularioReutilizado(bool usarHija)
    {
        await using var test = await TestDatabase.CreateAsync();
        var hija = await Hija(test, test.Categoria.Id);
        var modelo = new ProductoFormModel { Nombre = "Alta dependiente", CategoriaId = usarHija ? hija.Id : test.Categoria.Id };
        modelo.PrepararSeleccionCategoria(await new CategoriaService(test.Db).ListarAsync());
        var panel = new ProductoAltaPanel();
        Set(panel, "ProductoService", new ProductoService(test.Db));
        Set(panel, "CategoriaService", new CategoriaService(test.Db));
        Set(panel, "Logger", NullLogger<ProductoAltaPanel>.Instance);
        await (Task)Call(panel, "CargarCategoriasAsync")!;
        await (Task)Call(panel, "GuardarAsync", modelo)!;
        var creado = await test.Db.Productos.AsNoTracking().SingleAsync(x => x.Nombre == "Alta dependiente");
        Assert.Equal(usarHija ? hija.Id : test.Categoria.Id, creado.CategoriaId);
        Assert.Null(Get<string?>(panel, "Error"));
    }

    private static ProductoInput Input(int categoriaId, int? principalId = null) =>
        new(null, "Producto dependiente", null, null, null, null, null, 0, categoriaId, CategoriaPrincipalId: principalId);

    private static async Task<Categoria> Hija(TestDatabase test, int padreId)
    {
        var hija = new Categoria { Nombre = "Hija de prueba", CategoriaPadreId = padreId };
        test.Db.Categorias.Add(hija);
        await test.Db.SaveChangesAsync();
        return hija;
    }
}
