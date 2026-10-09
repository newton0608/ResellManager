using ResellManager.Application.DTOs;

namespace ResellManager.Web.Catalogo;

public interface ICatalogoPublicoClient
{
    Task<IReadOnlyList<ProductoCatalogoDto>> ListarAsync(
        string? termino = null, int? categoriaId = null, CancellationToken ct = default, string? marca = null);
    Task<CatalogoProductosPaginaDto> ListarPaginaAsync(string? termino, int? categoriaId, string? marca, int? cursor, CancellationToken ct = default);
    Task<CatalogoEscaparatePaginaDto> LeerEscaparateAsync(int? cursor = null, CancellationToken ct = default);
    Task<CatalogoCategoriasPaginaDto> LeerRaicesAsync(int? cursor = null, CancellationToken ct = default);
    Task<CatalogoMarcasPaginaDto> LeerMarcasAsync(string? cursor = null, CancellationToken ct = default);
    Task<CatalogoCategoriaContextoDto?> LeerContextoCategoriaAsync(int id, CancellationToken ct = default);
    Task<CatalogoCategoriasPaginaDto> LeerSubcategoriasAsync(int raizId, int? cursor = null, CancellationToken ct = default);
    Task<ProductoCatalogoDetalleDto?> ObtenerDetalleAsync(int productoId, CancellationToken ct = default);
}
