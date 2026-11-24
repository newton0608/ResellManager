namespace ResellManager.Domain.Entities;
public class GrupoVariantes
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = "";
    public ICollection<VinculoVariante> Variantes { get; set; } = [];
}
public class VinculoVariante
{
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;
    public Guid GrupoId { get; set; }
    public GrupoVariantes Grupo { get; set; } = null!;
    public string AtributosJson { get; set; } = "{}";
    public string Firma { get; set; } = "";
}
public class CambioPrecioProducto
{
    public Guid Id { get; set; }
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;
    public decimal Anterior { get; set; }
    public decimal Nuevo { get; set; }
    public string UsuarioId { get; set; } = "";
    public DateTime RegistradoUtc { get; set; }
}
