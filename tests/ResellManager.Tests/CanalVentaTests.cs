using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Pedidos;
using ResellManager.Web.Components.Ventas;

namespace ResellManager.Tests;

public sealed class CanalVentaTests
{
    [Fact]
    public void Enum_TieneValoresExplicitosYEstables()
    {
        Assert.Equal(
            [
                CanalVenta.Presencial,
                CanalVenta.WhatsApp,
                CanalVenta.Facebook,
                CanalVenta.Web,
                CanalVenta.Otro,
            ],
            Enum.GetValues<CanalVenta>()
        );
        Assert.Equal(1, (int)CanalVenta.Presencial);
        Assert.Equal(2, (int)CanalVenta.WhatsApp);
        Assert.Equal(3, (int)CanalVenta.Facebook);
        Assert.Equal(4, (int)CanalVenta.Web);
        Assert.Equal(5, (int)CanalVenta.Otro);
    }

    [Theory]
    [InlineData(TipoPedido.Catalogo, CanalVenta.Facebook)]
    [InlineData(TipoPedido.Apartado, CanalVenta.WhatsApp)]
    public async Task PedidoService_GuardaYDevuelveCanalIndependienteDelTipo(
        TipoPedido tipoPedido,
        CanalVenta canalVenta
    )
    {
        await using var test = await TestDatabase.CreateAsync();
        var service = new PedidoService(test.Db);

        var creado = await service.CrearAsync(Input(test, tipoPedido, canalVenta));
        Assert.True(creado.IsSuccess, creado.ErrorMessage);

        var listado = await service.ListarAsync();
        var detalle = await service.ObtenerPorIdAsync(creado.Value!.Id);
        var persistido = await test.Db.Pedidos.AsNoTracking().SingleAsync(x => x.Id == creado.Value.Id);

        Assert.Equal(tipoPedido, persistido.TipoPedido);
        Assert.Equal(canalVenta, persistido.CanalVenta);
        Assert.Contains(listado, x => x.Id == creado.Value.Id && x.CanalVenta == canalVenta);
        Assert.True(detalle.IsSuccess, detalle.ErrorMessage);
        Assert.Equal(canalVenta, detalle.Value!.CanalVenta);
    }

    [Fact]
    public async Task PedidoService_RechazaCanalNoDefinido()
    {
        await using var test = await TestDatabase.CreateAsync();
        var service = new PedidoService(test.Db);

        var resultado = await service.CrearAsync(
            Input(test, TipoPedido.Apartado, (CanalVenta)999)
        );

        Assert.False(resultado.IsSuccess);
        Assert.Equal("Canal de venta no válido.", resultado.ErrorMessage);
        Assert.Empty(await test.Db.Pedidos.AsNoTracking().ToListAsync());
    }

    [Fact]
    public void FormularioManual_ConservaCanalSeleccionado()
    {
        var modelo = new PedidoFormModel
        {
            Fecha = new DateOnly(2026, 8, 31),
            TipoPedido = TipoPedido.Catalogo,
            CanalVenta = CanalVenta.Facebook,
            ClienteId = 7,
        };
        modelo.Detalles.Single().ProductoId = 11;
        modelo.Detalles.Single().PrecioUnitario = 125m;

        var input = modelo.ToInput("PED-FORM-CANAL");

        Assert.Equal(TipoPedido.Catalogo, input.TipoPedido);
        Assert.Equal(CanalVenta.Facebook, input.CanalVenta);
    }

    [Fact]
    public void Presentacion_MuestraEtiquetasAmigables()
    {
        Assert.Equal("Presencial", PedidoPresentacion.Canal(CanalVenta.Presencial));
        Assert.Equal("WhatsApp", PedidoPresentacion.Canal(CanalVenta.WhatsApp));
        Assert.Equal("Facebook", PedidoPresentacion.Canal(CanalVenta.Facebook));
        Assert.Equal("Web", PedidoPresentacion.Canal(CanalVenta.Web));
        Assert.Equal("Otro", PedidoPresentacion.Canal(CanalVenta.Otro));
    }

    [Fact]
    public void Venta_NoDuplicaCanalYVentaDirectaNoLoSolicita()
    {
        Assert.Null(typeof(Venta).GetProperty(nameof(CanalVenta)));
        Assert.Null(typeof(VentaInput).GetProperty(nameof(CanalVenta)));
        Assert.Null(typeof(VentaDto).GetProperty(nameof(CanalVenta)));
        Assert.Null(typeof(VentaDirectaFormModel).GetProperty(nameof(CanalVenta)));

        var formulario = File.ReadAllText(
            Path.Combine(
                BuscarRaizRepositorio(),
                "src",
                "ResellManager.Web",
                "Components",
                "Ventas",
                "VentaDirectaForm.razor"
            )
        );
        Assert.Contains("CanalVenta.Presencial", formulario);
        Assert.DoesNotContain("Canal de venta", formulario, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@bind-Value=\"Modelo.CanalVenta\"", formulario);
    }

    [Fact]
    public async Task Migracion_ConviertePedidoHistoricoACanalOtroYDejaColumnaRequerida()
    {
        var ruta = Path.Combine(
            Path.GetTempPath(),
            $"resellmanager-canal-{Guid.NewGuid():N}.db"
        );

        try
        {
            var options = new DbContextOptionsBuilder<ResellManagerDbContext>()
                .UseSqlite($"Data Source={ruta}")
                .Options;
            await using var db = new ResellManagerDbContext(options);
            var migrator = db.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(
                "20260821050046_SeparateInventoryReservationFromPhysicalState"
            );
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Clientes (Nombres, Telefono)
                VALUES ('Cliente histórico', '555-0199')
                """
            );
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Pedidos
                    (CodigoInterno, Fecha, TipoPedido, Estado, ClienteId)
                VALUES
                    ('PED-HISTORICO', '2026-08-31', 'Apartado', 'Pendiente', 1)
                """
            );

            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();

            var historico = await db.Pedidos.AsNoTracking()
                .SingleAsync(x => x.CodigoInterno == "PED-HISTORICO");
            Assert.Equal(CanalVenta.Otro, historico.CanalVenta);

            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info('Pedidos')";
            await using var reader = await command.ExecuteReaderAsync();
            var encontroCanal = false;
            while (await reader.ReadAsync())
            {
                if (!string.Equals(reader.GetString(1), "CanalVenta", StringComparison.Ordinal))
                    continue;

                encontroCanal = true;
                Assert.Equal(1L, reader.GetInt64(3));
                Assert.Equal("5", reader.GetValue(4).ToString());
            }
            Assert.True(encontroCanal);
            await db.Database.CloseConnectionAsync();
            SqliteConnection.ClearAllPools();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(ruta);
        }
    }

    private static PedidoInput Input(
        TestDatabase test,
        TipoPedido tipoPedido,
        CanalVenta canalVenta
    ) =>
        new(
            $"PED-CANAL-{tipoPedido}-{canalVenta}",
            new DateOnly(2026, 8, 31),
            tipoPedido,
            canalVenta,
            test.Cliente.Id,
            null,
            [new DetallePedidoInput(test.Producto.Id, 1, 100m, null)]
        );

    private static string BuscarRaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (
            directorio is not null
            && !File.Exists(Path.Combine(directorio.FullName, "ResellManager.sln"))
        )
            directorio = directorio.Parent;
        return directorio?.FullName
            ?? throw new InvalidOperationException("No fue posible localizar ResellManager.sln.");
    }
}
