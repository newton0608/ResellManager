using ResellManager.Application.DTOs;

namespace ResellManager.Web.Catalogo;

public interface ICatalogoPublicoClient
{
    Task<IReadOnlyList<ProductoCatalogoDto>> ListarAsync(
        string? termino = null, int? categoriaId = null, CancellationToken ct = default, string? marca = null);
    Task<ProductoCatalogoDetalleDto?> ObtenerDetalleAsync(int productoId, CancellationToken ct = default);
}
