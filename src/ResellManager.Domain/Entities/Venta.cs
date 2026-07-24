using ResellManager.Domain.Enums;

namespace ResellManager.Domain.Entities;

public class Venta
{
    public int Id { get; set; }
    public string CodigoInterno { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public EstadoVenta Estado { get; set; }
    public string? Observaciones { get; set; }
    public int PedidoId { get; set; }

    public Pedido Pedido { get; set; } = null!;
    public ICollection<DetalleVenta> Detalles { get; set; } = [];

    public decimal CalcularTotal()
    {
        return Detalles.Sum(detalle => detalle.PrecioFinal);
    }
}
