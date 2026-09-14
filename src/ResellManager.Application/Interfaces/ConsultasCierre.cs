using ResellManager.Application.DTOs;
namespace ResellManager.Application.Interfaces;

public interface IActividadClienteService
{
    Task<PaginaMes<VentaDto>> VentasAsync(int clienteId, DateOnly? mes = null, CancellationToken ct = default);
    Task<PaginaMes<PagoDto>> PagosAsync(int clienteId, DateOnly? mes = null, CancellationToken ct = default);
}

public interface IRecepcionCompraService
{
    Task<IReadOnlyList<RecepcionCompraDto>> PendientesAsync(CancellationToken ct = default);
}
