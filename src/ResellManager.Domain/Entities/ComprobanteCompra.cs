namespace ResellManager.Domain.Entities;

public class ComprobanteCompra
{
    public int Id { get; set; }
    public string? NumeroDocumento { get; set; }
    public DateOnly Fecha { get; set; }
    public string RutaDocumento { get; set; } = string.Empty;
    public string? Observaciones { get; set; }
    public int CompraId { get; set; }

    public Compra Compra { get; set; } = null!;
}
