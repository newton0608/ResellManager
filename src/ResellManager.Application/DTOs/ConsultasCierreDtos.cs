using ResellManager.Domain.Enums;
namespace ResellManager.Application.DTOs;

public sealed record PaginaMes<T>(DateOnly? Mes, IReadOnlyList<T> Registros, DateOnly? MesAnterior);
public sealed record RecepcionCompraDto(int CompraId, string CodigoCompra, string Proveedor,
    OrigenCompra Origen, IReadOnlyList<UnidadInventarioDto> Unidades);
