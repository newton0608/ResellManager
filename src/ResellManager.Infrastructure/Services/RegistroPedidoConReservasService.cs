using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;

namespace ResellManager.Infrastructure.Services;

public sealed class RegistroPedidoConReservasService(IServiceScopeFactory scopes) : IRegistroPedidoConReservasService
{
    public async Task<ServiceResult<PedidoDto>> RegistrarAsync(PedidoInput pedido, IReadOnlyList<ReservaPedidoInput> reservas, CancellationToken ct = default)
    {
        var detalles = pedido.Detalles.ToArray();
        var ids = reservas.SelectMany(x => x.Unidades).ToArray();
        if (reservas.Select(x => x.IndiceDetalle).Distinct().Count() != reservas.Count
            || reservas.Any(x => x.IndiceDetalle < 0 || x.IndiceDetalle >= detalles.Length
                || x.Unidades.Count > detalles[x.IndiceDetalle].Cantidad)
            || ids.Distinct().Count() != ids.Length)
            return ServiceResult<PedidoDto>.Failure("Revisa la cantidad y selección de unidades: una unidad no puede repetirse.");
        if (pedido.TipoPedido == TipoPedido.Catalogo && ids.Length > 0)
            return ServiceResult<PedidoDto>.Failure("Los pedidos de catálogo no admiten reservas físicas.");

        // Contexto propio: rollback y descarte no afectan los datos rastreados del circuito Blazor.
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var unidades = await db.UnidadesInventario.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        if (unidades.Count != ids.Length || reservas.Any(r => r.Unidades.Any(id => !unidades.Any(u => u.Id == id
            && u.ProductoId == detalles[r.IndiceDetalle].ProductoId && u.Estado == EstadoUnidadInventario.Disponible && u.DetallePedidoReservaId == null))))
            return ServiceResult<PedidoDto>.Failure("Una unidad elegida ya no está disponible o recibió otra reserva. Edita la selección; no se creó el pedido.");

        var pedidos = scope.ServiceProvider.GetRequiredService<IPedidoService>();
        var creado = await pedidos.CrearManualAsync(pedido, ct);
        if (!creado.IsSuccess || creado.Value is null) return creado;
        var persistidos = creado.Value.Detalles.OrderBy(x => x.Id).ToArray();
        var inventario = scope.ServiceProvider.GetRequiredService<IInventarioService>();
        foreach (var reserva in reservas)
        foreach (var id in reserva.Unidades)
        {
            var resultado = await inventario.ReservarAsync(id, persistidos[reserva.IndiceDetalle].Id, ct);
            if (!resultado.IsSuccess)
                return ServiceResult<PedidoDto>.Failure($"No se creó el pedido ni sus reservas: {resultado.ErrorMessage}");
        }
        await tx.CommitAsync(ct);
        return creado;
    }
}
