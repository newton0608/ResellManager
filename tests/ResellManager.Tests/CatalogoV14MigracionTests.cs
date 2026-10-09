using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;
using ResellManager.Infrastructure.Storage;
using SkiaSharp;

namespace ResellManager.Tests;

public sealed class CatalogoV14MigracionTests
{
    [Fact]
    public async Task UpgradeDesdeV130_ConservaPortadaBytesRaicesYDatosHistoricos()
    {
        var directorio = Path.Combine(Path.GetTempPath(), "resellmanager-migration-v14-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directorio, "1"));
        var ruta = "productos/1/imagen-principal-00000000000000000000000000000001.webp";
        using var bitmap = new SKBitmap(12, 12);
        bitmap.Erase(SKColors.Blue);
        using var imagen = SKImage.FromBitmap(bitmap);
        using var webp = imagen.Encode(SKEncodedImageFormat.Webp, 85);
        var bytes = webp.ToArray();
        await File.WriteAllBytesAsync(Path.Combine(directorio, "1", Path.GetFileName(ruta)), bytes);
        try
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var db = new ResellManagerDbContext(
                new DbContextOptionsBuilder<ResellManagerDbContext>().UseSqlite(connection).Options);
            var migrator = db.Database.GetService<IMigrator>();
            await migrator.MigrateAsync("20261003155826_AddPurchaseCurrencies");
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Categorias (Id, Nombre) VALUES (1, 'Histórica');
                INSERT INTO Proveedores (Id, Nombre) VALUES (1, 'Proveedor histórico');
                INSERT INTO Clientes (Id, Nombres, Telefono) VALUES (1, 'Cliente histórico', '555-0100');
                INSERT INTO Productos (Id, CodigoInterno, Nombre, CategoriaId, PrecioSugerido, ImagenPrincipalRuta)
                    VALUES (1, 'PRO-HIST', 'Con foto', 1, 175, 'productos/1/imagen-principal-00000000000000000000000000000001.webp'),
                           (2, 'PRO-SIN-FOTO', 'Sin foto', 1, 125, NULL);
                INSERT INTO Compras (Id, CodigoInterno, FechaCompra, Origen, Total, ProveedorId, Moneda, TipoCambio, TotalMonedaOrigen)
                    VALUES (1, 'COMPRA-HIST', '2026-01-10', 'CompraLocal', 100.12345, 1, 'GTQ', 1, 100.12345);
                INSERT INTO DetallesCompra (Id, Cantidad, CostoUnitario, CompraId, ProductoId, CostoUnitarioMonedaOrigen)
                    VALUES (1, 2, 50.555, 1, 1, 50.555);
                INSERT INTO UnidadesInventario (Id, CodigoInterno, Estado, FechaIngreso, Costo, ProductoId, DetalleCompraId)
                    VALUES (1, 'UNI-HIST', 'Disponible', '2026-01-11', 50.555, 1, 1),
                           (2, 'UNI-VENDIDA', 'Vendida', '2026-01-11', 50.555, 1, 1);
                INSERT INTO Pedidos (Id, CodigoInterno, Fecha, TipoPedido, CanalVenta, Estado, ClienteId)
                    VALUES (1, 'PED-HIST', '2026-01-12', 'VentaDirecta', 0, 'Completado', 1);
                INSERT INTO Ventas (Id, CodigoInterno, Fecha, Estado, PedidoId)
                    VALUES (1, 'VEN-HIST', '2026-01-12', 'Registrada', 1);
                INSERT INTO DetallesVenta (Id, PrecioFinal, VentaId, UnidadInventarioId)
                    VALUES (1, 125.123, 1, 2);
                INSERT INTO Pagos (Id, Fecha, Monto, MetodoPago, ClienteId)
                    VALUES (1, '2026-01-12', 70.125, 'Efectivo', 1);
                """);
            var antes = await HuellaAsync(connection);
            await migrator.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(antes, await HuellaAsync(connection));
            var portada = Assert.Single(await db.ProductoImagenes.AsNoTracking().ToListAsync());
            Assert.NotEqual(Guid.Empty, portada.Id);
            Assert.Equal(1, portada.ProductoId);
            Assert.Equal(0, portada.Orden);
            Assert.Equal(ruta, portada.RutaRelativa);
            Assert.Equal(ruta, (await db.Productos.AsNoTracking().SingleAsync(p => p.Id == 1)).ImagenPrincipalRuta);
            Assert.Null((await db.Categorias.AsNoTracking().SingleAsync()).CategoriaPadreId);
            Assert.Empty(await db.ProductoImagenes.Where(i => i.ProductoId == 2).ToListAsync());
            var servicio = new CatalogoPublicoService(db, new AlmacenamientoImagenesProductoLocal(
                Options.Create(new AlmacenamientoImagenesProductoOptions { DirectorioBase = directorio }),
                NullLogger<AlmacenamientoImagenesProductoLocal>.Instance));
            var metadatos = Assert.Single((await servicio.ObtenerPorIdAsync(1)).Value!.Imagenes!);
            Assert.Equal(portada.Id.ToString("N"), metadatos.Id);
            Assert.True(metadatos.EsPortada);
            foreach (var apertura in new[] { await servicio.AbrirImagenPrincipalAsync(1),
                await servicio.AbrirImagenAsync(1, metadatos.Id) })
            {
                Assert.True(apertura.IsSuccess, apertura.ErrorMessage);
                await using var contenido = apertura.Value!.Contenido;
                using var copia = new MemoryStream();
                await contenido.CopyToAsync(copia);
                Assert.Equal(bytes, copia.ToArray());
            }
            Assert.Single(Directory.GetFiles(directorio, "*", SearchOption.AllDirectories));
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_key_check";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.False(await reader.ReadAsync());
        }
        finally { Directory.Delete(directorio, true); }
    }

    private static async Task<IReadOnlyList<string>> HuellaAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT 'Producto|' || Id || '|' || CodigoInterno || '|' || CategoriaId || '|' || quote(PrecioSugerido) || '|' || quote(ImagenPrincipalRuta) FROM Productos
            UNION ALL SELECT 'Categoria|' || Id || '|' || Nombre FROM Categorias
            UNION ALL SELECT 'Compra|' || Id || '|' || quote(Total) || '|' || quote(TotalMonedaOrigen) FROM Compras
            UNION ALL SELECT 'DetalleCompra|' || Id || '|' || quote(CostoUnitario) || '|' || quote(CostoUnitarioMonedaOrigen) FROM DetallesCompra
            UNION ALL SELECT 'Unidad|' || Id || '|' || Estado || '|' || quote(Costo) FROM UnidadesInventario
            UNION ALL SELECT 'Pedido|' || Id || '|' || CodigoInterno || '|' || Estado FROM Pedidos
            UNION ALL SELECT 'Venta|' || Id || '|' || CodigoInterno || '|' || Estado FROM Ventas
            UNION ALL SELECT 'DetalleVenta|' || Id || '|' || quote(PrecioFinal) || '|' || UnidadInventarioId FROM DetallesVenta
            UNION ALL SELECT 'Pago|' || Id || '|' || quote(Monto) || '|' || MetodoPago FROM Pagos
            ORDER BY 1
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var filas = new List<string>();
        while (await reader.ReadAsync()) filas.Add(reader.GetString(0));
        return filas;
    }
}
