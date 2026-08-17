using ResellManager.Domain.Enums;

namespace ResellManager.Application.DTOs;

public sealed record DetalleCompraInput(
    int ProductoId,
    int Cantidad,
    decimal CostoUnitario,
    decimal PrecioLista
);

public sealed record ComprobanteCompraInput(
    string? NumeroDocumento,
    DateOnly Fecha,
    string RutaDocumento,
    string? Observaciones
);

public sealed record CompraInput(
    string CodigoInterno,
    DateOnly FechaCompra,
    DateOnly? FechaIngreso,
    OrigenCompra Origen,
    int ProveedorId,
    string? Observaciones,
    IReadOnlyCollection<DetalleCompraInput> Detalles,
    ComprobanteCompraInput? Comprobante
);

public sealed record DetalleCompraDto(
    int Id,
    int ProductoId,
    string Producto,
    int Cantidad,
    decimal CostoUnitario,
    decimal PrecioLista,
    decimal Subtotal
);

public sealed record CompraDto(
    int Id,
    string CodigoInterno,
    DateOnly FechaCompra,
    OrigenCompra Origen,
    decimal Total,
    string? Observaciones,
    int ProveedorId,
    string Proveedor,
    IReadOnlyCollection<DetalleCompraDto> Detalles,
    string? RutaComprobante
);

public sealed record ComprobanteCompraDto(
    int Id,
    int CompraId,
    string? NumeroDocumento,
    DateOnly Fecha,
    string RutaDocumento,
    string? Observaciones
);

public sealed record RecepcionMercanciaInput(
    DateOnly FechaRecepcion,
    IReadOnlyCollection<int> UnidadInventarioIds
);

public sealed record UnidadInventarioDto(
    int Id,
    string CodigoInterno,
    EstadoUnidadInventario Estado,
    DateOnly? FechaIngreso,
    decimal Costo,
    decimal PrecioLista,
    int ProductoId,
    string Producto,
    string CodigoProducto,
    int DetalleCompraId,
    OrigenCompra OrigenCompra
);
