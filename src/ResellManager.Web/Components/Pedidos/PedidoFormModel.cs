using System.ComponentModel.DataAnnotations;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;

namespace ResellManager.Web.Components.Pedidos;

public sealed class PedidoFormModel
{
    [Required(ErrorMessage = "El código interno es obligatorio.")]
    [StringLength(80, ErrorMessage = "El código interno no puede exceder 80 caracteres.")]
    public string CodigoInterno { get; set; } = string.Empty;

    public DateOnly Fecha { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public TipoPedido TipoPedido { get; set; } = TipoPedido.Importacion;

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un cliente.")]
    public int ClienteId { get; set; }

    [StringLength(1000, ErrorMessage = "Las observaciones no pueden exceder 1000 caracteres.")]
    public string? Observaciones { get; set; }

    public List<DetallePedidoFormModel> Detalles { get; } = [new()];

    public PedidoInput ToInput() =>
        new(
            CodigoInterno,
            Fecha,
            TipoPedido,
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
