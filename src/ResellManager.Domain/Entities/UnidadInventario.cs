using ResellManager.Domain.Enums;

namespace ResellManager.Domain.Entities;

public class UnidadInventario
{
    public int Id { get; set; }
    public string CodigoInterno { get; set; } = string.Empty;
    public EstadoUnidadInventario Estado { get; set; }
    public DateOnly? FechaIngreso { get; set; }
    public decimal Costo { get; set; }
    public int ProductoId { get; set; }
    public int DetalleCompraId { get; set; }

    public Producto Producto { get; set; } = null!;
    public DetalleCompra DetalleCompra { get; set; } = null!;
    public ICollection<DetalleVenta> DetallesVenta { get; set; } = [];
}
