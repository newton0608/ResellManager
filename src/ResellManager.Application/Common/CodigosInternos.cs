namespace ResellManager.Application.Common;

public static class CodigosInternos
{
    public const string PrefijoPedido = "PED-";
    public const string PrefijoVenta = "VEN-";
    public const string PrefijoCompra = "COM-";

    public static string CrearCodigoPedido() =>
        PrefijoPedido + Guid.NewGuid().ToString("N").ToUpperInvariant();

    public static string CrearCodigoVenta() =>
        PrefijoVenta + Guid.NewGuid().ToString("N").ToUpperInvariant();

    public static string CrearCodigoCompra() =>
        PrefijoCompra + Guid.NewGuid().ToString("N").ToUpperInvariant();
}
