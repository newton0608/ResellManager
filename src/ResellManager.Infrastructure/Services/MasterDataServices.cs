using Microsoft.EntityFrameworkCore;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;

namespace ResellManager.Infrastructure.Services;

public sealed class ClienteService(ResellManagerDbContext db) : IClienteService
{
    public async Task<ServiceResult<ClienteDto>> CrearAsync(
        ClienteInput input,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(input.Nombres) || string.IsNullOrWhiteSpace(input.Telefono))
            return ServiceResult<ClienteDto>.Failure("Nombres y teléfono son obligatorios.");
        var entity = new Cliente
        {
            Nombres = input.Nombres.Trim(),
            Apellidos = input.Apellidos?.Trim(),
            Telefono = input.Telefono.Trim(),
            Direccion = input.Direccion?.Trim(),
            Observaciones = input.Observaciones?.Trim(),
        };
        db.Clientes.Add(entity);
        await db.SaveChangesAsync(ct);
        return ServiceResult<ClienteDto>.Ok(Map(entity, 0));
    }

    public async Task<ServiceResult<ClienteDto>> EditarAsync(
        int id,
        ClienteInput input,
        CancellationToken ct = default
    )
    {
        var entity = await db.Clientes.FindAsync([id], ct);
        if (entity is null)
            return ServiceResult<ClienteDto>.Failure("Cliente no encontrado.");
        if (string.IsNullOrWhiteSpace(input.Nombres) || string.IsNullOrWhiteSpace(input.Telefono))
            return ServiceResult<ClienteDto>.Failure("Nombres y teléfono son obligatorios.");
        entity.Nombres = input.Nombres.Trim();
        entity.Apellidos = input.Apellidos?.Trim();
        entity.Telefono = input.Telefono.Trim();
        entity.Direccion = input.Direccion?.Trim();
        entity.Observaciones = input.Observaciones?.Trim();
        await db.SaveChangesAsync(ct);
        return ServiceResult<ClienteDto>.Ok(Map(entity, await SaldoAsync(id, ct)));
    }

    public async Task<ServiceResult<ClienteDto>> ObtenerPorIdAsync(
        int id,
        CancellationToken ct = default
    )
    {
        var entity = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return entity is null
            ? ServiceResult<ClienteDto>.Failure("Cliente no encontrado.")
            : ServiceResult<ClienteDto>.Ok(Map(entity, await SaldoAsync(id, ct)));
    }

    public Task<IReadOnlyList<ClienteDto>> ListarAsync(CancellationToken ct = default) =>
        Query()
            .OrderBy(x => x.Nombres)
            .ThenBy(x => x.Apellidos)
            .ToListAsync(ct)
            .ContinueWith<IReadOnlyList<ClienteDto>>(x => x.Result, ct);

    public Task<IReadOnlyList<ClienteDto>> BuscarAsync(
        string termino,
        CancellationToken ct = default
    )
    {
        termino = termino.Trim();
        return Query()
            .Where(x =>
                x.Nombres.Contains(termino)
                || (x.Apellidos != null && x.Apellidos.Contains(termino))
                || x.Telefono.Contains(termino)
            )
            .OrderBy(x => x.Nombres)
            .ToListAsync(ct)
            .ContinueWith<IReadOnlyList<ClienteDto>>(x => x.Result, ct);
    }

    public async Task<ServiceResult<decimal>> ObtenerSaldoAsync(
        int clienteId,
        CancellationToken ct = default
    ) =>
        await db.Clientes.AnyAsync(x => x.Id == clienteId, ct)
            ? ServiceResult<decimal>.Ok(await SaldoAsync(clienteId, ct))
            : ServiceResult<decimal>.Failure("Cliente no encontrado.");

    public async Task<ServiceResult<ClienteHistorialDto>> ObtenerHistorialAsync(
        int clienteId,
        CancellationToken ct = default
    )
    {
        var clienteEntity = await db
            .Clientes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == clienteId, ct);
        if (clienteEntity is null)
            return ServiceResult<ClienteHistorialDto>.Failure("Cliente no encontrado.");
        var cliente = Map(clienteEntity, await SaldoAsync(clienteId, ct));

        var ventasEntities = await db
            .Ventas.AsNoTracking()
            .Include(x => x.Pedido)
                .ThenInclude(x => x.Cliente)
            .Include(x => x.Detalles)
                .ThenInclude(x => x.Producto)
            .Include(x => x.Detalles)
                .ThenInclude(x => x.UnidadInventario)
                    .ThenInclude(x => x!.Producto)
            .Where(x => x.Pedido.ClienteId == clienteId)
            .OrderByDescending(x => x.Fecha)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
        var ventas = ventasEntities.Select(MapVenta).ToList();

        var pagos = await db
            .Pagos.AsNoTracking()
            .Where(x => x.ClienteId == clienteId)
            .OrderByDescending(x => x.Fecha)
            .ThenByDescending(x => x.Id)
            .Select(x => new PagoDto(
                x.Id,
                x.ClienteId,
                x.Cliente.Nombres + (x.Cliente.Apellidos == null ? "" : " " + x.Cliente.Apellidos),
                x.Fecha,
                x.Monto,
                x.MetodoPago,
                x.Referencia,
                x.Observaciones
            ))
            .ToListAsync(ct);

        return ServiceResult<ClienteHistorialDto>.Ok(
            new ClienteHistorialDto(cliente, ventas, pagos)
        );
    }

    private static VentaDto MapVenta(Venta x) =>
        new(
            x.Id,
            x.CodigoInterno,
            x.Fecha,
            x.Estado,
            x.Observaciones,
            x.PedidoId,
            x.Pedido.ClienteId,
            x.Pedido.Cliente.Nombres
                + (x.Pedido.Cliente.Apellidos == null ? "" : " " + x.Pedido.Cliente.Apellidos),
            x.Detalles.Sum(d => d.PrecioFinal),
            x.Detalles.Select(d => new DetalleVentaDto(
                    d.Id,
                    d.UnidadInventarioId,
                    d.UnidadInventario?.CodigoInterno,
                    d.ProductoId ?? d.UnidadInventario!.ProductoId,
                    d.Producto?.Nombre ?? d.UnidadInventario!.Producto.Nombre,
                    d.CostoUnitario ?? d.UnidadInventario!.Costo,
                    d.PrecioFinal,
                    d.Observaciones
                ))
                .ToList()
        );

    private IQueryable<ClienteDto> Query() =>
        db
            .Clientes.AsNoTracking()
            .Select(c => new ClienteDto(
                c.Id,
                c.Nombres,
                c.Apellidos,
                c.Telefono,
                c.Direccion,
                c.Observaciones,
                (
                    c.Pedidos.Where(p =>
                            p.Venta != null && p.Venta.Estado == EstadoVenta.Registrada
                        )
                        .SelectMany(p => p.Venta!.Detalles)
                        .Sum(d => (decimal?)d.PrecioFinal)
                    ?? 0m
                ) - (c.Pagos.Sum(p => (decimal?)p.Monto) ?? 0m)
            ));

    private async Task<decimal> SaldoAsync(int id, CancellationToken ct)
    {
        var ventas = await db
            .DetallesVenta.AsNoTracking()
            .Where(x => x.Venta.Estado == EstadoVenta.Registrada && x.Venta.Pedido.ClienteId == id)
            .Select(x => x.PrecioFinal)
            .ToListAsync(ct);
        var pagos = await db
            .Pagos.AsNoTracking()
            .Where(x => x.ClienteId == id)
            .Select(x => x.Monto)
            .ToListAsync(ct);
        return ventas.Sum() - pagos.Sum();
    }

    private static ClienteDto Map(Cliente x, decimal saldo) =>
        new(x.Id, x.Nombres, x.Apellidos, x.Telefono, x.Direccion, x.Observaciones, saldo);
}

public sealed class CategoriaService(ResellManagerDbContext db) : ICategoriaService
{
    public async Task<ServiceResult<CategoriaDto>> CrearAsync(
        CategoriaInput input,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(input.Nombre))
            return ServiceResult<CategoriaDto>.Failure("El nombre es obligatorio.");
        var x = new Categoria
        {
            Nombre = input.Nombre.Trim(),
            Observaciones = input.Observaciones?.Trim(),
        };
        db.Categorias.Add(x);
        await db.SaveChangesAsync(ct);
        return ServiceResult<CategoriaDto>.Ok(Map(x));
    }

    public async Task<ServiceResult<CategoriaDto>> EditarAsync(
        int id,
        CategoriaInput input,
        CancellationToken ct = default
    )
    {
        var x = await db.Categorias.FindAsync([id], ct);
        if (x is null)
            return ServiceResult<CategoriaDto>.Failure("Categoría no encontrada.");
        if (string.IsNullOrWhiteSpace(input.Nombre))
            return ServiceResult<CategoriaDto>.Failure("El nombre es obligatorio.");
        x.Nombre = input.Nombre.Trim();
        x.Observaciones = input.Observaciones?.Trim();
        await db.SaveChangesAsync(ct);
        return ServiceResult<CategoriaDto>.Ok(Map(x));
    }

    public async Task<ServiceResult<CategoriaDto>> ObtenerPorIdAsync(
        int id,
        CancellationToken ct = default
    )
    {
        var x = await db.Categorias.AsNoTracking().FirstOrDefaultAsync(y => y.Id == id, ct);
        return x is null
            ? ServiceResult<CategoriaDto>.Failure("Categoría no encontrada.")
            : ServiceResult<CategoriaDto>.Ok(Map(x));
    }

    public async Task<IReadOnlyList<CategoriaDto>> ListarAsync(CancellationToken ct = default) =>
        await db
            .Categorias.AsNoTracking()
            .OrderBy(x => x.Nombre)
            .Select(x => Map(x))
            .ToListAsync(ct);

    private static CategoriaDto Map(Categoria x) => new(x.Id, x.Nombre, x.Observaciones);
}

public sealed class ProductoService(ResellManagerDbContext db) : IProductoService
{
    public async Task<ServiceResult<ProductoDto>> CrearAsync(
        ProductoInput input,
        CancellationToken ct = default
    )
    {
        var error = await Validar(input, null, ct);
        if (error is not null)
            return ServiceResult<ProductoDto>.Failure(error);
        var x = new Producto();
        Apply(x, input);
        db.Productos.Add(x);
        await db.SaveChangesAsync(ct);
        return await ObtenerPorIdAsync(x.Id, ct);
    }

    public async Task<ServiceResult<ProductoDto>> EditarAsync(
        int id,
        ProductoInput input,
        CancellationToken ct = default
    )
    {
        var x = await db.Productos.FindAsync([id], ct);
        if (x is null)
            return ServiceResult<ProductoDto>.Failure("Producto no encontrado.");
        var error = await Validar(input, id, ct);
        if (error is not null)
            return ServiceResult<ProductoDto>.Failure(error);
        Apply(x, input);
        await db.SaveChangesAsync(ct);
        return await ObtenerPorIdAsync(id, ct);
    }

    public async Task<ServiceResult<ProductoDto>> ObtenerPorIdAsync(
        int id,
        CancellationToken ct = default
    )
    {
        var x = await Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        return x is null
            ? ServiceResult<ProductoDto>.Failure("Producto no encontrado.")
            : ServiceResult<ProductoDto>.Ok(x);
    }

    public async Task<IReadOnlyList<ProductoDto>> ListarAsync(CancellationToken ct = default) =>
        await Query().OrderBy(x => x.Nombre).ToListAsync(ct);

    public async Task<IReadOnlyList<ProductoDto>> BuscarAsync(
        string termino,
        CancellationToken ct = default
    )
    {
        termino = termino.Trim();
        return await Query()
            .Where(x =>
                x.Nombre.Contains(termino)
                || x.CodigoInterno.Contains(termino)
                || (x.CodigoBarras != null && x.CodigoBarras.Contains(termino))
            )
            .OrderBy(x => x.Nombre)
            .ToListAsync(ct);
    }

    private async Task<string?> Validar(ProductoInput x, int? id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(x.CodigoInterno) || string.IsNullOrWhiteSpace(x.Nombre))
            return "Código interno y nombre son obligatorios.";
        if (!await db.Categorias.AnyAsync(c => c.Id == x.CategoriaId, ct))
            return "Categoría no encontrada.";
        if (
            await db.Productos.AnyAsync(
                p => p.CodigoInterno == x.CodigoInterno.Trim() && p.Id != id,
                ct
            )
        )
            return "El código interno ya está registrado.";
        return null;
    }

    private static void Apply(Producto x, ProductoInput i)
    {
        x.CodigoInterno = i.CodigoInterno.Trim();
        x.CodigoBarras = i.CodigoBarras?.Trim();
        x.Nombre = i.Nombre.Trim();
        x.Descripcion = i.Descripcion?.Trim();
        x.Marca = i.Marca?.Trim();
        x.Modelo = i.Modelo?.Trim();
        x.Color = i.Color?.Trim();
        x.Talla = i.Talla?.Trim();
        x.CategoriaId = i.CategoriaId;
    }

    private IQueryable<ProductoDto> Query() =>
        db
            .Productos.AsNoTracking()
            .Select(x => new ProductoDto(
                x.Id,
                x.CodigoInterno,
                x.CodigoBarras,
                x.Nombre,
                x.Descripcion,
                x.Marca,
                x.Modelo,
                x.Color,
                x.Talla,
                x.CategoriaId,
                x.Categoria.Nombre
            ));
}

public sealed class ProveedorService(ResellManagerDbContext db) : IProveedorService
{
    public async Task<ServiceResult<ProveedorDto>> CrearAsync(
        ProveedorInput input,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(input.Nombre))
            return ServiceResult<ProveedorDto>.Failure("El nombre es obligatorio.");
        var x = new Proveedor();
        Apply(x, input);
        db.Proveedores.Add(x);
        await db.SaveChangesAsync(ct);
        return ServiceResult<ProveedorDto>.Ok(Map(x));
    }

    public async Task<ServiceResult<ProveedorDto>> EditarAsync(
        int id,
        ProveedorInput input,
        CancellationToken ct = default
    )
    {
        var x = await db.Proveedores.FindAsync([id], ct);
        if (x is null)
            return ServiceResult<ProveedorDto>.Failure("Proveedor no encontrado.");
        if (string.IsNullOrWhiteSpace(input.Nombre))
            return ServiceResult<ProveedorDto>.Failure("El nombre es obligatorio.");
        Apply(x, input);
        await db.SaveChangesAsync(ct);
        return ServiceResult<ProveedorDto>.Ok(Map(x));
    }

    public async Task<ServiceResult<ProveedorDto>> ObtenerPorIdAsync(
        int id,
        CancellationToken ct = default
    )
    {
        var x = await db.Proveedores.AsNoTracking().FirstOrDefaultAsync(y => y.Id == id, ct);
        return x is null
            ? ServiceResult<ProveedorDto>.Failure("Proveedor no encontrado.")
            : ServiceResult<ProveedorDto>.Ok(Map(x));
    }

    public async Task<IReadOnlyList<ProveedorDto>> ListarAsync(CancellationToken ct = default) =>
        await db
            .Proveedores.AsNoTracking()
            .OrderBy(x => x.Nombre)
            .Select(x => Map(x))
            .ToListAsync(ct);

    private static void Apply(Proveedor x, ProveedorInput i)
    {
        x.Nombre = i.Nombre.Trim();
        x.Telefono = i.Telefono?.Trim();
        x.CodigoPais = i.CodigoPais?.Trim();
        x.Descripcion = i.Descripcion?.Trim();
    }

    private static ProveedorDto Map(Proveedor x) =>
        new(x.Id, x.Nombre, x.Telefono, x.CodigoPais, x.Descripcion);
}
