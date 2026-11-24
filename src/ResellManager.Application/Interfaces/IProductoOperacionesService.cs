using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
namespace ResellManager.Application.Interfaces;
public sealed record ProductoFiltros(bool SinPrecio = false, string? Marca = null, string? Modelo = null,
    decimal? MlDesde = null, decimal? MlHasta = null, decimal? GramosDesde = null, decimal? GramosHasta = null,
    string? Color = null, string? Talla = null, string? Presentacion = null);
public sealed record VarianteDto(int ProductoId,string Nombre,string Atributos,bool Disponible,decimal PrecioPublico);
public sealed record GrupoVariantesDto(Guid Id,string Nombre,IReadOnlyList<VarianteDto> Variantes);
public interface IProductoOperacionesService
{
    Task<ProductoPaginaDto> ListarAsync(ProductoFiltros filtros,string? termino=null,int? categoria=null,ProductoCursorDto? cursor=null,CancellationToken ct=default);
    Task<ServiceResult<ProductoDto>> PrecioAsync(int id,decimal precio,decimal esperado,string clave,CancellationToken ct=default);
    Task<ServiceResult<IReadOnlyList<GrupoVariantesDto>>> GruposAsync(CancellationToken ct=default);
    Task<ServiceResult<GrupoVariantesDto>> VincularAsync(int productoId,Guid? grupoId,string nombreGrupo,IReadOnlyDictionary<string,string> atributos,CancellationToken ct=default);
    Task<ServiceResult> DesvincularAsync(int productoId,CancellationToken ct=default);
}
