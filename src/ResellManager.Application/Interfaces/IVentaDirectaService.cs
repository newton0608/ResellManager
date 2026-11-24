using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;
using ResellManager.Domain.Entities;
namespace ResellManager.Application.Interfaces;

public sealed record CobroCFInput(MetodoPago Metodo, decimal Aplicado, decimal Entregado, string? Referencia = null);
public sealed record VentaDirectaInput(string Clave, DateOnly Fecha, int? ClienteId, string? Nombre, string? Apellido,
    IReadOnlyList<DetalleVentaInput> Detalles, IReadOnlyList<CobroCFInput> Cobros, string? Observaciones = null);
public sealed record RevisionVentaDirectaDto(decimal Total, decimal Aplicado, decimal Entregado, decimal Cambio, decimal? SaldoResultante);
public sealed record CobroCFDto(MetodoPago Metodo, decimal Aplicado, decimal Entregado, decimal Cambio, string? Referencia);
public sealed record ReembolsoCFDto(Guid Id,decimal Importe,MetodoPago Metodo,DateOnly Fecha,DateTime RegistradoUtc,string UsuarioId,string Referencia);
public sealed record ArticuloDevueltoCFDto(int UnidadId,bool Apta,string Observacion,DateTime RegistradoUtc,string UsuarioId);
public sealed record AnulacionCFDto(Guid Id, int VentaId, EstadoAnulacionCF Estado, string Motivo, string SolicitanteId,
    DateTime SolicitadaUtc, string? AdministradorId, DateTime? DecididaUtc, string? MotivoDecision,
    decimal Total, decimal Devuelto, int Unidades, int Recibidas, DateTime? CompletadaUtc,
    IReadOnlyList<ReembolsoCFDto>? Reembolsos = null, IReadOnlyList<ArticuloDevueltoCFDto>? Articulos = null);
public sealed record DevolucionCFInput(Guid AnulacionId, string Clave, decimal Importe, MetodoPago Metodo, DateOnly Fecha, string Referencia);
public interface IVentaDirectaService
{
    Task<ServiceResult<RevisionVentaDirectaDto>> RevisarAsync(VentaDirectaInput input, CancellationToken ct = default);
    Task<ServiceResult<VentaDto>> RegistrarAsync(VentaDirectaInput input, CancellationToken ct = default);
    Task<ServiceResult<AnulacionCFDto>> SolicitarAnulacionAsync(int ventaId, string motivo, string clave, CancellationToken ct = default);
    Task<ServiceResult<AnulacionCFDto>> DecidirAsync(Guid id, bool aprobar, string motivo, string clave, CancellationToken ct = default);
    Task<ServiceResult<AnulacionCFDto>> RegistrarDevolucionAsync(DevolucionCFInput input, CancellationToken ct = default);
    Task<ServiceResult<AnulacionCFDto>> RecibirAsync(Guid id, int unidadId, bool apta, string observacion, string clave, CancellationToken ct = default);
    Task<ServiceResult<AnulacionCFDto>> CompletarAsync(Guid id, string clave, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<AnulacionCFDto>>> AnulacionesAsync(int? ventaId = null, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<CobroCFDto>>> CobrosAsync(int ventaId, CancellationToken ct = default);
    Task<ServiceResult<ArchivoInformeDto>> ExportarAsync(string formato, FiltroMovimientos filtro, CancellationToken ct = default);
}
