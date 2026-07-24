using ResellManager.Domain.Enums;

namespace ResellManager.Domain.Entities;

public class Pago
{
    public int Id { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal Monto { get; set; }
    public MetodoPago MetodoPago { get; set; }
    public string? Referencia { get; set; }
    public string? Observaciones { get; set; }
    public int ClienteId { get; set; }

    public Cliente Cliente { get; set; } = null!;
}
