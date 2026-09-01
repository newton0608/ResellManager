using System.ComponentModel.DataAnnotations;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;

namespace ResellManager.Web.Components.Pagos;

public sealed class PagoFormModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un cliente.")]
    public int ClienteId { get; set; }

    public DateOnly Fecha { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Monto { get; set; }

    public MetodoPago MetodoPago { get; set; } = MetodoPago.Efectivo;

    [StringLength(100, ErrorMessage = "La referencia no puede exceder 100 caracteres.")]
    public string? Referencia { get; set; }

    [StringLength(500, ErrorMessage = "Las observaciones no pueden exceder 500 caracteres.")]
    public string? Observaciones { get; set; }

    public PagoInput ToInput() =>
        new(ClienteId, Fecha, Monto, MetodoPago, Referencia, Observaciones);
}
