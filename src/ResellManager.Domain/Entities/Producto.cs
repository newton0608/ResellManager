namespace ResellManager.Domain.Entities;

public class Producto
{
    public int Id { get; set; }
    public string CodigoInterno { get; set; } = string.Empty;
    public string? CodigoBarras { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? Color { get; set; }
    public string? Talla { get; set; }
    public int CategoriaId { get; set; }

    public Categoria Categoria { get; set; } = null!;
    public ICollection<UnidadInventario> UnidadesInventario { get; set; } = [];
    public ICollection<DetalleCompra> DetallesCompra { get; set; } = [];
    public ICollection<DetallePedido> DetallesPedido { get; set; } = [];
}
