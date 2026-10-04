using System.Globalization;
using ResellManager.Web.Components.Clientes;
using ResellManager.Domain.Enums;
using ResellManager.Application.DTOs;

namespace ResellManager.Web.Components.Compras;

public static class CompraPresentacion
{
    public static string Moneda(decimal importe) => ClientePresentacion.Moneda(importe);
    public static string MonedaOrigen(decimal? importe, MonedaCompra moneda) => importe is null ? "—" :
        moneda == MonedaCompra.USD ? $"US$ {importe.Value.ToString("N2", CultureInfo.GetCultureInfo("en-US"))}" : Moneda(importe.Value);
    public static string Equivalente(decimal? importe) => importe is null ? "—" : Moneda(importe.Value);
    public static string TipoCambio(decimal valor) => $"Q{valor.ToString("0.00000#######################", CultureInfo.InvariantCulture)}";
    public static string NombreMoneda(MonedaCompra moneda) => moneda == MonedaCompra.USD
        ? "USD — Dólar estadounidense" : "GTQ — Quetzal";

    public static string EstadoRecepcion(ResumenRecepcionCompraDto resumen) =>
        resumen.Pendientes > 0 ? "Recepción pendiente"
        : resumen.Perdidas > 0 ? "Recepción finalizada con pérdidas"
        : "Recepción completada";

    public static string Origen(OrigenCompra origen) =>
        origen switch
        {
            OrigenCompra.Importacion => "Importación",
            OrigenCompra.CompraLocal => "Compra local",
            OrigenCompra.Catalogo => "Catálogo",
            OrigenCompra.EnvioHermano => "Envío del hijo",
            _ => "Origen no disponible",
        };

    public static string ClaseOrigen(OrigenCompra origen) =>
        origen switch
        {
            OrigenCompra.Importacion => "purchase-origin-import",
            OrigenCompra.Catalogo => "purchase-origin-catalog",
            _ => "purchase-origin-received",
        };
}
