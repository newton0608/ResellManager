using ResellManager.Application.Common;
using ResellManager.Application.DTOs;

namespace ResellManager.Application.Interfaces;

public interface IConsultaProductoCodigoBarras
{
    Task<ProductoDto?> ObtenerPorCodigoBarrasAsync(string codigo, CancellationToken ct = default);
}

public interface IProductoLookupProvider
{
    string Fuente { get; }
    Task<ProductoLookupRespuesta> ConsultarAsync(string codigo, CancellationToken ct = default);
}

public interface IProductoLookupService
{
    Task<ProductoLookupRonda> IniciarAsync(string codigo, CancellationToken ct = default);
    Task ContinuarAsync(ProductoLookupRonda ronda, CancellationToken ct = default);
}

public interface IImagenProductoExternaService
{
    Task<ServiceResult<Stream>> DescargarAsync(string url, CancellationToken ct = default);
}

public sealed record AltaProductoAsistidaResultado(ServiceResult<ProductoDto> Producto, string? AvisoImagen = null);

public interface IAltaProductoAsistidaService
{
    Task<AltaProductoAsistidaResultado> CrearAsistidoAsync(
        ProductoInput input, Stream? imagenManual, string? imagenExternaUrl, CancellationToken ct = default);
}
