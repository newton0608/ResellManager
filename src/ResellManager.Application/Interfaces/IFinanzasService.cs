using ResellManager.Application.Common;
using ResellManager.Application.DTOs;

namespace ResellManager.Application.Interfaces;

public sealed record CuentaOperativa(string UsuarioId, bool Administrador);

public interface ICuentaOperativa
{
    Task<CuentaOperativa?> ObtenerAsync(CancellationToken ct = default);
}

public interface IFinanzasService
{
    Task<ServiceResult<AperturaDto>> AperturaAsync(CancellationToken ct = default);
    Task<ServiceResult<AperturaDto>> PrepararAsync(IReadOnlyList<FilaAperturaInput> filas, CancellationToken ct = default);
    Task<ServiceResult<AperturaDto>> PrepararCsvAsync(string csv, CancellationToken ct = default);
    Task<ServiceResult<AperturaDto>> ConfirmarAsync(Guid version, string clave, CancellationToken ct = default);
    Task<ServiceResult<ResultadoAjusteDto>> RevisarAjusteAsync(int clienteId, decimal importe, CancellationToken ct = default);
    Task<ServiceResult<ResultadoAjusteDto>> AjustarAsync(AjusteInput input, CancellationToken ct = default);
    Task<ServiceResult<ResultadoAjusteDto>> RevertirAsync(Guid movimientoId, string motivo, string clave, CancellationToken ct = default);
    Task<ServiceResult<PaginaMovimientosDto>> ConsultarAsync(FiltroMovimientos filtro, CancellationToken ct = default);
    Task<ServiceResult<ArchivoInformeDto>> ExportarAsync(string informe, string formato, FiltroMovimientos filtro, CancellationToken ct = default);
}
