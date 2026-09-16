using ResellManager.Web.Components.Clientes;
using ResellManager.Domain.Enums;
using ResellManager.Application.DTOs;

namespace ResellManager.Web.Components.Compras;

public static class CompraPresentacion
{
    public static string Moneda(decimal importe) => ClientePresentacion.Moneda(importe);

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
