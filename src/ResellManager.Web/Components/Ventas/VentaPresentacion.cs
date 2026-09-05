using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;
using ResellManager.Web.Components.Clientes;

namespace ResellManager.Web.Components.Ventas;

public static class VentaPresentacion
{
    public static string Estado(EstadoVenta estado) => estado switch
    {
        EstadoVenta.Registrada => "Registrada",
        EstadoVenta.Cancelada => "Cancelada",
        _ => "Estado desconocido",
    };

    public static string ClaseEstado(EstadoVenta estado) => estado switch
    {
        EstadoVenta.Registrada => "sale-status-registered",
        EstadoVenta.Cancelada => "sale-status-canceled",
        _ => "status-badge-neutral",
    };

    public static decimal Utilidad(DetalleVentaDto detalle) =>
        detalle.PrecioFinal - detalle.CostoUnitario;

    public static decimal UtilidadTotal(VentaDto venta) => venta.Detalles.Sum(Utilidad);

    public static string Moneda(decimal monto) => ClientePresentacion.Moneda(monto);
    public const string PrefijoPedidoVentaDirecta = "PED-VD-";
    public const string PrefijoVentaDirecta = "VEN-VD-";

    public static string CrearCodigoPedidoVentaDirecta() =>
        PrefijoPedidoVentaDirecta + Guid.NewGuid().ToString("N").ToUpperInvariant();

    public static string CrearCodigoVentaDirecta() =>
        PrefijoVentaDirecta + Guid.NewGuid().ToString("N").ToUpperInvariant();

    public static bool EsUnidadElegibleVentaDirecta(UnidadInventarioDto unidad) =>
        unidad.Estado == EstadoUnidadInventario.Disponible
        && !unidad.DetallePedidoReservaId.HasValue
        && !unidad.PedidoReservaId.HasValue;
    public static bool TieneUnidadesDuplicadas(IEnumerable<int> unidadIds)
    {
        var ids = unidadIds.ToArray();
        return ids.Distinct().Count() != ids.Length;
    }
}
