using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;
using ResellManager.Infrastructure.Storage;

namespace ResellManager.Tests;

public sealed class CatalogoPaginacionTests
{
    [Fact]
    public async Task Paginas16_SeLimitanEnSqlTrasTodosLosFiltros_YNoDuplicanNiSaltan()
    {
        await using var test = await TestDatabase.CreateAsync();
        await SembrarAsync(test, 20, test.Categoria.Id, "ÁCME", disponibles: false);
        await SembrarAsync(test, 20, test.Categoria.Id, "Otra");
        var esperados = await SembrarAsync(test, 35, test.Categoria.Id, "\tÁCME\u00a0");
        var privada = await SembrarAsync(test, 1, test.Categoria.Id, "ÁCME");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-PAGINA", producto: privada[0]);
        var unidad = await test.Db.UnidadesInventario.SingleAsync(u => u.ProductoId == privada[0].Id);
        Assert.True((await new InventarioService(test.Db).ReservarAsync(
            unidad.Id, pedido.Detalles.Single().Id)).IsSuccess);
        var recorder = new ConsultasGrabadas();
        await using var consultas = ContextoLectura(test, recorder);
        var servicio = Servicio(consultas);
        var primera = await servicio.ListarPaginaAsync("PUBLICO", test.Categoria.Id, " ácme ");
        Assert.Equal(16, primera.Items.Count);
        Assert.True(primera.HasMore);
        Assert.Equal(esperados[15].Id, primera.NextCursor);
        var segunda = await servicio.ListarPaginaAsync("PUBLICO", test.Categoria.Id, " ácme ", primera.NextCursor);
        var tercera = await servicio.ListarPaginaAsync("PUBLICO", test.Categoria.Id, " ácme ", segunda.NextCursor);
        Assert.Equal(16, segunda.Items.Count);
        Assert.True(segunda.HasMore);
        Assert.Equal(3, tercera.Items.Count);
        Assert.False(tercera.HasMore);
        Assert.Null(tercera.NextCursor);
        Assert.Equal(esperados.Select(p => p.Id), primera.Items.Concat(segunda.Items)
            .Concat(tercera.Items).Select(p => p.Id));
        Assert.Equal(3, recorder.Sql.Count);
        Assert.All(recorder.Sql, sql =>
        {
            Assert.Contains("LIMIT", sql);
            Assert.Contains("RM_CATALOGO_MARCA", sql);
            Assert.Contains("EXISTS", sql);
            Assert.Contains("CategoriaPadreId", sql);
            Assert.DoesNotContain("ProductoImagenes", sql);
        });
        Assert.Empty(consultas.ChangeTracker.Entries());
        // Los metadatos de galería no forman parte de las tarjetas ni de esta transferencia.
        Assert.Empty((await servicio.ListarPaginaAsync("PUBLICO", test.Categoria.Id, "ÁCM")).Items);
    }

    [Fact]
    public async Task InventarioQueCambiaEntrePaginas_SeRevalidaEnServidor()
    {
        await using var test = await TestDatabase.CreateAsync();
        var productos = await SembrarAsync(test, 34, test.Categoria.Id, "Marca");
        var servicio = Servicio(test.Db);
        var primera = await servicio.ListarPaginaAsync();
        var unidad = await test.Db.UnidadesInventario.SingleAsync(u => u.ProductoId == productos[16].Id);
        unidad.Estado = EstadoUnidadInventario.Perdida;
        await test.Db.SaveChangesAsync();
        var segunda = await servicio.ListarPaginaAsync(cursor: primera.NextCursor);
        Assert.Equal(16, segunda.Items.Count);
        Assert.DoesNotContain(segunda.Items, p => p.Id == productos[16].Id);
        Assert.DoesNotContain(segunda.Items, p => primera.Items.Any(a => a.Id == p.Id));
        Assert.True(segunda.HasMore);
        Assert.False((await servicio.ObtenerPorIdAsync(productos[16].Id)).IsSuccess);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(11)]
    public async Task Escaparate_RaizHasta10Productos_YRaizVaciaNoSeExpone(int cantidad)
    {
        await using var test = await TestDatabase.CreateAsync();
        var productos = await SembrarAsync(test, cantidad, test.Categoria.Id, "Marca");
        var pagina = await Servicio(test.Db).LeerEscaparateAsync();
        Assert.False(pagina.HasMore);
        if (cantidad == 0) Assert.Empty(pagina.Secciones);
        else
        {
            var seccion = Assert.Single(pagina.Secciones);
            Assert.Equal(test.Categoria.Id, seccion.Categoria.Id);
            Assert.Equal(productos.Take(10).Select(p => p.Id), seccion.Productos.Select(p => p.Id));
        }
    }

    [Fact]
    public async Task Escaparate_PaginaRaices_YResuelveTresCarruselesEnDosConsultas()
    {
        await using var test = await TestDatabase.CreateAsync();
        var raices = Enumerable.Range(0, 5).Select(i => new Categoria { Nombre = $"Raíz {i}" }).ToArray();
        test.Db.Categorias.AddRange(raices);
        await test.Db.SaveChangesAsync();
        foreach (var raiz in raices) await SembrarAsync(test, 13, raiz.Id, "Marca");
        var recorder = new ConsultasGrabadas();
        await using var consultas = ContextoLectura(test, recorder);
        var servicio = Servicio(consultas);
        var primera = await servicio.LeerEscaparateAsync();
        Assert.Equal(3, primera.Secciones.Count);
        Assert.True(primera.HasMore);
        Assert.Equal(raices[2].Id, primera.NextCursor);
        Assert.Equal(2, recorder.Sql.Count);
        Assert.All(primera.Secciones, s => Assert.Equal(10, s.Productos.Count));
        Assert.Contains("LIMIT", recorder.Sql[0]);
        Assert.Contains("OFFSET", recorder.Sql[1]);
        Assert.DoesNotContain("COUNT(*)", recorder.Sql[1]);
        recorder.Sql.Clear();
        var segunda = await servicio.LeerEscaparateAsync(primera.NextCursor);
        Assert.Equal(2, segunda.Secciones.Count);
        Assert.False(segunda.HasMore);
        Assert.Null(segunda.NextCursor);
        Assert.Equal(2, recorder.Sql.Count);
        Assert.Equal(raices.Select(r => r.Id), primera.Secciones.Concat(segunda.Secciones)
            .Select(s => s.Categoria.Id));
    }

    [Fact]
    public async Task Escaparate_SiDesapareceDisponibilidadEntreConsultas_NoExponeRaicesVacias_YPermiteContinuar()
    {
        await using var test = await TestDatabase.CreateAsync();
        var raices = Enumerable.Range(0, 4).Select(i => new Categoria { Nombre = $"Raíz {i}" }).ToArray();
        test.Db.Categorias.AddRange(raices);
        await test.Db.SaveChangesAsync();
        foreach (var raiz in raices) await SembrarAsync(test, 1, raiz.Id, "Marca");
        var recorder = new ConsultasGrabadas();
        recorder.AntesDeLeer = command =>
        {
            if (recorder.Sql.Count != 2) return;
            using var cambio = command.Connection!.CreateCommand();
            cambio.CommandText = "UPDATE UnidadesInventario SET Estado = $estado WHERE ProductoId IN "
                + "(SELECT Id FROM Productos WHERE CategoriaId <= $ultimaRaiz)";
            var estado = cambio.CreateParameter(); estado.ParameterName = "$estado";
            estado.Value = (int)EstadoUnidadInventario.Perdida; cambio.Parameters.Add(estado);
            var raiz = cambio.CreateParameter(); raiz.ParameterName = "$ultimaRaiz";
            raiz.Value = raices[2].Id; cambio.Parameters.Add(raiz);
            cambio.ExecuteNonQuery();
        };
        await using var consultas = ContextoLectura(test, recorder);
        var servicio = Servicio(consultas);
        var primera = await servicio.LeerEscaparateAsync();
        Assert.Empty(primera.Secciones);
        Assert.True(primera.HasMore);
        Assert.Equal(raices[2].Id, primera.NextCursor);
        var segunda = await servicio.LeerEscaparateAsync(primera.NextCursor);
        Assert.Equal(raices[3].Id, Assert.Single(segunda.Secciones).Categoria.Id);
        Assert.False(segunda.HasMore);
        Assert.Null(segunda.NextCursor);
    }

    [Fact]
    public async Task ContextoHija_ReconstruyeRaiz_YOpcionesSoloRevelanCategoriasPublicables()
    {
        await using var test = await TestDatabase.CreateAsync();
        var hija = new Categoria { Nombre = "Hija", CategoriaPadreId = test.Categoria.Id };
        var vacia = new Categoria { Nombre = "Vacía", CategoriaPadreId = test.Categoria.Id };
        var privada = new Categoria { Nombre = "Privada" };
        test.Db.Categorias.AddRange(hija, vacia, privada);
        await test.Db.SaveChangesAsync();
        var hijos = await SembrarAsync(test, 12, hija.Id, "Marca hija");
        await SembrarAsync(test, 2, vacia.Id, "Privada", disponibles: false);
        await SembrarAsync(test, 2, privada.Id, "Privada", disponibles: false);
        var servicio = Servicio(test.Db);
        var raiz = Assert.Single((await servicio.LeerRaicesAsync()).Items);
        Assert.Equal(test.Categoria.Id, raiz.Id);
        Assert.Equal(hija.Id, Assert.Single((await servicio.LeerSubcategoriasAsync(raiz.Id)).Items).Id);
        var contexto = (await servicio.LeerContextoCategoriaAsync(hija.Id)).Value!;
        Assert.Equal(raiz, contexto.Raiz);
        Assert.Equal(hija.Id, contexto.Seleccionada.Id);
        Assert.Equal(raiz.Id, contexto.Seleccionada.CategoriaPadreId);
        Assert.False((await servicio.LeerContextoCategoriaAsync(vacia.Id)).IsSuccess);
        Assert.False((await servicio.LeerContextoCategoriaAsync(privada.Id)).IsSuccess);
        Assert.Empty((await servicio.LeerSubcategoriasAsync(hija.Id)).Items);
        var escaparate = Assert.Single((await servicio.LeerEscaparateAsync()).Secciones);
        Assert.Equal(hijos.Take(10).Select(p => p.Id), escaparate.Productos.Select(p => p.Id));
        await SembrarAsync(test, 2, raiz.Id, "Marca raíz");
        Assert.Equal(14, (await servicio.ListarPaginaAsync(categoriaId: raiz.Id)).Items.Count);
        Assert.Equal(12, (await servicio.ListarPaginaAsync(categoriaId: hija.Id)).Items.Count);
    }

    [Fact]
    public async Task OpcionesRaicesEHijas_SePaginanConCursorIdEstable()
    {
        await using var test = await TestDatabase.CreateAsync();
        var hijas = Enumerable.Range(0, 19).Select(i => new Categoria
            { Nombre = $"Hija {i}", CategoriaPadreId = test.Categoria.Id }).ToArray();
        var raices = Enumerable.Range(0, 17).Select(i => new Categoria { Nombre = $"Raíz {i}" }).ToArray();
        test.Db.Categorias.AddRange(hijas.Concat(raices));
        await test.Db.SaveChangesAsync();
        foreach (var categoria in hijas.Concat(raices)) await SembrarAsync(test, 1, categoria.Id, "Marca");
        var servicio = Servicio(test.Db);
        var primeraHijas = await servicio.LeerSubcategoriasAsync(test.Categoria.Id);
        var segundaHijas = await servicio.LeerSubcategoriasAsync(test.Categoria.Id, primeraHijas.NextCursor);
        Assert.Equal(16, primeraHijas.Items.Count);
        Assert.Equal(3, segundaHijas.Items.Count);
        Assert.True(primeraHijas.HasMore);
        Assert.False(segundaHijas.HasMore);
        var primeraRaices = await servicio.LeerRaicesAsync();
        var segundaRaices = await servicio.LeerRaicesAsync(primeraRaices.NextCursor);
        Assert.Equal(16, primeraRaices.Items.Count);
        Assert.Equal(2, segundaRaices.Items.Count);
        Assert.True(primeraRaices.HasMore);
        Assert.False(segundaRaices.HasMore);
        Assert.Equal(hijas.Select(c => c.Id), primeraHijas.Items.Concat(segundaHijas.Items).Select(c => c.Id));
    }

    [Fact]
    public async Task Marcas_OpcionesAcotadasUnicodeSinDuplicados_EIndependientesDeLosFiltros()
    {
        await using var test = await TestDatabase.CreateAsync();
        await SembrarAsync(test, 1, test.Categoria.Id, "\tÁCME\u00a0");
        await SembrarAsync(test, 1, test.Categoria.Id, " ácme ");
        await SembrarAsync(test, 1, test.Categoria.Id, " βETA ");
        await SembrarAsync(test, 1, test.Categoria.Id, " БРЕНД ");
        await SembrarAsync(test, 1, test.Categoria.Id, "\t\u00a0");
        await SembrarAsync(test, 1, test.Categoria.Id, "Privada", disponibles: false);
        for (var i = 0; i < 17; i++) await SembrarAsync(test, 1, test.Categoria.Id, $"Marca {i:00}");
        var recorder = new ConsultasGrabadas();
        await using var consultas = ContextoLectura(test, recorder);
        var servicio = Servicio(consultas);
        var primera = await servicio.LeerMarcasAsync();
        var segunda = await servicio.LeerMarcasAsync(primera.NextCursor);
        var marcas = primera.Items.Concat(segunda.Items).ToArray();
        Assert.Equal(16, primera.Items.Count);
        Assert.True(primera.HasMore);
        Assert.Equal(4, segunda.Items.Count);
        Assert.False(segunda.HasMore);
        Assert.Null(segunda.NextCursor);
        Assert.Equal(20, marcas.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Single(marcas.Where(m => string.Equals(m, "ÁCME", StringComparison.OrdinalIgnoreCase)));
        Assert.All(marcas, m => Assert.Equal(m.Trim(), m));
        Assert.DoesNotContain("Privada", marcas);
        Assert.DoesNotContain("", marcas);
        Assert.Equal(2, (await servicio.ListarPaginaAsync(marca: "ÁCME")).Items.Count);
        Assert.Single((await servicio.ListarPaginaAsync(marca: "βeta")).Items);
        Assert.Single((await servicio.ListarPaginaAsync(marca: "бренд")).Items);
        Assert.All(recorder.Sql.Take(2), sql =>
        {
            Assert.Contains("LIMIT", sql);
            Assert.Contains("GROUP BY", sql);
            Assert.Contains("RM_CATALOGO_MARCA", sql);
            Assert.DoesNotContain("PrecioSugerido", sql);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(17)]
    [InlineData(int.MaxValue)]
    public async Task TamanoNoPermitido_SeRechazaAntesDeConsultar(int tamano)
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = Servicio(test.Db);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => servicio.ListarPaginaAsync(tamano: tamano));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => servicio.LeerRaicesAsync(tamano: tamano));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => servicio.LeerMarcasAsync(tamano: tamano));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => servicio.LeerSubcategoriasAsync(test.Categoria.Id, tamano: tamano));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CursorInvalido_SeRechazaAntesDeConsultar(int cursor)
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = Servicio(test.Db);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => servicio.ListarPaginaAsync(cursor: cursor));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => servicio.LeerRaicesAsync(cursor));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => servicio.LeerEscaparateAsync(cursor));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => servicio.LeerSubcategoriasAsync(test.Categoria.Id, cursor));
        await Assert.ThrowsAsync<ArgumentException>(() => servicio.LeerMarcasAsync(" "));
    }

    [Fact]
    public async Task Escaparate_NoPermiteMasDeTresSeccionesEnUnaSolicitud()
    {
        await using var test = await TestDatabase.CreateAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Servicio(test.Db).LeerEscaparateAsync(tamano: 4));
    }

    private static ResellManagerDbContext ContextoLectura(TestDatabase test, ConsultasGrabadas recorder) =>
        new(new DbContextOptionsBuilder<ResellManagerDbContext>()
            .UseSqlite(test.Db.Database.GetDbConnection()).AddInterceptors(recorder).Options);

    private static CatalogoPublicoService Servicio(ResellManagerDbContext db) => new(db,
        new AlmacenamientoImagenesProductoLocal(Options.Create(new AlmacenamientoImagenesProductoOptions
        {
            DirectorioBase = Path.Combine(Path.GetTempPath(), "resellmanager-catalogo-paginas")
        }), NullLogger<AlmacenamientoImagenesProductoLocal>.Instance));

    private static async Task<Producto[]> SembrarAsync(TestDatabase test, int cantidad, int categoriaId,
        string? marca, bool disponibles = true)
    {
        var lote = Guid.NewGuid().ToString("N");
        var productos = Enumerable.Range(0, cantidad).Select(i => new Producto
        {
            CodigoInterno = $"PRO-{lote[..12]}-{i}", Nombre = $"Publico {lote} {i}",
            CategoriaId = categoriaId, Marca = marca, PrecioSugerido = 100m
        }).ToArray();
        test.Db.Productos.AddRange(productos);
        await test.Db.SaveChangesAsync();
        if (cantidad > 0 && disponibles)
        {
            var compra = await new CompraService(test.Db).RegistrarAsync(new CompraInput(
                $"COM-{lote[..12]}", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 11),
                OrigenCompra.CompraLocal, test.Proveedor.Id, null,
                productos.Select(p => new DetalleCompraInput(p.Id, 1, 40m)).ToArray(), null));
            Assert.True(compra.IsSuccess, compra.ErrorMessage);
        }
        return productos;
    }

    private sealed class ConsultasGrabadas : DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];
        public Action<DbCommand>? AntesDeLeer { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Sql.Add(command.CommandText);
            AntesDeLeer?.Invoke(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
