using Microsoft.EntityFrameworkCore;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;

namespace ResellManager.Infrastructure.Services;

public sealed class CompraService(ResellManagerDbContext db) : ICompraService
{
    public async Task<ServiceResult<CompraDto>> RegistrarAsync(CompraInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.CodigoInterno)) return ServiceResult<CompraDto>.Failure("El código interno es obligatorio.");
        if (input.Detalles.Count == 0) return ServiceResult<CompraDto>.Failure("La compra debe incluir detalles.");
        if (!await db.Proveedores.AnyAsync(x => x.Id == input.ProveedorId, ct)) return ServiceResult<CompraDto>.Failure("Proveedor no encontrado.");
        if (await db.Compras.AnyAsync(x => x.CodigoInterno == input.CodigoInterno.Trim(), ct)) return ServiceResult<CompraDto>.Failure("El código de compra ya está registrado.");
        if (input.Detalles.Any(x => x.Cantidad <= 0 || x.CostoUnitario < 0 || x.PrecioLista < 0)) return ServiceResult<CompraDto>.Failure("Cantidad, costo y precio de los detalles no son válidos.");
        var productIds = input.Detalles.Select(x => x.ProductoId).Distinct().ToArray();
        if (await db.Productos.CountAsync(x => productIds.Contains(x.Id), ct) != productIds.Length) return ServiceResult<CompraDto>.Failure("Uno o más productos no existen.");
        if (input.Comprobante is not null && string.IsNullOrWhiteSpace(input.Comprobante.RutaDocumento)) return ServiceResult<CompraDto>.Failure("La ruta del comprobante es obligatoria.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var compra = new Compra { CodigoInterno = input.CodigoInterno.Trim(), FechaCompra = input.FechaCompra, Origen = input.Origen, ProveedorId = input.ProveedorId, Observaciones = input.Observaciones?.Trim(), Total = input.Detalles.Sum(x => x.Cantidad * x.CostoUnitario) };
        var detailNumber = 0;
        foreach (var item in input.Detalles)
        {
            detailNumber++;
            var detalle = new DetalleCompra { ProductoId = item.ProductoId, Cantidad = item.Cantidad, CostoUnitario = item.CostoUnitario };
            for (var i = 1; i <= item.Cantidad; i++)
                detalle.UnidadesInventario.Add(new UnidadInventario { CodigoInterno = $"{compra.CodigoInterno}-{detailNumber:D2}-{i:D3}", Estado = EstadoUnidadInventario.Comprada, FechaIngreso = input.FechaCompra, Costo = item.CostoUnitario, PrecioLista = item.PrecioLista, ProductoId = item.ProductoId });
            compra.Detalles.Add(detalle);
        }
        if (input.Comprobante is not null) compra.Comprobante = new ComprobanteCompra { NumeroDocumento = input.Comprobante.NumeroDocumento?.Trim(), Fecha = input.Comprobante.Fecha, RutaDocumento = input.Comprobante.RutaDocumento.Trim(), Observaciones = input.Comprobante.Observaciones?.Trim() };
        db.Compras.Add(compra);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException) { await transaction.RollbackAsync(ct); return ServiceResult<CompraDto>.Failure("No se pudo registrar la compra; verifique que los códigos generados no estén duplicados."); }
        return await ObtenerPorIdAsync(compra.Id, ct);
    }

    public async Task<ServiceResult<CompraDto>> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    { var x = await Query().FirstOrDefaultAsync(x => x.Id == id, ct); return x is null ? ServiceResult<CompraDto>.Failure("Compra no encontrada.") : ServiceResult<CompraDto>.Ok(x); }
    public async Task<IReadOnlyList<CompraDto>> ListarAsync(CancellationToken ct = default) => await Query().OrderByDescending(x => x.FechaCompra).ThenByDescending(x => x.Id).ToListAsync(ct);
    private IQueryable<CompraDto> Query() => db.Compras.AsNoTracking().Select(x => new CompraDto(x.Id, x.CodigoInterno, x.FechaCompra, x.Origen, x.Total, x.Observaciones, x.ProveedorId, x.Proveedor.Nombre,
        x.Detalles.Select(d => new DetalleCompraDto(d.Id, d.ProductoId, d.Producto.Nombre, d.Cantidad, d.CostoUnitario, d.UnidadesInventario.Select(u => u.PrecioLista).FirstOrDefault(), d.Cantidad * d.CostoUnitario)).ToList(), x.Comprobante != null ? x.Comprobante.RutaDocumento : null));
}

public sealed class InventarioService(ResellManagerDbContext db) : IInventarioService
{
    public async Task<IReadOnlyList<UnidadInventarioDto>> ListarAsync(CancellationToken ct = default) => await Query().OrderByDescending(x => x.FechaIngreso).ThenBy(x => x.CodigoInterno).ToListAsync(ct);
    public async Task<IReadOnlyList<UnidadInventarioDto>> ListarDisponiblesAsync(CancellationToken ct = default) => await Query().Where(x => x.Estado == EstadoUnidadInventario.Disponible).OrderBy(x => x.Producto).ToListAsync(ct);
    public async Task<IReadOnlyList<UnidadInventarioDto>> BuscarAsync(string? termino, EstadoUnidadInventario? estado, CancellationToken ct = default)
    { var query = Query(); if (!string.IsNullOrWhiteSpace(termino)) { termino = termino.Trim(); query = query.Where(x => x.Producto.Contains(termino) || x.CodigoProducto.Contains(termino) || x.CodigoInterno.Contains(termino)); } if (estado.HasValue) query = query.Where(x => x.Estado == estado.Value); return await query.OrderBy(x => x.Producto).ThenBy(x => x.CodigoInterno).ToListAsync(ct); }
    public async Task<ServiceResult<UnidadInventarioDto>> CambiarEstadoAsync(int id, EstadoUnidadInventario estado, CancellationToken ct = default)
    {
        var x = await db.UnidadesInventario.FindAsync([id], ct); if (x is null) return ServiceResult<UnidadInventarioDto>.Failure("Unidad de inventario no encontrada.");
        if ((x.Estado is EstadoUnidadInventario.Vendida or EstadoUnidadInventario.Entregada) && estado == EstadoUnidadInventario.Disponible) return ServiceResult<UnidadInventarioDto>.Failure("Una unidad vendida o entregada no puede volver a disponible.");
        if (x.Estado == EstadoUnidadInventario.Entregada && estado != EstadoUnidadInventario.Entregada) return ServiceResult<UnidadInventarioDto>.Failure("Una unidad entregada no puede cambiar de estado.");
        if (x.Estado == EstadoUnidadInventario.Vendida && estado is not (EstadoUnidadInventario.Vendida or EstadoUnidadInventario.Entregada)) return ServiceResult<UnidadInventarioDto>.Failure("Una unidad vendida solo puede marcarse como entregada.");
        x.Estado = estado; await db.SaveChangesAsync(ct); var result = await Query().SingleAsync(y => y.Id == id, ct); return ServiceResult<UnidadInventarioDto>.Ok(result);
    }
    private IQueryable<UnidadInventarioDto> Query() => db.UnidadesInventario.AsNoTracking().Select(x => new UnidadInventarioDto(x.Id, x.CodigoInterno, x.Estado, x.FechaIngreso, x.Costo, x.PrecioLista, x.ProductoId, x.Producto.Nombre, x.Producto.CodigoInterno, x.DetalleCompraId, x.DetalleCompra.Compra.Origen));
}
