using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;

namespace ResellManager.Application.Interfaces;

/// <summary>Consulta opcional de una sugerencia; nunca determina el tipo aplicado a la compra.</summary>
public interface ITipoCambioReferenciaService
{
    Task<TipoCambioReferenciaResultado> ConsultarAsync(
        MonedaCompra moneda, DateOnly fecha, CancellationToken cancellationToken = default);
}
