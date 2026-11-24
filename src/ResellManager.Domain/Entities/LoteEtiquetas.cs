namespace ResellManager.Domain.Entities;
public class LoteEtiquetas
{
    public Guid Id { get; set; }
    public string ContenidoJson { get; set; } = "";
    public string UsuarioId { get; set; } = "";
    public DateTime CreadoUtc { get; set; }
    public DateTime? ImpresoUtc { get; set; }
    public string? ImpresoPor { get; set; }
}
