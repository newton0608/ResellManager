using System.ComponentModel.DataAnnotations;
using ResellManager.Application.DTOs;

namespace ResellManager.Web.Components.Ventas;

public sealed class VentaFormModel
{
    [Required(ErrorMessage = "El código interno es obligatorio.")]
    [StringLength(50, ErrorMessage = "El código no puede exceder 50 caracteres.")]
    public string CodigoInterno { get; set; } = string.Empty;

    public DateOnly Fecha { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [StringLength(500, ErrorMessage = "Las observaciones no pueden exceder 500 caracteres.")]
    public string? Observaciones { get; set; }
}

public sealed class DetalleVentaFormModel
{
    public required int DetallePedidoId { get; init; }
    public required int ProductoId { get; init; }
    public required string Producto { get; init; }
    public required int Numero { get; init; }
    public int? UnidadInventarioId { get; set; }
    public decimal? CostoUnitario { get; set; }
    public decimal PrecioFinal { get; set; }
    public string? Observaciones { get; set; }

    public DetalleVentaInput ToInput(bool catalogo) =>
        new(
            catalogo ? null : UnidadInventarioId,
            ProductoId,
            catalogo ? CostoUnitario : null,
            PrecioFinal,
            Observaciones
        );
}
