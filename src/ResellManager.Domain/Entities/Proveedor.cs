namespace ResellManager.Domain.Entities;

public class Proveedor
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? CodigoPais { get; set; }
    public string? Descripcion { get; set; }

    public ICollection<Compra> Compras { get; set; } = [];
}
