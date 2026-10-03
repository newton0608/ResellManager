using System.ComponentModel.DataAnnotations;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;

namespace ResellManager.Web.Components.Compras;

public sealed class CompraFormModel
{
    public DateOnly FechaCompra { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public OrigenCompra Origen { get; set; } = OrigenCompra.CompraLocal;
    public MonedaCompra Moneda { get; set; } = MonedaCompra.GTQ;
    public decimal TipoCambio { get; set; } = 1m;
    public bool TipoCambioModificadoManualmente { get; private set; }
    public TipoCambioReferenciaDto? Referencia { get; private set; }

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

    public decimal TotalVisual => TotalVisualMonedaOrigen ?? 0m;
    public decimal? TotalVisualMonedaOrigen => SumarSeguro(x => x.Subtotal);
    public decimal? TotalVisualGtq => Moneda == MonedaCompra.GTQ
        ? TotalVisualMonedaOrigen
        : TipoCambio > 0 ? SumarSeguro(x => x.SubtotalGtq(TipoCambio)) : null;

    public void CambiarMoneda(MonedaCompra moneda)
    {
        if (Moneda == moneda) return;
        Moneda = moneda;
        TipoCambio = moneda == MonedaCompra.GTQ ? 1m : 0m;
        TipoCambioModificadoManualmente = false;
        Referencia = null;
    }

    public void MarcarTipoCambioManual() => TipoCambioModificadoManualmente = true;

    public void PrepararConsultaReferencia()
    {
        Referencia = null;
        if (Moneda == MonedaCompra.USD && !TipoCambioModificadoManualmente)
            TipoCambio = 0m;
    }

    public void ActualizarReferencia(TipoCambioReferenciaDto referencia)
    {
        if (Moneda != MonedaCompra.USD || referencia.Moneda != Moneda ||
            referencia.FechaSolicitada != FechaCompra || referencia.FechaEfectiva > FechaCompra || referencia.Valor <= 0)
            return;
        Referencia = referencia;
        if (!TipoCambioModificadoManualmente) TipoCambio = referencia.Valor;
    }

    public void UsarReferencia()
    {
        if (Referencia is not { } referencia || referencia.FechaSolicitada != FechaCompra) return;
        TipoCambio = referencia.Valor;
        TipoCambioModificadoManualmente = false;
    }

    public CompraInput ToInput(string codigoInterno) =>
        new(
            codigoInterno,
            FechaCompra,
            Origen is OrigenCompra.CompraLocal or OrigenCompra.EnvioHermano ? FechaIngreso : null,
            Origen,
            ProveedorId,
            Observaciones,
            Detalles.Select(x => new DetalleCompraInput(x.ProductoId, x.Cantidad, x.CostoUnitarioMonedaOrigen)).ToList(),
            null
        )
        {
            Moneda = Moneda,
            TipoCambio = TipoCambio,
            TipoCambioReferencia = ReferenciaActual?.Valor,
            FechaTipoCambioReferencia = ReferenciaActual?.FechaEfectiva,
            FuenteTipoCambio = ReferenciaActual?.Fuente,
        };

    private TipoCambioReferenciaDto? ReferenciaActual =>
        Moneda == MonedaCompra.USD && Referencia?.FechaSolicitada == FechaCompra ? Referencia : null;

    private decimal? SumarSeguro(Func<DetalleCompraFormModel, decimal?> calcular)
    {
        try
        {
            decimal total = 0;
            foreach (var detalle in Detalles)
            {
                var subtotal = calcular(detalle);
                if (subtotal is null) return null;
                total = checked(total + subtotal.Value);
            }
            return total;
        }
        catch (OverflowException) { return null; }
    }

    public DatosComprobanteCompraInput? ToDatosComprobante() =>
        AdjuntarComprobante ? new DatosComprobanteCompraInput(NumeroDocumento, FechaComprobante, ObservacionesComprobante) : null;
}

public sealed class DetalleCompraFormModel
{
    public ProductoDto? ProductoSeleccionado { get; set; }
    public int ProductoId { get; set; }
    public int Cantidad { get; set; } = 1;
    public decimal CostoUnitarioMonedaOrigen { get; set; }
    // Conserva el contrato del formulario previo; siempre representa el importe que el usuario introduce.
    public decimal CostoUnitario { get => CostoUnitarioMonedaOrigen; set => CostoUnitarioMonedaOrigen = value; }
    public decimal Subtotal => checked(Cantidad * CostoUnitarioMonedaOrigen);

    public decimal? CostoUnitarioGtq(decimal tipoCambio)
    {
        try { return ConversionMonedaCompra.CostoUnitarioGtq(CostoUnitarioMonedaOrigen, tipoCambio); }
        catch (OverflowException) { return null; }
    }

    public decimal? SubtotalGtq(decimal tipoCambio)
    {
        try { return checked(Cantidad * CostoUnitarioGtq(tipoCambio)); }
        catch (OverflowException) { return null; }
    }
}
