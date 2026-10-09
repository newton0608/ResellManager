namespace ResellManager.Domain.Entities;

public sealed class ProductoImagen
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ProductoId { get; set; }
    public string RutaRelativa { get; set; } = string.Empty;
    public int Orden { get; set; }
    public Producto Producto { get; set; } = null!;
}
