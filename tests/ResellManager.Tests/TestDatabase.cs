using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection connection;

    private TestDatabase(SqliteConnection connection, ResellManagerDbContext db)
    {
        this.connection = connection;
        Db = db;
    }

    public ResellManagerDbContext Db { get; }
    public Categoria Categoria { get; private set; } = null!;
    public Producto Producto { get; private set; } = null!;
    public Proveedor Proveedor { get; private set; } = null!;
    public Cliente Cliente { get; private set; } = null!;

    public static async Task<TestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ResellManagerDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new ResellManagerDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var result = new TestDatabase(connection, db);
        await result.SeedAsync();
        return result;
    }

    public CompraInput Compra(
        OrigenCompra origen,
        string codigo,
        DateOnly? fechaIngreso = null,
        int cantidad = 1,
        Producto? producto = null
    ) =>
        new(
            codigo,
            new DateOnly(2026, 1, 10),
            fechaIngreso,
            origen,
            Proveedor.Id,
            null,
            [new DetalleCompraInput((producto ?? Producto).Id, cantidad, 40m)],
            null
        );

    public async Task<Pedido> CrearPedidoAsync(
        TipoPedido tipo,
        string codigo,
        int cantidad = 1,
        Producto? producto = null
    )
    {
        var pedido = new Pedido
        {
            CodigoInterno = codigo,
            Fecha = new DateOnly(2026, 2, 1),
            TipoPedido = tipo,
            Estado = EstadoPedido.Pendiente,
            ClienteId = Cliente.Id,
            Detalles =
            [
                new DetallePedido
                {
                    ProductoId = (producto ?? Producto).Id,
                    Cantidad = cantidad,
                    PrecioUnitario = 100m,
                },
            ],
        };
        Db.Pedidos.Add(pedido);
        await Db.SaveChangesAsync();
        return pedido;
    }

    public async Task<UnidadInventario> CrearUnidadDisponibleAsync(
        string codigo,
        Producto? producto = null
    )
    {
        var result = await new CompraService(Db).RegistrarAsync(
            Compra(
                OrigenCompra.CompraLocal,
                codigo,
                new DateOnly(2026, 1, 11),
                producto: producto
            )
        );
        Assert.True(result.IsSuccess, result.ErrorMessage);
        return await Db.UnidadesInventario.SingleAsync(x =>
            x.DetalleCompra.Compra.CodigoInterno == codigo
        );
    }

    public async Task<UnidadInventario> CrearUnidadImportadaAsync(
        string codigo,
        Producto? producto = null
    )
    {
        var result = await new CompraService(Db).RegistrarAsync(
            Compra(OrigenCompra.Importacion, codigo, producto: producto)
        );
        Assert.True(result.IsSuccess, result.ErrorMessage);
        return await Db.UnidadesInventario.SingleAsync(x =>
            x.DetalleCompra.Compra.CodigoInterno == codigo
        );
    }

    public async Task<Venta> CrearVentaCatalogoAsync(
        string pedidoCodigo,
        string ventaCodigo,
        decimal precio = 100m,
        decimal costo = 40m
    )
    {
        var pedido = await CrearPedidoAsync(TipoPedido.Catalogo, pedidoCodigo);
        var result = await new VentaService(Db).RegistrarDesdePedidoAsync(
            new VentaInput(
                pedido.Id,
                ventaCodigo,
                new DateOnly(2026, 2, 2),
                null,
                [new DetalleVentaInput(null, Producto.Id, costo, precio, null)]
            )
        );
        Assert.True(result.IsSuccess, result.ErrorMessage);
        return await Db.Ventas.Include(x => x.Detalles).SingleAsync(x => x.Id == result.Value!.Id);
    }

    public async Task<Producto> CrearProductoAsync(string codigo, decimal precioSugerido = 100m)
    {
        var producto = new Producto
        {
            CodigoInterno = codigo,
            Nombre = $"Producto {codigo}",
            PrecioSugerido = precioSugerido,
            CategoriaId = Categoria.Id,
        };
        Db.Productos.Add(producto);
        await Db.SaveChangesAsync();
        return producto;
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await connection.DisposeAsync();
    }

    private async Task SeedAsync()
    {
        Categoria = new Categoria { Nombre = "General" };
        Proveedor = new Proveedor { Nombre = "Proveedor" };
        Cliente = new Cliente { Nombres = "Cliente", Telefono = "555-0100" };
        Producto = new Producto
        {
            CodigoInterno = "PROD-1",
            Nombre = "Producto",
            PrecioSugerido = 120m,
            Categoria = Categoria,
        };
        Db.AddRange(Categoria, Proveedor, Cliente, Producto);
        await Db.SaveChangesAsync();
    }
}
