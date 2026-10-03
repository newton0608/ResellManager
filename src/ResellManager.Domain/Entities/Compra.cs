using ResellManager.Domain.Enums;

namespace ResellManager.Domain.Entities;

public class Compra
{
    public int Id { get; set; }
    public string CodigoInterno { get; set; } = string.Empty;
    public DateOnly FechaCompra { get; set; }
    public OrigenCompra Origen { get; set; }
    public MonedaCompra Moneda { get; set; } = MonedaCompra.GTQ;
    /// <summary>GTQ por una unidad de origen; queda fijado al registrar la compra.</summary>
    public decimal TipoCambio { get; set; } = 1m;
    public decimal? TipoCambioReferencia { get; set; }
    public DateOnly? FechaTipoCambioReferencia { get; set; }
    public string? FuenteTipoCambio { get; set; }
    public decimal TotalMonedaOrigen { get; set; }
    /// <summary>Total operativo en GTQ, sumado desde los costos unitarios convertidos.</summary>
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }
    public int ProveedorId { get; set; }

    public Proveedor Proveedor { get; set; } = null!;
    public ICollection<DetalleCompra> Detalles { get; set; } = [];
    public ComprobanteCompra? Comprobante { get; set; }
}
