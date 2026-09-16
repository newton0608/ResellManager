using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Pages;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed class CatalogosConsultaTests
{
    [Fact]
    public async Task Producto_CategoriaSeCombinaConBusquedaYLimpiarRestauraTodos()
    {
        await using var test = await TestDatabase.CreateAsync();
        var categoria = new Categoria { Nombre = "Perfumes" };
        test.Db.Categorias.Add(categoria);
        await test.Db.SaveChangesAsync();
        var perfume = await test.CrearProductoAsync("PER-1");
        perfume.Nombre = "Versace perfume";
        perfume.CategoriaId = categoria.Id;
        test.Producto.Nombre = "Versace accesorio";
        await test.Db.SaveChangesAsync();
        var servicio = new ProductoService(test.Db);
        Assert.Equal(2, (await servicio.BuscarAsync("", categoriaId: null)).Count);
        Assert.Equal(perfume.Id, Assert.Single(await servicio.BuscarAsync("", categoriaId: categoria.Id)).Id);
        Assert.Equal(perfume.Id, Assert.Single(await servicio.BuscarAsync("VERSACE", categoriaId: categoria.Id)).Id);
        Assert.Empty(await servicio.BuscarAsync("accesorio", categoriaId: categoria.Id));
        var pagina = new Productos();
        Set(pagina, "ProductoService", servicio);
        Set(pagina, "CategoriaService", new CategoriaService(test.Db));
        Set(pagina, "Logger", NullLogger<Productos>.Instance);
        Set(pagina, "Termino", "Versace");
        Set(pagina, "CategoriaId", categoria.Id);
        await CallAsync(pagina, "BuscarAsync");
        Assert.Single(Get<IReadOnlyList<ProductoDto>>(pagina, "ProductosEncontrados"));
        await CallAsync(pagina, "LimpiarBusquedaAsync");
        Assert.Equal(2, Get<IReadOnlyList<ProductoDto>>(pagina, "ProductosEncontrados").Count);
        Assert.Null(Get<int?>(pagina, "CategoriaId"));
        Assert.False(Get<bool>(pagina, "BusquedaActiva"));
    }

    [Theory]
    [InlineData("distribuidora", 1)]
    [InlineData("DISTRIBUIDORA", 1)]
    [InlineData("12345678", 1)]
    [InlineData("inexistente", 0)]
    public async Task Proveedor_ReutilizaBusquedaNombreTelefono(string termino, int cantidad)
    {
        await using var test = await TestDatabase.CreateAsync();
        test.Proveedor.Nombre = "Distribuidora Central";
        test.Proveedor.Telefono = "12345678";
        await test.Db.SaveChangesAsync();
        var pagina = new Proveedores();
        Set(pagina, "ProveedorService", new ProveedorService(test.Db));
        Set(pagina, "Logger", NullLogger<Proveedores>.Instance);
        Set(pagina, "Termino", termino);
        await CallAsync(pagina, "CargarAsync");
        Assert.Equal(cantidad, Get<IReadOnlyList<ProveedorDto>>(pagina, "ProveedoresRegistrados").Count);
        await CallAsync(pagina, "LimpiarAsync");
        Assert.Single(Get<IReadOnlyList<ProveedorDto>>(pagina, "ProveedoresRegistrados"));
    }

    [Fact]
    public async Task Proveedor_DirectorioNoHeredaLimiteDelAutocomplete()
    {
        await using var test = await TestDatabase.CreateAsync();
        for (var i = 0; i < 15; i++) test.Db.Proveedores.Add(new Proveedor { Nombre = $"Distribuidor {i}" });
        await test.Db.SaveChangesAsync();
        var servicio = new ProveedorService(test.Db);
        Assert.Equal(12, (await servicio.BuscarAsync("Distribuidor")).Count);
        Assert.Equal(15, (await servicio.BuscarAsync("Distribuidor", limite: null)).Count);
    }
}
