using ResellManager.Application.Common;
using ResellManager.Application.DTOs;

namespace ResellManager.Application.Interfaces;

/// <summary>
/// Lectura comercial de productos con inventario físico libre para venta general.
/// </summary>
public interface ICatalogoPublicoService
{
    Task<IReadOnlyList<ProductoCatalogoDto>> ListarAsync(
        string? termino = null, int? categoriaId = null, CancellationToken ct = default, string? marca = null);

    Task<CatalogoProductosPaginaDto> ListarPaginaAsync(
        string? termino = null, int? categoriaId = null, string? marca = null,
        int? cursor = null, int tamano = 16, CancellationToken ct = default);

    Task<CatalogoEscaparatePaginaDto> LeerEscaparateAsync(
        int? cursor = null, int tamano = 3, CancellationToken ct = default);

    Task<CatalogoCategoriasPaginaDto> LeerRaicesAsync(
        int? cursor = null, int tamano = 16, CancellationToken ct = default);

    Task<CatalogoMarcasPaginaDto> LeerMarcasAsync(
        string? cursor = null, int tamano = 16, CancellationToken ct = default);

    Task<ServiceResult<CatalogoCategoriaContextoDto>> LeerContextoCategoriaAsync(
        int categoriaId, CancellationToken ct = default);

    Task<CatalogoCategoriasPaginaDto> LeerSubcategoriasAsync(
        int raizId, int? cursor = null, int tamano = 16, CancellationToken ct = default);

    Task<ServiceResult<ProductoCatalogoDetalleDto>> ObtenerPorIdAsync(
        int productoId, CancellationToken ct = default);

    // El consumidor recibe el contenido; la ruta de almacenamiento permanece privada.
    Task<ServiceResult<ImagenProductoLectura>> AbrirImagenPrincipalAsync(
        int productoId, CancellationToken ct = default);

    Task<ServiceResult<ImagenProductoLectura>> AbrirImagenAsync(
        int productoId, string imagenId, CancellationToken ct = default);
}
