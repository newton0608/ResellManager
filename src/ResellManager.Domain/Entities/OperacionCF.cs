using ResellManager.Domain.Enums;
namespace ResellManager.Domain.Entities;

public class CobroVenta
{
    public Guid Id { get; set; }
    public int VentaId { get; set; }
    public Venta Venta { get; set; } = null!;
    public MetodoPago Metodo { get; set; }
    public decimal Aplicado { get; set; }
    public decimal Entregado { get; set; }
    public string? Referencia { get; set; }
    public string UsuarioId { get; set; } = "";
    public DateTime RegistradoUtc { get; set; }
}
public enum EstadoAnulacionCF { Pendiente = 1, Aprobada = 2, Rechazada = 3, Completada = 4 }
public class AnulacionCF
{
    public Guid Id { get; set; }
    public int VentaId { get; set; }
    public Venta Venta { get; set; } = null!;
    public EstadoAnulacionCF Estado { get; set; }
    public string Motivo { get; set; } = "";
    public string SolicitanteId { get; set; } = "";
    public DateTime SolicitadaUtc { get; set; }
    public string? AdministradorId { get; set; }
    public DateTime? DecididaUtc { get; set; }
    public string? MotivoDecision { get; set; }
    public DateTime? CompletadaUtc { get; set; }
    public ICollection<DevolucionDineroCF> Reembolsos { get; set; } = [];
    public ICollection<RecepcionDevolucionCF> Recepciones { get; set; } = [];
}
public class DevolucionDineroCF
{
    public Guid Id { get; set; }
    public Guid AnulacionId { get; set; }
    public AnulacionCF Anulacion { get; set; } = null!;
    public decimal Importe { get; set; }
    public MetodoPago Metodo { get; set; }
    public string Referencia { get; set; } = "";
    public string UsuarioId { get; set; } = "";
    public DateTime RegistradoUtc { get; set; }
    public DateOnly FechaEfectiva { get; set; }
}
public class RecepcionDevolucionCF
{
    public Guid Id { get; set; }
    public Guid AnulacionId { get; set; }
    public AnulacionCF Anulacion { get; set; } = null!;
    public int UnidadId { get; set; }
    public UnidadInventario Unidad { get; set; } = null!;
    public bool Apta { get; set; }
    public string Observacion { get; set; } = "";
    public string UsuarioId { get; set; } = "";
    public DateTime RegistradoUtc { get; set; }
}
