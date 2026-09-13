using Microsoft.EntityFrameworkCore;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;

namespace ResellManager.Infrastructure.Services;

public sealed class SeleccionOperativaService(ResellManagerDbContext db) : ISeleccionOperativaService
{
    public async Task<IReadOnlyList<PedidoDto>> BuscarPedidosAsync(string termino, CancellationToken ct = default)
    {
        var texto = termino.Trim().ToLowerInvariant();
        var consulta = db.Pedidos.Where(x => x.Estado != EstadoPedido.Cancelado && x.Venta == null && x.Detalles.Any())
            .Where(x => x.CodigoInterno.ToLower().Contains(texto)
                || (x.Cliente.Nombres + " " + (x.Cliente.Apellidos ?? "")).ToLower().Contains(texto))
            .OrderByDescending(x => x.CodigoInterno.ToLower() == texto).ThenByDescending(x => x.Fecha).ThenByDescending(x => x.Id).Take(12);
        return await PedidoService.Query(consulta).ToListAsync(ct);
    }

    private IQueryable<UnidadInventario> Elegibles(int? productoId, int? pedidoId) => db.UnidadesInventario.AsNoTracking()
        .Where(x => x.Estado == EstadoUnidadInventario.Disponible
            && (!productoId.HasValue || x.ProductoId == productoId.Value)
            && (x.DetallePedidoReservaId == null || (pedidoId.HasValue && x.DetallePedidoReserva!.PedidoId == pedidoId.Value)));

    public async Task<IReadOnlyList<UnidadInventarioDto>> BuscarUnidadesAsync(string termino, int? productoId = null,
        int? pedidoId = null, IReadOnlyCollection<int>? excluir = null, bool soloReservadas = false, CancellationToken ct = default)
    {
        var texto = termino.Trim().ToLowerInvariant();
        var consulta = Elegibles(productoId, pedidoId);
        if (soloReservadas) consulta = consulta.Where(x => pedidoId.HasValue && x.DetallePedidoReserva != null && x.DetallePedidoReserva.PedidoId == pedidoId.Value);
        if (excluir is { Count: > 0 }) consulta = consulta.Where(x => !excluir.Contains(x.Id));
        if (texto.Length > 0) consulta = consulta.Where(x => x.CodigoInterno.ToLower().Contains(texto)
            || x.Producto.Nombre.ToLower().Contains(texto) || x.Producto.CodigoInterno.ToLower().Contains(texto)
            || (x.Producto.CodigoBarras != null && x.Producto.CodigoBarras.ToLower().Contains(texto)));
        return await InventarioService.Query(consulta.OrderByDescending(x => x.DetallePedidoReservaId != null)
            .ThenByDescending(x => x.CodigoInterno.ToLower() == texto).ThenBy(x => x.CodigoInterno).Take(12)).ToListAsync(ct);
    }

    public async Task<DisponibilidadReservaDto> DisponibilidadAsync(int productoId, CancellationToken ct = default)
    {
        var consulta = Elegibles(productoId, null);
        var cantidad = await consulta.CountAsync(ct);
        return new(cantidad, cantidad == 1 ? await InventarioService.Query(consulta).SingleAsync(ct) : null);
    }

    public async Task<bool> UnidadesDirectasDisponiblesAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
        ids.Count > 0 && ids.Distinct().Count() == ids.Count && await Elegibles(null, null).CountAsync(x => ids.Contains(x.Id), ct) == ids.Count;

    public async Task<IReadOnlyList<PendienteClienteDto>> PendientesClienteAsync(int clienteId, CancellationToken ct = default)
    {
        var reservas = await db.UnidadesInventario.AsNoTracking()
            .Where(x => x.DetallePedidoReserva != null && x.DetallePedidoReserva.Pedido.ClienteId == clienteId
                && x.DetallePedidoReserva.Pedido.Estado != EstadoPedido.Cancelado
                && x.Estado != EstadoUnidadInventario.Entregada && x.Estado != EstadoUnidadInventario.Vendida)
            .Select(x => new PendienteClienteDto(x.Id, x.CodigoInterno, x.Producto.Nombre, x.DetallePedidoReserva!.PedidoId,
                x.DetallePedidoReserva.Pedido.CodigoInterno, x.Estado, false)).ToListAsync(ct);
        var entregas = await db.DetallesVenta.AsNoTracking()
            .Where(x => x.Venta.Estado == EstadoVenta.Registrada && x.Venta.Pedido.ClienteId == clienteId
                && x.UnidadInventario != null && x.UnidadInventario.Estado == EstadoUnidadInventario.Vendida)
            .Select(x => new PendienteClienteDto(x.UnidadInventario!.Id, x.UnidadInventario.CodigoInterno, x.UnidadInventario.Producto.Nombre,
                x.Venta.PedidoId, x.Venta.Pedido.CodigoInterno, x.UnidadInventario.Estado, true)).ToListAsync(ct);
        return reservas.Concat(entregas).DistinctBy(x => x.UnidadId).OrderBy(x => x.Producto).ThenBy(x => x.CodigoUnidad).ToArray();
    }
}
