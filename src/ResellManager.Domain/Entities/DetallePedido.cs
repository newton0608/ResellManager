namespace ResellManager.Domain.Entities;

public class DetallePedido
{
    public int Id { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public string? Observaciones { get; set; }
    public int PedidoId { get; set; }
    public int ProductoId { get; set; }

    public Pedido Pedido { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
    public ICollection<UnidadInventario> UnidadesReservadas { get; set; } = [];

    public decimal CalcularSubtotal()
    {
        return Cantidad * PrecioUnitario;
    }
}
