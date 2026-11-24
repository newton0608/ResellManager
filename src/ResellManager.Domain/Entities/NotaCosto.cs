namespace ResellManager.Domain.Entities;
public class NotaCosto
{
    public Guid Id { get; set; }
    public int UnidadId { get; set; }
    public UnidadInventario Unidad { get; set; } = null!;
    public decimal? Anterior { get; set; }
    public decimal Nuevo { get; set; }
    public DateOnly FechaConocimiento { get; set; }
    public DateTime RegistradoUtc { get; set; }
    public string UsuarioId { get; set; } = "";
    public string Motivo { get; set; } = "";
    public string? Referencia { get; set; }
    public ICollection<CorreccionDetalleCosto> Detalles { get; set; } = [];
}
public class CorreccionDetalleCosto
{
    public Guid NotaId { get; set; }
    public NotaCosto Nota { get; set; } = null!;
    public int DetalleVentaId { get; set; }
    public DetalleVenta DetalleVenta { get; set; } = null!;
}
public class MigracionCostosHistoricos
{
    public Guid Id { get; set; }
    public string AlcanceJson { get; set; } = "";
    public string Respaldo { get; set; } = "";
    public string Motivo { get; set; } = "";
    public string UsuarioId { get; set; } = "";
    public DateTime RegistradoUtc { get; set; }
}
