using ResellManager.Domain.Enums;

namespace ResellManager.Domain.Entities;

public class Compra
{
    public int Id { get; set; }
    public string CodigoInterno { get; set; } = string.Empty;
    public DateOnly FechaCompra { get; set; }
    public OrigenCompra Origen { get; set; }
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }
    public int ProveedorId { get; set; }

    public Proveedor Proveedor { get; set; } = null!;
    public ICollection<DetalleCompra> Detalles { get; set; } = [];
    public ComprobanteCompra? Comprobante { get; set; }
}
