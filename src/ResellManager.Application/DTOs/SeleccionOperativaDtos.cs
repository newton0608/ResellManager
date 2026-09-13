using ResellManager.Domain.Enums;
namespace ResellManager.Application.DTOs;

// El índice vincula la intención con el detalle, aún sin Id persistido.
public sealed record ReservaPedidoInput(int IndiceDetalle, IReadOnlyList<int> Unidades);
public sealed record DisponibilidadReservaDto(int Cantidad, UnidadInventarioDto? Unica);
public sealed record PendienteClienteDto(int UnidadId, string CodigoUnidad, string Producto,
    int PedidoId, string CodigoPedido, EstadoUnidadInventario Estado, bool PendienteEntrega);
