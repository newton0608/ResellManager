using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
namespace ResellManager.Application.Interfaces;
public sealed record CopiasEtiquetaInput(int ProductoId,int Copias);
public sealed record PerfilEtiquetas(decimal AnchoMm=50,decimal AltoMm=30,decimal MargenMm=2,int Dpi=203);
public sealed record FilaEtiquetaDto(int ProductoId,string Nombre,string Codigo,decimal Precio,int Copias,string Atributos);
public sealed record LoteEtiquetasDto(Guid Id,DateTime CreadoUtc,DateTime? ImpresoUtc,PerfilEtiquetas Perfil,IReadOnlyList<FilaEtiquetaDto> Filas,int Total,bool Desactualizado);
public interface IEtiquetasService
{
    Task<ServiceResult<LoteEtiquetasDto>> PrepararAsync(IReadOnlyList<CopiasEtiquetaInput> productos,PerfilEtiquetas perfil,string clave,CancellationToken ct=default);
    Task<ServiceResult<LoteEtiquetasDto>> ObtenerAsync(Guid id,CancellationToken ct=default);
    Task<ServiceResult<ArchivoInformeDto>> ExportarAsync(Guid id,string formato,CancellationToken ct=default);
    Task<ServiceResult> ConfirmarImpresionAsync(Guid id,CancellationToken ct=default);
}
