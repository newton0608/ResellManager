using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

public sealed class ProductoPaginacionTests
{
    [Fact]
    public async Task PaginasAcotadas_MantienenOrdenYNombresRepetidos_SinOmisionesNiDuplicados()
    {
        await using var test = await TestDatabase.CreateAsync();
        for (var i = 0; i < 47; i++)
            test.Db.Productos.Add(new Producto { CodigoInterno = $"PRO-PAG-{i:000}",
                Nombre = i < 30 ? "Lote repetido" : $"Lote Z {i:000}", CategoriaId = test.Categoria.Id });
        await test.Db.SaveChangesAsync();
        var observador = new ObservadorSql();
        await using var db = new ResellManagerDbContext(new DbContextOptionsBuilder<ResellManagerDbContext>()
            .UseSqlite(test.Db.Database.GetDbConnection()).AddInterceptors(observador).Options);
        var servicio = new ProductoService(db);
        var paginas = new List<ProductoPaginaDto>();
        ProductoCursorDto? cursor = null;
        do
        {
            var pagina = await servicio.ListarPaginaAsync("Lote", cursor: cursor);
            paginas.Add(pagina);
            cursor = pagina.SiguienteCursor;
            Assert.True(paginas.Count <= 3, "La paginación debe terminar.");
        } while (paginas[^1].HayMas);
        Assert.Equal(new[] { 20, 20, 7 }, paginas.Select(x => x.Items.Count));
        Assert.Null(cursor);
        var ids = paginas.SelectMany(x => x.Items).Select(x => x.Id).ToArray();
        var esperados = await test.Db.Productos.Where(x => x.Nombre.StartsWith("Lote"))
            .OrderBy(x => x.Nombre).ThenBy(x => x.Id).Select(x => x.Id).ToArrayAsync();
        Assert.Equal(esperados, ids);
        Assert.Equal(47, ids.Distinct().Count());
        Assert.Equal(3, observador.Consultas.Count);
        Assert.All(observador.Consultas, sql => Assert.Contains("LIMIT", sql));
        Assert.All(observador.Limites, limite => Assert.Equal(21L, limite));
    }

    [Fact]
    public async Task FiltrosYCodigos_SeAplicanEnCadaPagina_ConPrioridadExacta()
    {
        await using var test = await TestDatabase.CreateAsync();
        var otra = new Categoria { Nombre = "Otra categoría" };
        test.Db.Add(otra);
        await test.Db.SaveChangesAsync();
        var exacta = new Producto { CodigoInterno = "PRO-EXACTA", CodigoBarras = "buscar", Nombre = "Z foto exacta", CategoriaId = test.Categoria.Id };
        var exacta2 = new Producto { CodigoInterno = "buscar", Nombre = "Z foto exacta", CategoriaId = test.Categoria.Id };
        var parcial = new Producto { CodigoInterno = "PRO-buscar-001", Nombre = "A foto parcial", CategoriaId = test.Categoria.Id };
        var nombre = new Producto { CodigoInterno = "PRO-OTRO", Nombre = "B buscar por nombre", CategoriaId = test.Categoria.Id };
        test.Db.AddRange(exacta, exacta2, parcial, nombre,
            new Producto { CodigoInterno = "PRO-AJENA", CodigoBarras = "buscar", Nombre = "Ajena", CategoriaId = otra.Id });
        await test.Db.SaveChangesAsync();
        var servicio = new ProductoService(test.Db);
        var filas = new List<ProductoDto>();
        ProductoCursorDto? cursor = null;
        for (var i = 0; i < 4; i++)
        {
            var pagina = await servicio.ListarPaginaAsync("  BUSCAR  ", test.Categoria.Id, cursor, 1);
            filas.AddRange(pagina.Items);
            Assert.Equal(i < 3, pagina.HayMas);
            cursor = pagina.SiguienteCursor;
        }
        Assert.Equal(new[] { exacta.Id, exacta2.Id, parcial.Id, nombre.Id }, filas.Select(x => x.Id));
        Assert.All(filas, x => Assert.Equal(test.Categoria.Id, x.CategoriaId));
        Assert.Empty((await servicio.ListarPaginaAsync("sin coincidencia", test.Categoria.Id)).Items);
        Assert.All((await servicio.ListarPaginaAsync(categoriaId: otra.Id)).Items, x => Assert.Equal(otra.Id, x.CategoriaId));
    }

    [Fact]
    public async Task Cursor_NoDependeDeQueSigaExistiendoLaFilaAnterior()
    {
        await using var test = await TestDatabase.CreateAsync();
        test.Db.AddRange(new Producto { CodigoInterno = "PRO-UNO", Nombre = "Página A", CategoriaId = test.Categoria.Id },
            new Producto { CodigoInterno = "PRO-DOS", Nombre = "Página B", CategoriaId = test.Categoria.Id });
        await test.Db.SaveChangesAsync();
        var servicio = new ProductoService(test.Db);
        var primera = await servicio.ListarPaginaAsync("Página", tamano: 1);
        // Mutación sintética para verificar estabilidad del cursor, no una operación de la aplicación.
        await test.Db.Productos.Where(x => x.Id == primera.Items[0].Id).ExecuteDeleteAsync();
        var segunda = await servicio.ListarPaginaAsync("Página", cursor: primera.SiguienteCursor, tamano: 1);
        Assert.Equal("Página B", Assert.Single(segunda.Items).Nombre);
        Assert.False(segunda.HayMas);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    public async Task TamanoInvalido_SeRechazaSinConsultar(int tamano)
    {
        await using var test = await TestDatabase.CreateAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new ProductoService(test.Db).ListarPaginaAsync(tamano: tamano));
    }

    private sealed class ObservadorSql : DbCommandInterceptor
    {
        public List<string> Consultas { get; } = [];
        public List<long> Limites { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Consultas.Add(command.CommandText);
            Limites.Add(Convert.ToInt64(command.Parameters[command.Parameters.Count - 1].Value));
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
