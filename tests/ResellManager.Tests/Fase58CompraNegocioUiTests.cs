using Microsoft.EntityFrameworkCore;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Compras;

namespace ResellManager.Tests;

public sealed class Fase58CompraNegocioTests
{
    [Fact]
    public void CodigoCompra_UsaPrefijoGuidYNoSeRepite()
    {
        var primero = CodigosInternos.CrearCodigoCompra();
        var segundo = CodigosInternos.CrearCodigoCompra();

        Assert.StartsWith(CodigosInternos.PrefijoCompra, primero);
        Assert.Equal(36, primero.Length);
        Assert.True(Guid.TryParseExact(primero[4..], "N", out _));
        Assert.NotEqual(primero, segundo);
    }

    [Fact]
    public async Task Backend_ConservaUnicidadDelCodigoCompra()
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new CompraService(test.Db);
        var codigo = CodigosInternos.CrearCodigoCompra();

        var primera = await servicio.RegistrarAsync(
            test.Compra(OrigenCompra.Catalogo, codigo)
        );
        var segunda = await servicio.RegistrarAsync(
            test.Compra(OrigenCompra.Catalogo, codigo)
        );

        Assert.True(primera.IsSuccess, primera.ErrorMessage);
        Assert.False(segunda.IsSuccess);
        Assert.Single(await test.Db.Compras.ToListAsync());
    }

    [Fact]
    public async Task CompraConMultiplesDetalles_CalculaTotalEnBackend()
    {
        await using var test = await TestDatabase.CreateAsync();
        var otroProducto = await test.CrearProductoAsync("PROD-COMPRA-2");
        var input = test.Compra(
            OrigenCompra.CompraLocal,
            CodigosInternos.CrearCodigoCompra(),
            new DateOnly(2026, 9, 1)
        ) with
        {
            Detalles =
            [
                new DetalleCompraInput(test.Producto.Id, 2, 40m),
                new DetalleCompraInput(otroProducto.Id, 3, 25.50m),
            ],
        };

        var resultado = await new CompraService(test.Db).RegistrarAsync(input);

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal(156.50m, resultado.Value!.Total);
        Assert.Equal(2, resultado.Value.Detalles.Count);
        Assert.Equal(5, await test.Db.UnidadesInventario.CountAsync());
    }

    [Fact]
    public async Task Compra_RequiereProveedorReal()
    {
        await using var test = await TestDatabase.CreateAsync();
        var input = test.Compra(
            OrigenCompra.Catalogo,
            CodigosInternos.CrearCodigoCompra()
        ) with
        {
            ProveedorId = int.MaxValue,
        };

        var resultado = await new CompraService(test.Db).RegistrarAsync(input);

        Assert.False(resultado.IsSuccess);
        Assert.Empty(await test.Db.Compras.ToListAsync());
    }

    [Fact]
    public async Task Compra_RequiereProductosReales()
    {
        await using var test = await TestDatabase.CreateAsync();
        var input = test.Compra(
            OrigenCompra.Catalogo,
            CodigosInternos.CrearCodigoCompra()
        ) with
        {
            Detalles = [new DetalleCompraInput(int.MaxValue, 1, 10m)],
        };

        var resultado = await new CompraService(test.Db).RegistrarAsync(input);

        Assert.False(resultado.IsSuccess);
        Assert.Empty(await test.Db.Compras.ToListAsync());
    }

    [Fact]
    public async Task CompraLocal_SinFechaIngresoSeRechaza()
    {
        await using var test = await TestDatabase.CreateAsync();

        var resultado = await new CompraService(test.Db).RegistrarAsync(
            test.Compra(OrigenCompra.CompraLocal, CodigosInternos.CrearCodigoCompra())
        );

        Assert.False(resultado.IsSuccess);
        Assert.Empty(await test.Db.UnidadesInventario.ToListAsync());
    }

    [Fact]
    public async Task EnvioHermano_SinFechaIngresoSeRechaza()
    {
        await using var test = await TestDatabase.CreateAsync();

        var resultado = await new CompraService(test.Db).RegistrarAsync(
            test.Compra(OrigenCompra.EnvioHermano, CodigosInternos.CrearCodigoCompra())
        );

        Assert.False(resultado.IsSuccess);
        Assert.Empty(await test.Db.UnidadesInventario.ToListAsync());
    }

    [Fact]
    public async Task Importacion_ConFechaIngresoSeRechaza()
    {
        await using var test = await TestDatabase.CreateAsync();

        var resultado = await new CompraService(test.Db).RegistrarAsync(
            test.Compra(
                OrigenCompra.Importacion,
                CodigosInternos.CrearCodigoCompra(),
                new DateOnly(2026, 9, 1)
            )
        );

        Assert.False(resultado.IsSuccess);
        Assert.Empty(await test.Db.UnidadesInventario.ToListAsync());
    }

    [Fact]
    public async Task Catalogo_ConMultiplesDetallesNuncaGeneraInventario()
    {
        await using var test = await TestDatabase.CreateAsync();
        var otroProducto = await test.CrearProductoAsync("PROD-CATALOGO-2");
        var input = test.Compra(
            OrigenCompra.Catalogo,
            CodigosInternos.CrearCodigoCompra()
        ) with
        {
            FechaIngreso = null,
            Detalles =
            [
                new DetalleCompraInput(test.Producto.Id, 4, 15m),
                new DetalleCompraInput(otroProducto.Id, 2, 20m),
            ],
        };

        var resultado = await new CompraService(test.Db).RegistrarAsync(input);

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal(2, await test.Db.DetallesCompra.CountAsync());
        Assert.Empty(await test.Db.UnidadesInventario.ToListAsync());
    }

    [Fact]
    public async Task CostoDeUnidadProvieneDelDetalleCompra()
    {
        await using var test = await TestDatabase.CreateAsync();
        var input = test.Compra(
            OrigenCompra.EnvioHermano,
            CodigosInternos.CrearCodigoCompra(),
            new DateOnly(2026, 9, 1),
            cantidad: 2
        ) with
        {
            Detalles = [new DetalleCompraInput(test.Producto.Id, 2, 83.75m)],
        };

        var resultado = await new CompraService(test.Db).RegistrarAsync(input);
        var unidades = await test.Db.UnidadesInventario.ToListAsync();

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.All(unidades, unidad => Assert.Equal(83.75m, unidad.Costo));
        Assert.All(unidades, unidad => Assert.StartsWith(resultado.Value!.CodigoInterno, unidad.CodigoInterno));
    }
}

public sealed class Fase58CompraUiTests
{
    [Fact]
    public void FormularioCompra_NoExponeCodigoNiRutaManual()
    {
        Assert.Null(typeof(CompraFormModel).GetProperty("CodigoInterno"));
        Assert.Null(typeof(CompraFormModel).GetProperty("RutaDocumento"));

        var fuente = File.ReadAllText(
            Path.Combine(
                BuscarRaizRepositorio(),
                "src",
                "ResellManager.Web",
                "Components",
                "Pages",
                "CompraNueva.razor"
            )
        );

        Assert.Contains("CodigosInternos.CrearCodigoCompra()", fuente, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"compra-codigo\"", fuente, StringComparison.Ordinal);
        Assert.DoesNotContain("RutaDocumento", fuente, StringComparison.Ordinal);
        Assert.Contains("<InputFile", fuente, StringComparison.Ordinal);
        Assert.Contains("Guardando", fuente, StringComparison.Ordinal);
        Assert.Contains("disabled=", fuente, StringComparison.Ordinal);
    }

    [Fact]
    public void FormularioCompra_MuestraFechaIngresoSoloParaOrigenesRecibidos()
    {
        var modelo = new CompraFormModel
        {
            Origen = OrigenCompra.Importacion,
            FechaIngreso = new DateOnly(2026, 9, 1),
        };

        var importacion = modelo.ToInput(CodigosInternos.CrearCodigoCompra());
        modelo.Origen = OrigenCompra.Catalogo;
        var catalogo = modelo.ToInput(CodigosInternos.CrearCodigoCompra());
        modelo.Origen = OrigenCompra.CompraLocal;
        var local = modelo.ToInput(CodigosInternos.CrearCodigoCompra());

        Assert.Null(importacion.FechaIngreso);
        Assert.Null(catalogo.FechaIngreso);
        Assert.Equal(new DateOnly(2026, 9, 1), local.FechaIngreso);
    }

    [Fact]
    public void ComprasYProveedores_UsanPatronResponsiveExistente()
    {
        var paginas = Path.Combine(
            BuscarRaizRepositorio(),
            "src",
            "ResellManager.Web",
            "Components",
            "Pages"
        );

        var compras = File.ReadAllText(Path.Combine(paginas, "Compras.razor"));
        var proveedores = File.ReadAllText(Path.Combine(paginas, "Proveedores.razor"));

        Assert.Contains("desktop-table", compras, StringComparison.Ordinal);
        Assert.Contains("mobile-card-list", compras, StringComparison.Ordinal);
        Assert.Contains("desktop-table", proveedores, StringComparison.Ordinal);
        Assert.Contains("mobile-card-list", proveedores, StringComparison.Ordinal);
    }

    [Fact]
    public void ComponentesBlazor_NoUsanDbContext()
    {
        var componentes = Directory.GetFiles(
            Path.Combine(BuscarRaizRepositorio(), "src", "ResellManager.Web", "Components"),
            "*.razor",
            SearchOption.AllDirectories
        );

        Assert.All(
            componentes,
            archivo =>
                Assert.DoesNotContain(
                    "DbContext",
                    File.ReadAllText(archivo),
                    StringComparison.Ordinal
                )
        );
    }

    private static string BuscarRaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "ResellManager.sln")))
            directorio = directorio.Parent;

        return directorio?.FullName
            ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}
