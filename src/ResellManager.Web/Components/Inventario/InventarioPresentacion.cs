using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;

namespace ResellManager.Web.Components.Inventario;

public static class InventarioPresentacion
{
    public static string Estado(EstadoUnidadInventario estado) =>
        estado switch
        {
            EstadoUnidadInventario.Comprada => "Comprada",
            EstadoUnidadInventario.EnTransito => "En tránsito",
            EstadoUnidadInventario.Disponible => "Disponible",
            EstadoUnidadInventario.Vendida => "Vendida",
            EstadoUnidadInventario.Entregada => "Entregada",
            _ => estado.ToString(),
        };

    public static string ClaseEstado(EstadoUnidadInventario estado) =>
        estado switch
        {
            EstadoUnidadInventario.Comprada => "inventory-status-purchased",
            EstadoUnidadInventario.EnTransito => "inventory-status-transit",
            EstadoUnidadInventario.Disponible => "inventory-status-available",
            EstadoUnidadInventario.Vendida => "inventory-status-sold",
            EstadoUnidadInventario.Entregada => "inventory-status-delivered",
            _ => "inventory-status-neutral",
        };

    public static bool EstaReservada(UnidadInventarioDto unidad) =>
        unidad.DetallePedidoReservaId.HasValue;

    public static bool PuedeRecibirse(UnidadInventarioDto unidad) =>
        unidad.Estado is EstadoUnidadInventario.Comprada or EstadoUnidadInventario.EnTransito;

    public static EstadoUnidadInventario? SiguienteEstadoManual(UnidadInventarioDto unidad) =>
        unidad.Estado switch
        {
            EstadoUnidadInventario.Comprada => EstadoUnidadInventario.EnTransito,
            EstadoUnidadInventario.Vendida => EstadoUnidadInventario.Entregada,
            _ => null,
        };

    public static string AccionEstado(EstadoUnidadInventario estado) =>
        estado switch
        {
            EstadoUnidadInventario.EnTransito => "Marcar en tránsito",
            EstadoUnidadInventario.Entregada => "Marcar entregada",
            _ => string.Empty,
        };

    public static string Origen(OrigenCompra origen) =>
        origen switch
        {
            OrigenCompra.Importacion => "Importación",
            OrigenCompra.CompraLocal => "Compra local",
            OrigenCompra.Catalogo => "Catálogo",
            OrigenCompra.EnvioHermano => "Envío del hijo",
            _ => origen.ToString(),
        };
}
