namespace ResellManager.Domain.Entities;

public class Categoria
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Observaciones { get; set; }
    public int? CategoriaPadreId { get; set; }
    public Categoria? CategoriaPadre { get; set; }
    public ICollection<Categoria> Subcategorias { get; set; } = [];

    public ICollection<Producto> Productos { get; set; } = [];
}
