using Microsoft.EntityFrameworkCore;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

public sealed class CategoriaJerarquiaTests
{
    [Fact]
    public async Task CrearEditar_ConservaRaicesYPermiteCambiarHijaEntreRaices()
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new CategoriaService(test.Db);
        var otraRaiz = await servicio.CrearAsync(new CategoriaInput("Salud", null));
        var hija = await servicio.CrearAsync(new CategoriaInput("Vitaminas", null, test.Categoria.Id));
        Assert.True(hija.IsSuccess, hija.ErrorMessage);
        Assert.Equal(test.Categoria.Id, hija.Value!.CategoriaPadreId);
        Assert.Equal("General", hija.Value.CategoriaPadre);
        Assert.True((await servicio.ObtenerPorIdAsync(test.Categoria.Id)).Value!.TieneSubcategorias);
        Assert.Null((await servicio.ObtenerPorIdAsync(test.Categoria.Id)).Value!.CategoriaPadreId);
        var cambio = await servicio.EditarAsync(hija.Value.Id, new CategoriaInput("Vitaminas", null, otraRaiz.Value!.Id));
        Assert.True(cambio.IsSuccess, cambio.ErrorMessage);
        Assert.Equal("Salud", cambio.Value!.CategoriaPadre);
        Assert.False((await servicio.ObtenerPorIdAsync(test.Categoria.Id)).Value!.TieneSubcategorias);
        Assert.Equal(test.Categoria.Id, (await new ProductoService(test.Db).ObtenerPorIdAsync(test.Producto.Id)).Value!.CategoriaId);
        var promovida = await servicio.EditarAsync(hija.Value.Id, new CategoriaInput("Vitaminas", null));
        Assert.True(promovida.IsSuccess, promovida.ErrorMessage);
        Assert.Null(promovida.Value!.CategoriaPadreId);
    }

    [Fact]
    public async Task PadreInexistenteAutorreferenciaNietosYCiclos_SeRechazanSinCambios()
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new CategoriaService(test.Db);
        Assert.False((await servicio.CrearAsync(new CategoriaInput("Inválida", null, int.MaxValue))).IsSuccess);
        Assert.False((await servicio.EditarAsync(test.Categoria.Id,
            new CategoriaInput("No persistir", null, test.Categoria.Id))).IsSuccess);
        var hija = (await servicio.CrearAsync(new CategoriaInput("Hija", null, test.Categoria.Id))).Value!;
        Assert.False((await servicio.CrearAsync(new CategoriaInput("Nieta", null, hija.Id))).IsSuccess);
        Assert.False((await servicio.EditarAsync(test.Categoria.Id,
            new CategoriaInput("No persistir", null, hija.Id))).IsSuccess);
        var otraRaiz = (await servicio.CrearAsync(new CategoriaInput("Otra", null))).Value!;
        Assert.False((await servicio.EditarAsync(test.Categoria.Id,
            new CategoriaInput("No persistir", null, otraRaiz.Id))).IsSuccess);
        Assert.Equal("General", (await servicio.ObtenerPorIdAsync(test.Categoria.Id)).Value!.Nombre);
        Assert.Null((await servicio.ObtenerPorIdAsync(test.Categoria.Id)).Value!.CategoriaPadreId);
        Assert.Equal(3, await test.Db.Categorias.CountAsync());
    }

    [Fact]
    public async Task EliminacionDePadreConHijas_ConservaPoliticaRestrictivaEnSqlite()
    {
        await using var test = await TestDatabase.CreateAsync();
        var raiz = new Categoria { Nombre = "Raíz sin productos" };
        test.Db.Categorias.Add(raiz);
        await test.Db.SaveChangesAsync();
        test.Db.Categorias.Add(new Categoria { Nombre = "Hija", CategoriaPadreId = raiz.Id });
        await test.Db.SaveChangesAsync();
        test.Db.ChangeTracker.Clear();
        var padre = await test.Db.Categorias.SingleAsync(x => x.Id == raiz.Id);
        test.Db.Categorias.Remove(padre);
        await Assert.ThrowsAsync<DbUpdateException>(() => test.Db.SaveChangesAsync());
        test.Db.ChangeTracker.Clear();
        Assert.True(await test.Db.Categorias.AnyAsync(x => x.Id == raiz.Id));
        Assert.True(await test.Db.Categorias.AnyAsync(x => x.CategoriaPadreId == raiz.Id));
    }
}
