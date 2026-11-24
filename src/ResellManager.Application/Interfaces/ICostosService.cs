using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
namespace ResellManager.Application.Interfaces;
public sealed record NotaCostoInput(int UnidadId,decimal CostoReal,DateOnly FechaConocimiento,string Motivo,string Clave,string? Referencia=null);
public sealed record NotaCostoDto(Guid Id,int UnidadId,decimal? Anterior,decimal Nuevo,DateOnly FechaConocimiento,DateTime RegistradoUtc,string UsuarioId,string Motivo,string? Referencia);
public sealed record LineaUtilidadDto(int VentaId,string Venta,DateOnly Fecha,int DetalleId,decimal Precio,decimal? CostoOriginal,decimal? CostoRevisado,Guid? NotaId,string? UsuarioId,string? Motivo,DateTime? FechaCorreccion);
public sealed record ResumenUtilidadDto(decimal UtilidadCalculable,int LineasSinCosto,decimal VentasSinCosto,int Correcciones,DateTime CorteUtc,bool Original,IReadOnlyList<LineaUtilidadDto> Lineas);
public interface ICostosService
{
    Task<ServiceResult<NotaCostoDto>> CorregirAsync(NotaCostoInput input,CancellationToken ct=default);
    Task<ServiceResult<IReadOnlyList<NotaCostoDto>>> HistorialAsync(int unidadId,CancellationToken ct=default);
    Task<ServiceResult<ResumenUtilidadDto>> UtilidadAsync(DateOnly desde,DateOnly hasta,DateTime? corteUtc=null,bool original=false,CancellationToken ct=default);
    Task<ServiceResult<ArchivoInformeDto>> ExportarAsync(string formato,DateOnly desde,DateOnly hasta,DateTime? corteUtc=null,bool original=false,CancellationToken ct=default);
    Task<ServiceResult<Guid>> MigrarHistoricosAsync(IReadOnlyList<int> unidades,string motivo,string clave,CancellationToken ct=default);
}
