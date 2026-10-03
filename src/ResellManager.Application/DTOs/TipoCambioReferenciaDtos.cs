using ResellManager.Domain.Enums;

namespace ResellManager.Application.DTOs;

/// <summary>Referencia informativa en GTQ por una unidad de la moneda de origen.</summary>
public sealed record TipoCambioReferenciaDto(
    MonedaCompra Moneda,
    DateOnly FechaSolicitada,
    DateOnly FechaEfectiva,
    decimal Valor,
    string Fuente);

public sealed record TipoCambioReferenciaResultado(
    bool Disponible,
    TipoCambioReferenciaDto? Referencia,
    string? Mensaje)
{
    public const string MensajeEntradaManual =
        "No pudimos consultar la referencia del Banco de Guatemala. Ingresa el tipo de cambio manualmente.";

    public static TipoCambioReferenciaResultado Encontrada(TipoCambioReferenciaDto referencia) =>
        new(true, referencia, null);

    public static TipoCambioReferenciaResultado NoDisponible() =>
        new(false, null, MensajeEntradaManual);
}
