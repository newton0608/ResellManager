namespace ResellManager.Domain.Entities;

public class DetalleCompra
{
    public int Id { get; set; }
    public int Cantidad { get; set; }
    public decimal CostoUnitarioMonedaOrigen { get; set; }
    /// <summary>Costo unitario operativo en GTQ; las unidades de inventario heredan este valor.</summary>
    public decimal CostoUnitario { get; set; }
    public int CompraId { get; set; }
    public int ProductoId { get; set; }

    public Compra Compra { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
    public ICollection<UnidadInventario> UnidadesInventario { get; set; } = [];

    public decimal CalcularSubtotal()
    {
        return Cantidad * CostoUnitario;
    }
}
