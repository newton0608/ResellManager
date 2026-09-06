using System.ComponentModel.DataAnnotations;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;

namespace ResellManager.Web.Components.Pedidos;

public sealed class PedidoFormModel
{
    public DateOnly Fecha { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public TipoPedido TipoPedido { get; set; } = TipoPedido.Importacion;

    [EnumDataType(typeof(CanalVenta), ErrorMessage = "Selecciona un canal de venta válido.")]
    public CanalVenta CanalVenta { get; set; } = CanalVenta.Presencial;

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un cliente.")]
    public int ClienteId { get; set; }

    [StringLength(500, ErrorMessage = "Las observaciones no pueden exceder 500 caracteres.")]
    public string? Observaciones { get; set; }

    public List<DetallePedidoFormModel> Detalles { get; } = [new()];

    public PedidoInput ToInput(string codigoInterno) =>
        new(
            codigoInterno,
            Fecha,
            TipoPedido,
            CanalVenta,
            ClienteId,
            Observaciones,
            Detalles.Select(x => x.ToInput()).ToArray()
        );
}

public sealed class DetallePedidoFormModel
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; } = 1;
    public decimal PrecioUnitario { get; set; }
    public string? Observaciones { get; set; }

    public DetallePedidoInput ToInput() =>
        new(ProductoId, Cantidad, PrecioUnitario, Observaciones);
}
