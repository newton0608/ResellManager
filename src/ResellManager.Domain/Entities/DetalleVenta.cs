namespace ResellManager.Domain.Entities;

public class DetalleVenta
{
    public int Id { get; set; }
    public decimal PrecioFinal { get; set; }
    public string? Observaciones { get; set; }
    public int VentaId { get; set; }
    public int? UnidadInventarioId { get; set; }
    public int? ProductoId { get; set; }
    public decimal? CostoUnitario { get; set; }

    public Venta Venta { get; set; } = null!;
    public UnidadInventario? UnidadInventario { get; set; }
    public Producto? Producto { get; set; }

    public decimal CalcularGanancia(decimal costo)
    {
        return PrecioFinal - costo;
    }
}
