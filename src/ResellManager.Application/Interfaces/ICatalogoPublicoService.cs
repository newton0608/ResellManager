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

    Task<ServiceResult<ProductoCatalogoDetalleDto>> ObtenerPorIdAsync(
        int productoId, CancellationToken ct = default);

    // El consumidor recibe el contenido; la ruta de almacenamiento permanece privada.
    Task<ServiceResult<ImagenProductoLectura>> AbrirImagenPrincipalAsync(
        int productoId, CancellationToken ct = default);

    Task<ServiceResult<ImagenProductoLectura>> AbrirImagenAsync(
        int productoId, string imagenId, CancellationToken ct = default);
}
