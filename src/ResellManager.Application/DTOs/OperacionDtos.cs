using ResellManager.Domain.Enums;

namespace ResellManager.Application.DTOs;

public sealed record DetallePedidoInput(
    int ProductoId,
    int Cantidad,
    decimal PrecioUnitario,
    string? Observaciones
);

public sealed record PedidoInput(
    string CodigoInterno,
    DateOnly Fecha,
    TipoPedido TipoPedido,
    int ClienteId,
    string? Observaciones,
    IReadOnlyCollection<DetallePedidoInput> Detalles
);

public sealed record DetallePedidoDto(
    int Id,
    int ProductoId,
    string Producto,
    int Cantidad,
    decimal PrecioUnitario,
    string? Observaciones,
    decimal Subtotal
);

public sealed record PedidoDto(
    int Id,
    string CodigoInterno,
    DateOnly Fecha,
    TipoPedido TipoPedido,
    EstadoPedido Estado,
    string? Observaciones,
    int ClienteId,
    string Cliente,
    IReadOnlyCollection<DetallePedidoDto> Detalles,
    int? VentaId
);

public sealed record DetalleVentaInput(
    int? UnidadInventarioId,
    int? ProductoId,
    decimal? CostoUnitario,
    decimal PrecioFinal,
    string? Observaciones
);

public sealed record VentaInput(
    int PedidoId,
    string CodigoInterno,
    DateOnly Fecha,
    string? Observaciones,
    IReadOnlyCollection<DetalleVentaInput> Detalles
);

public sealed record DetalleVentaDto(
    int Id,
    int? UnidadInventarioId,
    string? CodigoUnidad,
    int ProductoId,
    string Producto,
    decimal CostoUnitario,
    decimal PrecioFinal,
    string? Observaciones
);

public sealed record VentaDto(
    int Id,
    string CodigoInterno,
    DateOnly Fecha,
    EstadoVenta Estado,
    string? Observaciones,
    int PedidoId,
    int ClienteId,
    string Cliente,
    decimal Total,
    IReadOnlyCollection<DetalleVentaDto> Detalles
);

public sealed record PagoInput(
    int ClienteId,
    DateOnly Fecha,
    decimal Monto,
    MetodoPago MetodoPago,
    string? Referencia,
    string? Observaciones
);

public sealed record PagoDto(
    int Id,
    int ClienteId,
    string Cliente,
    DateOnly Fecha,
    decimal Monto,
    MetodoPago MetodoPago,
    string? Referencia,
    string? Observaciones
);

public sealed record ClienteHistorialDto(
    ClienteDto Cliente,
    IReadOnlyCollection<VentaDto> Ventas,
    IReadOnlyCollection<PagoDto> Pagos
);

public sealed record DashboardDto(
    decimal TotalAdeudado,
    decimal ValorInventarioDisponible,
    int UnidadesDisponibles,
    int PedidosPendientes,
    IReadOnlyCollection<PagoDto> UltimosPagos,
    IReadOnlyCollection<VentaDto> UltimasVentas
);
