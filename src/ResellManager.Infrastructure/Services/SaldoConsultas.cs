using Microsoft.EntityFrameworkCore;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;

namespace ResellManager.Infrastructure.Services;

internal static class SaldoConsultas
{
    public static async Task<decimal> CalcularAsync(
        ResellManagerDbContext db,
        int? clienteId,
        CancellationToken ct
    )
    {
        var ventas = db
            .DetallesVenta.AsNoTracking()
            .Where(x => x.Venta.Estado == EstadoVenta.Registrada && x.Venta.Pedido.ClienteId != null);
        var pagos = db.Pagos.AsNoTracking().AsQueryable();

        if (clienteId.HasValue)
        {
            ventas = ventas.Where(x => x.Venta.Pedido.ClienteId == clienteId.Value);
            pagos = pagos.Where(x => x.ClienteId == clienteId.Value);
        }

        var totalVentas = await SumarAsync(ventas.Select(x => x.PrecioFinal), ct);
        var totalPagos = await SumarAsync(pagos.Select(x => x.Monto), ct);
        var movimientos = db.MovimientosCuenta.AsNoTracking().AsQueryable();
        if (clienteId.HasValue) movimientos = movimientos.Where(x => x.ClienteId == clienteId.Value);
        return totalVentas - totalPagos + await SumarAsync(movimientos.Select(x => x.Importe), ct);
    }

    public static async Task<IReadOnlyDictionary<int, decimal>> PorClientesAsync(ResellManagerDbContext db, int[] ids, CancellationToken ct)
    {
        var ventas = await db.DetallesVenta.AsNoTracking()
            .Where(x => x.Venta.Estado == EstadoVenta.Registrada && x.Venta.Pedido.ClienteId != null && ids.Contains(x.Venta.Pedido.ClienteId.Value))
            .Select(x => new { ClienteId = x.Venta.Pedido.ClienteId!.Value, Importe = x.PrecioFinal }).ToListAsync(ct);
        var pagos = await db.Pagos.AsNoTracking().Where(x => ids.Contains(x.ClienteId))
            .Select(x => new { x.ClienteId, Importe = -x.Monto }).ToListAsync(ct);
        var movimientos = await db.MovimientosCuenta.AsNoTracking().Where(x => ids.Contains(x.ClienteId))
            .Select(x => new { x.ClienteId, x.Importe }).ToListAsync(ct);
        return ventas.Concat(pagos).Concat(movimientos).GroupBy(x => x.ClienteId).ToDictionary(x => x.Key, x => x.Sum(y => y.Importe));
    }

    public static async Task<decimal> SumarAsync(
        IQueryable<decimal> importes,
        CancellationToken ct
    )
    {
        var total = 0m;
        await foreach (var importe in importes.AsAsyncEnumerable().WithCancellation(ct))
            total += importe;

        return total;
    }
}
