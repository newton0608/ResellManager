using System.ComponentModel.DataAnnotations;
using ResellManager.Application.DTOs;

namespace ResellManager.Web.Components.Ventas;

public sealed class VentaFormModel
{
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
public sealed class VentaDirectaFormModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un cliente.")]
    public int ClienteId { get; set; }

    public DateOnly Fecha { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [StringLength(500, ErrorMessage = "Las observaciones no pueden exceder 500 caracteres.")]
    public string? Observaciones { get; set; }
}

public sealed class UnidadVentaDirectaFormModel
{
    public required UnidadInventarioDto Unidad { get; init; }
    public Guid LoteId { get; init; } = Guid.NewGuid();
    public bool Seleccionada { get; set; }
    public decimal PrecioFinal { get; set; }

    [StringLength(500, ErrorMessage = "Las observaciones no pueden exceder 500 caracteres.")]
    public string? Observaciones { get; set; }

    public DetallePedidoInput ToPedidoInput() =>
        new(
            Unidad.ProductoId,
            1,
            PrecioFinal,
            Observaciones
        );

    public DetalleVentaInput ToVentaInput() =>
        new(
            Unidad.Id,
            Unidad.ProductoId,
            null,
            PrecioFinal,
            Observaciones
        );
}

// Agrupación de presentación por acción; los inputs persistidos siguen siendo por unidad.
public sealed class LoteVentaDirectaFormModel(IReadOnlyList<UnidadVentaDirectaFormModel> unidades)
{
    public Guid Id => unidades[0].LoteId;
    public string Producto => unidades[0].Unidad.Producto;
    public IReadOnlyList<UnidadVentaDirectaFormModel> Unidades => unidades;
    public int Cantidad => unidades.Count;
    public decimal PrecioFinal
    {
        get => unidades[0].PrecioFinal;
        set { foreach (var unidad in unidades) unidad.PrecioFinal = value; }
    }
    public decimal Subtotal => unidades.Sum(x => x.PrecioFinal);
    public static IReadOnlyList<LoteVentaDirectaFormModel> Agrupar(IEnumerable<UnidadVentaDirectaFormModel> unidades) =>
        unidades.GroupBy(x => x.LoteId).Select(grupo => new LoteVentaDirectaFormModel(grupo.ToArray())).ToArray();
}
