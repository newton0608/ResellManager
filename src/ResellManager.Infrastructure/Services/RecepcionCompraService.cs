using Microsoft.EntityFrameworkCore;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
namespace ResellManager.Infrastructure.Services;

public sealed class RecepcionCompraService(ResellManagerDbContext db) : IRecepcionCompraService
{
    public async Task<IReadOnlyList<RecepcionCompraDto>> PendientesAsync(CancellationToken ct = default)
    {
        var pendientes = db.UnidadesInventario.AsNoTracking()
            .Where(x => x.Estado == EstadoUnidadInventario.Comprada || x.Estado == EstadoUnidadInventario.EnTransito);
        var unidades = await InventarioService.Query(pendientes.OrderBy(x => x.CodigoInterno)).ToListAsync(ct);
        var compras = await pendientes.Select(x => new { x.DetalleCompraId, x.DetalleCompra.CompraId,
            x.DetalleCompra.Compra.CodigoInterno, Proveedor = x.DetalleCompra.Compra.Proveedor.Nombre,
            x.DetalleCompra.Compra.Origen }).Distinct().ToListAsync(ct);
        var porDetalle = unidades.ToLookup(x => x.DetalleCompraId);
        return compras.GroupBy(x => new { x.CompraId, x.CodigoInterno, x.Proveedor, x.Origen })
            .OrderByDescending(x => x.Key.CompraId)
            .Select(g => new RecepcionCompraDto(g.Key.CompraId, g.Key.CodigoInterno, g.Key.Proveedor, g.Key.Origen,
                g.SelectMany(x => porDetalle[x.DetalleCompraId]).ToArray())).ToArray();
    }
}
