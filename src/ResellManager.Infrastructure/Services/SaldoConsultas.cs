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
            .Where(x => x.Venta.Estado == EstadoVenta.Registrada);
        var pagos = db.Pagos.AsNoTracking().AsQueryable();

        if (clienteId.HasValue)
        {
            ventas = ventas.Where(x => x.Venta.Pedido.ClienteId == clienteId.Value);
            pagos = pagos.Where(x => x.ClienteId == clienteId.Value);
        }

        var totalVentas = await SumarAsync(ventas.Select(x => x.PrecioFinal), ct);
        var totalPagos = await SumarAsync(pagos.Select(x => x.Monto), ct);
        return totalVentas - totalPagos;
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
