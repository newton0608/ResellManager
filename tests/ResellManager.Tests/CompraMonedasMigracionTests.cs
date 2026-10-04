using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;

namespace ResellManager.Tests;

public sealed class CompraMonedasMigracionTests
{
    private const string Anterior = "20261001043949_AddProductoImagenPrincipal";
    private const string Monedas = "20261003155826_AddPurchaseCurrencies";

    [Fact]
    public async Task UpgradeDeBaseExistente_CopiaOrigenSinRecalcularNingunImporteHistorico()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ResellManagerDbContext>().UseSqlite(connection).Options;
        await using var db = new ResellManagerDbContext(options);
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(Anterior);
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Categorias (Id, Nombre) VALUES (1, 'Histórica');
            INSERT INTO Proveedores (Id, Nombre) VALUES (1, 'Proveedor histórico');
            INSERT INTO Productos (Id, CodigoInterno, Nombre, CategoriaId, PrecioSugerido)
                VALUES (1, 'PROD-HIST', 'Producto histórico', 1, 175);
            INSERT INTO Compras (Id, CodigoInterno, FechaCompra, Origen, Total, ProveedorId)
                VALUES (1, 'COMPRA-HIST', '2026-01-10', 'CompraLocal', 100, 1),
                       (2, 'COMPRA-PRECISA', '2026-01-11', 'Importacion', 100.12345, 1);
            INSERT INTO DetallesCompra (Id, Cantidad, CostoUnitario, CompraId, ProductoId)
                VALUES (1, 2, 50, 1, 1), (2, 2, 50.555, 2, 1);
            INSERT INTO UnidadesInventario
                (Id, CodigoInterno, Estado, FechaIngreso, Costo, ProductoId, DetalleCompraId)
                VALUES (1, 'UNI-HIST-1', 'Disponible', '2026-01-11', 50, 1, 1),
                       (2, 'UNI-HIST-2', 'Disponible', '2026-01-11', 50, 1, 1),
                       (3, 'UNI-PRECISA', 'Comprada', NULL, 50.555, 1, 2);
            """);
        var antes = await HuellaImportesAsync(connection);
        var script = migrator.GenerateScript(Anterior, Monedas);
        Assert.Contains("ALTER TABLE", script);
        Assert.Contains("UPDATE Compras SET TotalMonedaOrigen = Total", script);
        Assert.Contains("UPDATE DetallesCompra SET CostoUnitarioMonedaOrigen = CostoUnitario", script);
        Assert.DoesNotContain("DROP TABLE", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE UnidadesInventario", script, StringComparison.OrdinalIgnoreCase);

        await migrator.MigrateAsync();
        Assert.Equal(antes, await HuellaImportesAsync(connection));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var compras = await db.Compras.AsNoTracking().Include(x => x.Detalles)
            .ThenInclude(x => x.UnidadesInventario).OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, compras.Count);
        Assert.All(compras, compra =>
        {
            Assert.Equal(MonedaCompra.GTQ, compra.Moneda);
            Assert.Equal(1m, compra.TipoCambio);
            Assert.Equal(compra.Total, compra.TotalMonedaOrigen);
            Assert.Null(compra.TipoCambioReferencia);
            Assert.Null(compra.FechaTipoCambioReferencia);
            Assert.Null(compra.FuenteTipoCambio);
            Assert.All(compra.Detalles, detalle =>
                Assert.Equal(detalle.CostoUnitario, detalle.CostoUnitarioMonedaOrigen));
        });
        Assert.Equal(100m, compras[0].Total);
        var detalleHistorico = Assert.Single(compras[0].Detalles);
        Assert.Equal(50m, detalleHistorico.CostoUnitario);
        Assert.Equal(50m, detalleHistorico.CostoUnitarioMonedaOrigen);
        Assert.Equal(2, detalleHistorico.UnidadesInventario.Count);
        Assert.All(detalleHistorico.UnidadesInventario, u => Assert.Equal(50m, u.Costo));
        Assert.Equal(100.12345m, compras[1].Total);
        Assert.Equal(100.12345m, compras[1].TotalMonedaOrigen);
        Assert.Equal(50.555m, compras[1].Detalles.Single().CostoUnitario);
        Assert.Equal(50.555m, compras[1].Detalles.Single().CostoUnitarioMonedaOrigen);
        Assert.Equal(50.555m, compras[1].Detalles.Single().UnidadesInventario.Single().Costo);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.False(await reader.ReadAsync());
    }

    private static async Task<IReadOnlyList<string>> HuellaImportesAsync(DbConnection connection)
    {
        await using var command = connection.CreateCommand();
        // quote + typeof demuestra preservación del valor SQLite, incluso mayor escala histórica.
        command.CommandText = """
            SELECT 'Compra|' || Id || '|' || typeof(Total) || '|' || quote(Total) FROM Compras
            UNION ALL
            SELECT 'Detalle|' || Id || '|' || typeof(CostoUnitario) || '|' || quote(CostoUnitario) FROM DetallesCompra
            UNION ALL
            SELECT 'Unidad|' || Id || '|' || typeof(Costo) || '|' || quote(Costo) FROM UnidadesInventario
            ORDER BY 1
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var resultado = new List<string>();
        while (await reader.ReadAsync()) resultado.Add(reader.GetString(0));
        return resultado;
    }
}
