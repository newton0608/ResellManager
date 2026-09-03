using System.ComponentModel.DataAnnotations;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;

namespace ResellManager.Web.Components.Compras;

public sealed class CompraFormModel
{
    public DateOnly FechaCompra { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public OrigenCompra Origen { get; set; } = OrigenCompra.CompraLocal;

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un proveedor válido.")]
    public int ProveedorId { get; set; }

    public DateOnly? FechaIngreso { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [StringLength(500, ErrorMessage = "Las observaciones permiten hasta 500 caracteres.")]
    public string? Observaciones { get; set; }

    public List<DetalleCompraFormModel> Detalles { get; } = [new()];

    public bool AdjuntarComprobante { get; set; }

    [StringLength(100, ErrorMessage = "El número de documento permite hasta 100 caracteres.")]
    public string? NumeroDocumento { get; set; }

    public DateOnly FechaComprobante { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [StringLength(500, ErrorMessage = "Las observaciones permiten hasta 500 caracteres.")]
    public string? ObservacionesComprobante { get; set; }

    public decimal TotalVisual => Detalles.Sum(x => x.Cantidad * x.CostoUnitario);

    public CompraInput ToInput(string codigoInterno) =>
        new(
            codigoInterno,
            FechaCompra,
            Origen is OrigenCompra.CompraLocal or OrigenCompra.EnvioHermano
                ? FechaIngreso
                : null,
            Origen,
            ProveedorId,
            Observaciones,
            Detalles
                .Select(x => new DetalleCompraInput(x.ProductoId, x.Cantidad, x.CostoUnitario))
                .ToList(),
            null
        );

    public DatosComprobanteCompraInput? ToDatosComprobante() =>
        AdjuntarComprobante
            ? new DatosComprobanteCompraInput(
                NumeroDocumento,
                FechaComprobante,
                ObservacionesComprobante
            )
            : null;
}

public sealed class DetalleCompraFormModel
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; } = 1;
    public decimal CostoUnitario { get; set; }
    public decimal Subtotal => Cantidad * CostoUnitario;
}
