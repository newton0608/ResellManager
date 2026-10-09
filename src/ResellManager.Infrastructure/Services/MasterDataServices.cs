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

    public async Task<IReadOnlyList<ClienteDto>> ListarAsync(CancellationToken ct = default)
    {
        var clientes = await db
            .Clientes.AsNoTracking()
            .OrderBy(x => x.Nombres)
            .ThenBy(x => x.Apellidos)
            .ToListAsync(ct);
        return await MapConSaldosAsync(clientes, ct);
    }

    public async Task<IReadOnlyList<ClienteDto>> BuscarAsync(
        string termino,
        CancellationToken ct = default,
        int? limite = null
    )
    {
        termino = termino.Trim().ToLowerInvariant();
        var consulta = db
            .Clientes.AsNoTracking()
            .Where(x =>
                (x.Nombres + " " + (x.Apellidos ?? "")).ToLower().Contains(termino)
                || x.Telefono.Contains(termino)
            )
            .OrderByDescending(x => x.Telefono == termino)
            .ThenBy(x => x.Nombres)
            .ThenBy(x => x.Apellidos)
            .ThenBy(x => x.Id).AsQueryable();
        if (limite.HasValue) consulta = consulta.Take(Math.Clamp(limite.Value, 1, 50));
        var clientes = await consulta.ToListAsync(ct);
        return await MapConSaldosAsync(clientes, ct);
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

    internal static VentaDto MapVenta(Venta x) =>
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

    private async Task<IReadOnlyList<ClienteDto>> MapConSaldosAsync(
        IReadOnlyCollection<Cliente> clientes,
        CancellationToken ct
    )
    {
        if (clientes.Count == 0)
            return [];

        var ids = clientes.Select(x => x.Id).ToArray();
        var ventas = await db
            .DetallesVenta.AsNoTracking()
            .Where(x =>
                x.Venta.Estado == EstadoVenta.Registrada
                && ids.Contains(x.Venta.Pedido.ClienteId)
            )
            .Select(x => new { x.Venta.Pedido.ClienteId, Monto = x.PrecioFinal })
            .ToListAsync(ct);
        var pagos = await db
            .Pagos.AsNoTracking()
            .Where(x => ids.Contains(x.ClienteId))
            .Select(x => new { x.ClienteId, x.Monto })
            .ToListAsync(ct);
        var ventasPorCliente = ventas
            .GroupBy(x => x.ClienteId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Monto));
        var pagosPorCliente = pagos
            .GroupBy(x => x.ClienteId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Monto));

        return clientes
            .Select(x =>
                Map(
                    x,
                    ventasPorCliente.GetValueOrDefault(x.Id)
                        - pagosPorCliente.GetValueOrDefault(x.Id)
                )
            )
            .ToList();
    }

    private async Task<decimal> SaldoAsync(int id, CancellationToken ct)
        => await SaldoConsultas.CalcularAsync(db, id, ct);

    private static ClienteDto Map(Cliente x, decimal saldo) =>
        new(x.Id, x.Nombres, x.Apellidos, x.Telefono, x.Direccion, x.Observaciones, saldo);
}

public sealed class CategoriaService(ResellManagerDbContext db) : ICategoriaService
{
    public async Task<ServiceResult<CategoriaDto>> CrearAsync(
        CategoriaInput input, CancellationToken ct = default)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var error = await ValidarAsync(input, null, ct);
        if (error is not null) return ServiceResult<CategoriaDto>.Failure(error);
        var categoria = new Categoria
        {
            Nombre = input.Nombre.Trim(),
            Observaciones = input.Observaciones?.Trim(),
            CategoriaPadreId = input.CategoriaPadreId
        };
        db.Categorias.Add(categoria);
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        return await ObtenerPorIdAsync(categoria.Id, ct);
    }

    public async Task<ServiceResult<CategoriaDto>> EditarAsync(
        int id, CategoriaInput input, CancellationToken ct = default)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var categoria = await db.Categorias.FindAsync([id], ct);
        if (categoria is null) return ServiceResult<CategoriaDto>.Failure("Categoría no encontrada.");
        var error = await ValidarAsync(input, id, ct);
        if (error is not null) return ServiceResult<CategoriaDto>.Failure(error);
        categoria.Nombre = input.Nombre.Trim();
        categoria.Observaciones = input.Observaciones?.Trim();
        categoria.CategoriaPadreId = input.CategoriaPadreId;
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        return await ObtenerPorIdAsync(id, ct);
    }

    public async Task<ServiceResult<CategoriaDto>> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var categoria = await Query(db.Categorias.Where(x => x.Id == id)).FirstOrDefaultAsync(ct);
        return categoria is null
            ? ServiceResult<CategoriaDto>.Failure("Categoría no encontrada.")
            : ServiceResult<CategoriaDto>.Ok(categoria);
    }

    public async Task<IReadOnlyList<CategoriaDto>> ListarAsync(CancellationToken ct = default) =>
        await Query(db.Categorias.OrderBy(x => x.CategoriaPadre == null ? x.Nombre : x.CategoriaPadre.Nombre)
            .ThenBy(x => x.CategoriaPadreId.HasValue).ThenBy(x => x.Nombre).ThenBy(x => x.Id))
            .ToListAsync(ct);

    private async Task<string?> ValidarAsync(CategoriaInput input, int? id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Nombre)) return "El nombre es obligatorio.";
        if (!input.CategoriaPadreId.HasValue) return null;
        if (input.CategoriaPadreId == id) return "Una categoría no puede ser su propia categoría padre.";
        var padre = await db.Categorias.AsNoTracking()
            .Where(x => x.Id == input.CategoriaPadreId.Value)
            .Select(x => new { x.CategoriaPadreId }).FirstOrDefaultAsync(ct);
        if (padre is null) return "Categoría padre no encontrada.";
        if (padre.CategoriaPadreId.HasValue) return "La categoría padre debe ser una raíz; solo se permiten dos niveles.";
        if (id.HasValue && await db.Categorias.AnyAsync(x => x.CategoriaPadreId == id.Value, ct))
            return "Una categoría con subcategorías no puede convertirse en hija.";
        return null;
    }

    private static IQueryable<CategoriaDto> Query(IQueryable<Categoria> categorias) => categorias.AsNoTracking()
        .Select(x => new CategoriaDto(x.Id, x.Nombre, x.Observaciones, x.CategoriaPadreId,
            x.CategoriaPadre == null ? null : x.CategoriaPadre.Nombre, x.Subcategorias.Any()));
}

public sealed class ProductoService(ResellManagerDbContext db) : IProductoService, IConsultaProductoCodigoBarras
{
    public async Task<ServiceResult<ProductoDto>> CrearAsync(
        ProductoInput input,
        CancellationToken ct = default
    )
    {
        var x = new Producto { CodigoInterno = CodigosInternos.CrearCodigoProducto() };
        var error = await Validar(input, x.CodigoInterno, null, ct);
        if (error is not null)
            return ServiceResult<ProductoDto>.Failure(error);
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
        var error = await Validar(input, x.CodigoInterno, id, ct);
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
        var x = await Query(db.Productos.Where(x => x.Id == id)).FirstOrDefaultAsync(ct);
        return x is null
            ? ServiceResult<ProductoDto>.Failure("Producto no encontrado.")
            : ServiceResult<ProductoDto>.Ok(x);
    }

    public async Task<IReadOnlyList<ProductoDto>> ListarAsync(CancellationToken ct = default) =>
        await Query(db.Productos.OrderBy(x => x.Nombre)).ToListAsync(ct);

    public async Task<IReadOnlyList<ProductoDto>> BuscarAsync(
        string termino,
        CancellationToken ct = default,
        int? limite = null,
        int? categoriaId = null
    )
    {
        termino = termino.Trim();
        var terminoMinusculas = termino.ToLowerInvariant();
        var productos = db.Productos.Where(x =>
            x.Nombre.ToLower().Contains(terminoMinusculas)
            || x.CodigoInterno.ToLower().Contains(terminoMinusculas)
            || (x.CodigoBarras != null && x.CodigoBarras.ToLower().Contains(terminoMinusculas))
        );
        if (categoriaId.HasValue) productos = productos.Where(x => x.CategoriaId == categoriaId.Value);
        IQueryable<Producto> ordenados = productos
            .OrderByDescending(x => x.CodigoInterno.ToLower() == terminoMinusculas
                || (x.CodigoBarras != null && x.CodigoBarras.ToLower() == terminoMinusculas))
            .ThenBy(x => x.Nombre).ThenBy(x => x.Id);
        if (limite.HasValue)
            ordenados = ordenados.Take(Math.Clamp(limite.Value, 1, 50));
        return await Query(ordenados).ToListAsync(ct);
    }

    public async Task<ProductoDto?> ObtenerPorCodigoBarrasAsync(string codigo, CancellationToken ct = default) =>
        await Query(db.Productos.Where(x => x.CodigoBarras == codigo).OrderBy(x => x.Id)).FirstOrDefaultAsync(ct);

    private async Task<string?> Validar(ProductoInput x, string codigoInterno, int? id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(x.Nombre))
            return "El nombre es obligatorio.";
        if (x.PrecioSugerido < 0)
            return "El precio sugerido no puede ser negativo.";
        if (x.ContenidoMl is <= 0)
            return "El contenido en ml debe ser mayor que cero.";
        if (x.PesoGramos is <= 0)
            return "El peso en gramos debe ser mayor que cero.";
        if (x.ContenidoMl.HasValue && x.PesoGramos.HasValue)
            return "Un producto no puede tener contenido en ml y peso en gramos simultáneamente.";
        if (x.Presentacion is { Length: > 100 })
            return "La presentación no puede superar los 100 caracteres.";
        var categoria = await db.Categorias.AsNoTracking().Where(c => c.Id == x.CategoriaId)
            .Select(c => new { c.Id, c.CategoriaPadreId,
                PadreDePadreId = c.CategoriaPadre == null ? (int?)null : c.CategoriaPadre.CategoriaPadreId })
            .SingleOrDefaultAsync(ct);
        if (categoria is null) return "Categoría no encontrada.";
        if (categoria.CategoriaPadreId == categoria.Id || categoria.PadreDePadreId.HasValue)
            return "La categoría del producto debe ser una raíz o una subcategoría de una raíz.";
        if (x.CategoriaPrincipalId.HasValue)
        {
            var principal = await db.Categorias.AsNoTracking()
                .Where(c => c.Id == x.CategoriaPrincipalId.Value)
                .Select(c => new { c.Id, c.CategoriaPadreId }).SingleOrDefaultAsync(ct);
            if (principal is null || principal.CategoriaPadreId.HasValue)
                return "Selecciona una categoría principal válida.";
            if (categoria.Id != principal.Id && categoria.CategoriaPadreId != principal.Id)
                return "La subcategoría no pertenece a la categoría principal seleccionada.";
        }
        if (
            await db.Productos.AnyAsync(
                p => p.CodigoInterno == codigoInterno && p.Id != id,
                ct
            )
        )
            return "El código interno ya está registrado.";
        return null;
    }

    private static void Apply(Producto x, ProductoInput i)
    {
        x.CodigoBarras = i.CodigoBarras;
        x.Nombre = i.Nombre.Trim();
        x.Descripcion = i.Descripcion?.Trim();
        x.Marca = i.Marca?.Trim();
        x.Modelo = i.Modelo?.Trim();
        x.Color = i.Color?.Trim();
        x.Talla = i.Talla?.Trim();
        x.ContenidoMl = i.ContenidoMl;
        x.PesoGramos = i.PesoGramos;
        x.Presentacion = i.Presentacion?.Trim();
        x.PrecioSugerido = i.PrecioSugerido;
        x.CategoriaId = i.CategoriaId;
    }

    private IQueryable<ProductoDto> Query(IQueryable<Producto>? productos = null) =>
        (productos ?? db.Productos)
            .AsNoTracking()
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
                x.PrecioSugerido,
                x.CategoriaId,
                x.Categoria.Nombre,
                x.ContenidoMl,
                x.PesoGramos,
                x.Presentacion,
                x.ImagenPrincipalRuta
            ));
}

public sealed class ProveedorService(ResellManagerDbContext db) : IProveedorService
{
    public async Task<IReadOnlyList<ProveedorDto>> BuscarAsync(string termino, CancellationToken ct = default, int? limite = 12)
    {
        var texto = termino.Trim().ToLowerInvariant();
        IQueryable<Proveedor> proveedores = db.Proveedores.AsNoTracking()
            .Where(x => x.Nombre.ToLower().Contains(texto)
                || (x.Telefono != null && x.Telefono.Contains(texto)))
            .OrderByDescending(x => x.Telefono == texto)
            .ThenBy(x => x.Nombre).ThenBy(x => x.Id);
        if (limite.HasValue) proveedores = proveedores.Take(Math.Clamp(limite.Value, 1, 50));
        return await proveedores.Select(x => Map(x)).ToListAsync(ct);
    }

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
