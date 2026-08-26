using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;

namespace ResellManager.Web.Components.Pedidos;

public static class PedidoPresentacion
{
    public static string Tipo(TipoPedido tipo) => tipo switch
    {
        TipoPedido.Importacion => "Importación",
        TipoPedido.Catalogo => "Catálogo",
        TipoPedido.Apartado => "Apartado",
        TipoPedido.VentaDirecta => "Venta directa",
        _ => tipo.ToString(),
    };

    public static string Estado(EstadoPedido estado) => estado switch
    {
        EstadoPedido.Pendiente => "Pendiente",
        EstadoPedido.Confirmado => "Confirmado",
        EstadoPedido.Cancelado => "Cancelado",
        EstadoPedido.Completado => "Completado",
        _ => estado.ToString(),
    };

    public static string ClaseEstado(EstadoPedido estado) => estado switch
    {
        EstadoPedido.Pendiente => "order-status-pending",
        EstadoPedido.Confirmado => "order-status-confirmed",
        EstadoPedido.Cancelado => "order-status-canceled",
        EstadoPedido.Completado => "order-status-completed",
        _ => "status-badge-neutral",
    };

    public static bool EsModificable(EstadoPedido estado) =>
        estado is not EstadoPedido.Cancelado and not EstadoPedido.Completado;

    public static decimal Total(PedidoDto pedido) => pedido.Detalles.Sum(x => x.Subtotal);

    public static string Moneda(decimal valor) => $"Q {valor:N2}";
}
