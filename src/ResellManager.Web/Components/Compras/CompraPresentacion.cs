using System.Globalization;
using ResellManager.Domain.Enums;

namespace ResellManager.Web.Components.Compras;

public static class CompraPresentacion
{
    private static readonly CultureInfo CulturaMexico = CultureInfo.GetCultureInfo("es-MX");

    public static string Moneda(decimal importe) => importe.ToString("C", CulturaMexico);

    public static string Origen(OrigenCompra origen) =>
        origen switch
        {
            OrigenCompra.Importacion => "Importación",
            OrigenCompra.CompraLocal => "Compra local",
            OrigenCompra.Catalogo => "Catálogo",
            OrigenCompra.EnvioHermano => "Envío hermano",
            _ => origen.ToString(),
        };

    public static string ClaseOrigen(OrigenCompra origen) =>
        origen switch
        {
            OrigenCompra.Importacion => "purchase-origin-import",
            OrigenCompra.Catalogo => "purchase-origin-catalog",
            _ => "purchase-origin-received",
        };
}
