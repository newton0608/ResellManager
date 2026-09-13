using ResellManager.Application.Common;
using ResellManager.Application.DTOs;

namespace ResellManager.Application.Interfaces;

public interface ISeleccionOperativaService
{
    Task<IReadOnlyList<PedidoDto>> BuscarPedidosAsync(string termino, CancellationToken ct = default);
    Task<IReadOnlyList<UnidadInventarioDto>> BuscarUnidadesAsync(string termino, int? productoId = null,
        int? pedidoId = null, IReadOnlyCollection<int>? excluir = null, bool soloReservadas = false, CancellationToken ct = default);
    Task<DisponibilidadReservaDto> DisponibilidadAsync(int productoId, CancellationToken ct = default);
    Task<IReadOnlyList<PendienteClienteDto>> PendientesClienteAsync(int clienteId, CancellationToken ct = default);
    Task<bool> UnidadesDirectasDisponiblesAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);
}

public interface IRegistroPedidoConReservasService
{
    Task<ServiceResult<PedidoDto>> RegistrarAsync(PedidoInput pedido, IReadOnlyList<ReservaPedidoInput> reservas, CancellationToken ct = default);
}
