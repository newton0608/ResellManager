using ResellManager.Domain.Enums;

namespace ResellManager.Domain.Entities;

public class Pedido
{
    public int Id { get; set; }
    public string CodigoInterno { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public TipoPedido TipoPedido { get; set; }
    public EstadoPedido Estado { get; set; }
    public string? Observaciones { get; set; }
    public int ClienteId { get; set; }

    public Cliente Cliente { get; set; } = null!;
    public ICollection<DetallePedido> Detalles { get; set; } = [];
    public Venta? Venta { get; set; }
}
