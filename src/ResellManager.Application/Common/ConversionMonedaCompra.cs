namespace ResellManager.Application.Common;

/// <summary>Conversión de costos de compra a la moneda base GTQ, compartida con la vista previa.</summary>
public static class ConversionMonedaCompra
{
    public static decimal CostoUnitarioGtq(decimal costoMonedaOrigen, decimal tipoCambio) =>
        decimal.Round(costoMonedaOrigen * tipoCambio, 2, MidpointRounding.AwayFromZero);
}
