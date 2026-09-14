using Microsoft.EntityFrameworkCore;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Infrastructure.Persistence;
namespace ResellManager.Infrastructure.Services;

public sealed class ActividadClienteService(ResellManagerDbContext db) : IActividadClienteService
{
    private static DateOnly Inicio(DateOnly fecha) => new(fecha.Year, fecha.Month, 1);
    private static async Task<DateOnly?> MesReciente(IQueryable<DateOnly> fechas, CancellationToken ct) =>
        (await fechas.OrderByDescending(x => x).Select(x => (DateOnly?)x).FirstOrDefaultAsync(ct)) is { } fecha ? Inicio(fecha) : null;

    public async Task<PaginaMes<VentaDto>> VentasAsync(int clienteId, DateOnly? mes = null, CancellationToken ct = default)
    {
        var consulta = db.Ventas.AsNoTracking().Where(x => x.Pedido.ClienteId == clienteId);
        if (mes is { } limite) { var fin = Inicio(limite).AddMonths(1); consulta = consulta.Where(x => x.Fecha < fin); }
        var inicio = await MesReciente(consulta.Select(x => x.Fecha), ct);
        if (inicio is null) return new(null, [], null);
        var hasta = inicio.Value.AddMonths(1);
        var anterior = await MesReciente(consulta.Where(x => x.Fecha < inicio.Value).Select(x => x.Fecha), ct);
        var ventas = await consulta.Where(x => x.Fecha >= inicio.Value && x.Fecha < hasta)
            .Include(x => x.Pedido).ThenInclude(x => x.Cliente)
            .Include(x => x.Detalles).ThenInclude(x => x.Producto)
            .Include(x => x.Detalles).ThenInclude(x => x.UnidadInventario).ThenInclude(x => x!.Producto)
            .OrderByDescending(x => x.Fecha).ThenByDescending(x => x.Id).ToListAsync(ct);
        return new(inicio, ventas.Select(ClienteService.MapVenta).ToArray(), anterior);
    }

    public async Task<PaginaMes<PagoDto>> PagosAsync(int clienteId, DateOnly? mes = null, CancellationToken ct = default)
    {
        var consulta = db.Pagos.AsNoTracking().Where(x => x.ClienteId == clienteId);
        if (mes is { } limite) { var fin = Inicio(limite).AddMonths(1); consulta = consulta.Where(x => x.Fecha < fin); }
        var inicio = await MesReciente(consulta.Select(x => x.Fecha), ct);
        if (inicio is null) return new(null, [], null);
        var hasta = inicio.Value.AddMonths(1);
        var anterior = await MesReciente(consulta.Where(x => x.Fecha < inicio.Value).Select(x => x.Fecha), ct);
        var pagos = await consulta.Where(x => x.Fecha >= inicio.Value && x.Fecha < hasta)
            .OrderByDescending(x => x.Fecha).ThenByDescending(x => x.Id)
            .Select(x => new PagoDto(x.Id, x.ClienteId, x.Cliente.Nombres + " " + (x.Cliente.Apellidos ?? ""),
                x.Fecha, x.Monto, x.MetodoPago, x.Referencia, x.Observaciones)).ToListAsync(ct);
        return new(inicio, pagos, anterior);
    }
}
