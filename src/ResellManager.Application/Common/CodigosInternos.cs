namespace ResellManager.Application.Common;

public static class CodigosInternos
{
    public const string PrefijoCompra = "COM-";

    public static string CrearCodigoCompra() =>
        PrefijoCompra + Guid.NewGuid().ToString("N").ToUpperInvariant();
}
