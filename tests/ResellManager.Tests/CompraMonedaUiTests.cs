using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Enums;
using ResellManager.Web.Components.Compras;
using ResellManager.Web.Components.Pages;
using ResellManager.Web.Components.Shared;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed class CompraMonedaUiTests
{
    [Fact]
    public void GtqInicial_OrigenNoReinterpretaCostosYConservaExperiencia()
    {
        using var pagina = Preparar();
        var modelo = Get<CompraFormModel>(pagina, "Modelo");
        modelo.Detalles[0].CostoUnitarioMonedaOrigen = 50m;
        modelo.Detalles[0].Cantidad = 2;
        Call(pagina, "CambiarOrigen", new ChangeEventArgs { Value = "Importacion" });
        Assert.Equal(MonedaCompra.GTQ, modelo.Moneda);
        Assert.Equal(1m, modelo.TipoCambio);
        Assert.Equal(100m, modelo.TotalVisualGtq);
        var texto = TextoRenderizado(pagina);
        Assert.Contains("Q 100.00", texto);
        Assert.DoesNotContain("Referencia Banco de Guatemala", texto);
        var input = modelo.ToInput("COM-GTQ");
        Assert.Equal(50m, Assert.Single(input.Detalles).CostoUnitarioMonedaOrigen);
        Assert.Null(input.TipoCambioReferencia);
    }

    [Fact]
    public void UsdPreview_RedondeaCadaUnidadYLuegoSumaDetalles()
    {
        var modelo = new CompraFormModel();
        modelo.CambiarMoneda(MonedaCompra.USD);
        modelo.TipoCambio = 7.67m;
        modelo.Detalles[0].Cantidad = 3;
        modelo.Detalles[0].CostoUnitarioMonedaOrigen = 12.50m;
        modelo.Detalles.Add(new() { Cantidad = 2, CostoUnitarioMonedaOrigen = 0.50m });
        Assert.Equal(38.50m, modelo.TotalVisualMonedaOrigen);
        Assert.Equal(95.88m, modelo.Detalles[0].CostoUnitarioGtq(modelo.TipoCambio));
        Assert.Equal(295.32m, modelo.TotalVisualGtq);
        Assert.NotEqual(decimal.Round(38.50m * 7.67m, 2, MidpointRounding.AwayFromZero), modelo.TotalVisualGtq);
        modelo.Detalles[0].Cantidad = 1;
        modelo.TipoCambio = 8m;
        Assert.Equal(100m, modelo.Detalles[0].SubtotalGtq(modelo.TipoCambio));
    }

    [Fact]
    public void Referencia_ManualSeConservaAlCambiarFecha_UsarReferenciaRestauraSugerencia()
    {
        var modelo = new CompraFormModel { FechaCompra = new(2026, 10, 3) };
        modelo.CambiarMoneda(MonedaCompra.USD);
        modelo.ActualizarReferencia(Referencia(modelo.FechaCompra, 7.64136m, new(2026, 10, 2)));
        Assert.Equal(7.64136m, modelo.TipoCambio);
        modelo.TipoCambio = 7.78m;
        modelo.MarcarTipoCambioManual();
        modelo.FechaCompra = new(2026, 10, 2);
        modelo.PrepararConsultaReferencia();
        Assert.Null(modelo.ToInput("COM-MANUAL").TipoCambioReferencia);
        modelo.ActualizarReferencia(Referencia(modelo.FechaCompra, 7.65m));
        Assert.Equal(7.78m, modelo.TipoCambio);
        var input = modelo.ToInput("COM-MANUAL");
        Assert.Equal(7.78m, input.TipoCambio);
        Assert.Equal(7.65m, input.TipoCambioReferencia);
        Assert.Equal(new DateOnly(2026, 10, 2), input.FechaTipoCambioReferencia);
        Assert.Equal("Banco de Guatemala", input.FuenteTipoCambio);
        modelo.UsarReferencia();
        Assert.Equal(7.65m, modelo.TipoCambio);
        Assert.False(modelo.TipoCambioModificadoManualmente);
        modelo.FechaCompra = new(2026, 10, 1);
        modelo.PrepararConsultaReferencia();
        modelo.ActualizarReferencia(Referencia(modelo.FechaCompra, 7.6m));
        Assert.Equal(7.6m, modelo.TipoCambio);
    }

    [Fact]
    public async Task ConsultaGtq_NoConsultaProveedor_CambioExplicitoAUsdNoModificaCostos()
    {
        var servicio = new ReferenciaFalsa();
        using var pagina = Preparar(servicio);
        var modelo = Get<CompraFormModel>(pagina, "Modelo");
        modelo.Detalles[0].CostoUnitarioMonedaOrigen = 12.50m;
        await CallAsync(pagina, "ConsultarReferenciaAsync");
        Assert.Empty(servicio.Pendientes);
        var tarea = CallAsync(pagina, "CambiarMonedaAsync", new ChangeEventArgs { Value = "USD" });
        Assert.True(Get<bool>(pagina, "ConsultandoReferencia"));
        Assert.Contains("Consultando referencia", TextoRenderizado(pagina));
        Assert.Equal(12.50m, modelo.Detalles[0].CostoUnitarioMonedaOrigen);
        servicio.Completar(0, Referencia(modelo.FechaCompra, 7.67m));
        await tarea;
        Assert.Equal(7.67m, modelo.TipoCambio);
        await CallAsync(pagina, "CambiarMonedaAsync", new ChangeEventArgs { Value = "GTQ" });
        Assert.Single(servicio.Pendientes);
        Assert.Equal(1m, modelo.TipoCambio);
        Assert.Null(modelo.Referencia);
        Assert.False(Get<bool>(pagina, "ConsultandoReferencia"));
        Assert.Equal(12.50m, modelo.Detalles[0].CostoUnitarioMonedaOrigen);
    }

    [Fact]
    public async Task CambioRapidoFecha_RespuestaAntiguaNuncaSobrescribeLaNueva()
    {
        var servicio = new ReferenciaFalsa();
        using var pagina = Preparar(servicio);
        var modelo = Get<CompraFormModel>(pagina, "Modelo");
        modelo.CambiarMoneda(MonedaCompra.USD);
        modelo.FechaCompra = new(2026, 10, 2);
        var anterior = CallAsync(pagina, "ConsultarReferenciaAsync");
        modelo.FechaCompra = new(2026, 10, 3);
        var actual = CallAsync(pagina, "ConsultarReferenciaAsync");
        Assert.True(servicio.Pendientes[0].Cancelacion.IsCancellationRequested);
        servicio.Completar(1, Referencia(modelo.FechaCompra, 7.64136m, new(2026, 10, 2)));
        await actual;
        servicio.Completar(0, Referencia(new(2026, 10, 2), 8m)); // Simula proveedor que ignora cancelación.
        await anterior;
        Assert.Equal(7.64136m, modelo.TipoCambio);
        Assert.Equal(new DateOnly(2026, 10, 3), modelo.Referencia!.FechaSolicitada);
        Assert.Equal(new DateOnly(2026, 10, 2), modelo.Referencia.FechaEfectiva);
        Assert.False(Get<bool>(pagina, "ConsultandoReferencia"));
    }

    [Fact]
    public async Task TipoManualDuranteConsulta_NoSeSobrescribeYPermiteUsarReferencia()
    {
        var servicio = new ReferenciaFalsa();
        using var pagina = Preparar(servicio);
        var modelo = Get<CompraFormModel>(pagina, "Modelo");
        modelo.CambiarMoneda(MonedaCompra.USD);
        var tarea = CallAsync(pagina, "ConsultarReferenciaAsync");
        modelo.TipoCambio = 7.78m;
        modelo.MarcarTipoCambioManual();
        servicio.Completar(0, Referencia(modelo.FechaCompra, 7.64136m));
        await tarea;
        Assert.Equal(7.78m, modelo.TipoCambio);
        Assert.Contains("Usar referencia", TextoRenderizado(pagina));
        modelo.UsarReferencia();
        Assert.Equal(7.64136m, modelo.TipoCambio);
        Assert.DoesNotContain("Usar referencia", TextoRenderizado(pagina));
    }

    [Fact]
    public async Task FallaReferencia_FormularioPermiteRevisarConTipoManual()
    {
        var servicio = new ReferenciaFalsa();
        using var pagina = Preparar(servicio);
        var modelo = Get<CompraFormModel>(pagina, "Modelo");
        modelo.CambiarMoneda(MonedaCompra.USD);
        var tarea = CallAsync(pagina, "ConsultarReferenciaAsync");
        servicio.Pendientes[0].Resultado.SetResult(TipoCambioReferenciaResultado.NoDisponible());
        await tarea;
        Assert.Contains(TipoCambioReferenciaResultado.MensajeEntradaManual, TextoRenderizado(pagina));
        modelo.TipoCambio = 7.67m;
        modelo.MarcarTipoCambioManual();
        modelo.Detalles[0].CostoUnitarioMonedaOrigen = 12.50m;
        Call(pagina, "RevisarCompra");
        Assert.True(Get<bool>(pagina, "RevisandoCompra"));
        Assert.Null(modelo.ToInput("COM-FALLBACK").TipoCambioReferencia);
    }

    [Fact]
    public void RevisionUsd_ExplicaCostoInventarioYAmbosTotales()
    {
        using var pagina = Preparar();
        var modelo = Get<CompraFormModel>(pagina, "Modelo");
        modelo.CambiarMoneda(MonedaCompra.USD);
        modelo.Detalles[0].Cantidad = 2;
        modelo.Detalles[0].CostoUnitarioMonedaOrigen = 12.50m;
        modelo.ActualizarReferencia(Referencia(modelo.FechaCompra, 7.64136m));
        modelo.TipoCambio = 7.67m;
        modelo.MarcarTipoCambioManual();
        Call(pagina, "RevisarCompra");
        Assert.True(Get<bool>(pagina, "RevisandoCompra"));
        var texto = TextoRenderizado(pagina);
        Assert.Contains("US$ 25.00", texto);
        Assert.Contains("Q 191.76", texto);
        Assert.Contains("Q 95.88", texto);
        Assert.Contains("Q7.67000 por US$1", texto);
        Assert.Contains("Referencia Banguat", texto);
        Assert.Contains("Fecha efectiva", texto);
        Assert.Contains("Total moneda origen", texto);
        Assert.Contains("Total registrado en GTQ", texto);
        Assert.Contains("El inventario y la utilidad se registrarán usando el costo convertido a GTQ.", texto);
    }

    [Theory]
    [InlineData(MonedaCompra.USD, 0)]
    [InlineData(MonedaCompra.USD, -1)]
    [InlineData(MonedaCompra.GTQ, 2)]
    public void Revision_RechazaTipoCambioInvalido(MonedaCompra moneda, int tipo)
    {
        using var pagina = Preparar();
        var modelo = Get<CompraFormModel>(pagina, "Modelo");
        modelo.Moneda = moneda;
        modelo.TipoCambio = tipo;
        Call(pagina, "RevisarCompra");
        Assert.False(Get<bool>(pagina, "RevisandoCompra"));
        Assert.NotNull(Get<string?>(pagina, "ErrorGuardado"));
    }

    [Theory]
    [InlineData(MonedaCompra.GTQ)]
    [InlineData(MonedaCompra.USD)]
    public void Revision_CostoOrigenConMasDeDosDecimalesSeRechaza(MonedaCompra moneda)
    {
        using var pagina = Preparar();
        var modelo = Get<CompraFormModel>(pagina, "Modelo");
        modelo.Moneda = moneda;
        modelo.TipoCambio = moneda == MonedaCompra.GTQ ? 1m : 7.67m;
        modelo.Detalles[0].CostoUnitarioMonedaOrigen = 12.501m;
        Call(pagina, "RevisarCompra");
        Assert.False(Get<bool>(pagina, "RevisandoCompra"));
        Assert.Equal("El costo unitario debe tener como máximo dos decimales.", Get<string?>(pagina, "ErrorGuardado"));
    }

    [Fact]
    public void ImportesFueraDeRango_NoRompenRenderNiPermitenRevision()
    {
        using var pagina = Preparar();
        var modelo = Get<CompraFormModel>(pagina, "Modelo");
        modelo.CambiarMoneda(MonedaCompra.USD);
        modelo.TipoCambio = decimal.MaxValue;
        modelo.Detalles[0].Cantidad = 2;
        modelo.Detalles[0].CostoUnitarioMonedaOrigen = decimal.MaxValue;
        Assert.Null(modelo.TotalVisualGtq);
        Assert.Null(modelo.TotalVisualMonedaOrigen);
        Assert.Contains("—", TextoRenderizado(pagina));
        Call(pagina, "RevisarCompra");
        Assert.False(Get<bool>(pagina, "RevisandoCompra"));
    }

    [Fact]
    public void PresentacionTipoCambio_ConservaPrecisionAplicadaYEsInvariante()
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-GT");
            Assert.Equal("Q7.6413612345678901234567890123", CompraPresentacion.TipoCambio(7.6413612345678901234567890123m));
            Assert.Equal("Q0.0000001", CompraPresentacion.TipoCambio(0.0000001m));
            Assert.Equal("Q7.67000", CompraPresentacion.TipoCambio(7.67m));
            Assert.Equal("US$ 12.50", CompraPresentacion.MonedaOrigen(12.5m, MonedaCompra.USD));
        }
        finally { CultureInfo.CurrentCulture = anterior; }
    }

    [Theory]
    [InlineData(MonedaCompra.GTQ)]
    [InlineData(MonedaCompra.USD)]
    public void ListaCompras_MuestraOrigenUsdYTotalOperativoGtqSinCambiarAgrupacion(MonedaCompra moneda)
    {
        var compra = CompraRegistrada(moneda);
        var pagina = new Compras();
        IReadOnlyList<CompraDto> compras = [compra];
        var grupos = HistorialMensual.Agrupar(compras, x => x.FechaCompra, x => x.Id);
        Set(pagina, "ComprasRegistradas", compras);
        Set(pagina, "Grupos", grupos);
        Get<MesesDesplegables>(pagina, "Meses").Reiniciar(grupos);
        Set(pagina, "Cargando", false);
        var texto = TextoRenderizado(pagina);
        Assert.Contains("Octubre 2026", texto);
        Assert.Contains("Q 191.76", texto);
        if (moneda == MonedaCompra.USD)
        {
            Assert.Contains("US$ 25.00", texto);
            Assert.Contains("USD", texto);
        }
        else Assert.DoesNotContain("US$", texto);
    }

    [Theory]
    [InlineData(MonedaCompra.GTQ)]
    [InlineData(MonedaCompra.USD)]
    public void CompraDetalle_ConservaTipoGuardadoYCostosAmbosSegunMoneda(MonedaCompra moneda)
    {
        var pagina = new CompraDetalle();
        Set(pagina, "Compra", CompraRegistrada(moneda));
        Set(pagina, "Cargando", false);
        var texto = TextoRenderizado(pagina);
        Assert.Contains("Q 191.76", texto);
        Assert.Contains("Q 95.88", texto);
        if (moneda == MonedaCompra.USD)
        {
            Assert.Contains("US$ 25.00", texto);
            Assert.Contains("US$ 12.50", texto);
            Assert.Contains("Q7.67000 por US$1", texto);
            Assert.Contains("Q7.64136", texto);
            Assert.Contains("02/10/2026", texto);
            Assert.Contains("Banco de Guatemala", texto);
            Assert.Contains("Costo unitario GTQ", texto);
        }
        else
        {
            Assert.DoesNotContain("Tipo de cambio usado", texto);
            Assert.DoesNotContain("US$", texto);
        }
    }

    private static CompraDto CompraRegistrada(MonedaCompra moneda) =>
        new(1, "COM-MONEDA", new(2026, 10, 3), OrigenCompra.Catalogo, 191.76m, null, 1, "Proveedor",
            [new DetalleCompraDto(1, 1, "Producto", 2, 95.88m, 191.76m)
            {
                CostoUnitarioMonedaOrigen = moneda == MonedaCompra.USD ? 12.50m : 95.88m,
                SubtotalMonedaOrigen = moneda == MonedaCompra.USD ? 25m : 191.76m,
            }], null)
        {
            Moneda = moneda,
            TipoCambio = moneda == MonedaCompra.USD ? 7.67m : 1m,
            TotalMonedaOrigen = moneda == MonedaCompra.USD ? 25m : 191.76m,
            TipoCambioReferencia = moneda == MonedaCompra.USD ? 7.64136m : null,
            FechaTipoCambioReferencia = moneda == MonedaCompra.USD ? new(2026, 10, 2) : null,
            FuenteTipoCambio = moneda == MonedaCompra.USD ? "Banco de Guatemala" : null,
        };

    private static string TextoRenderizado(object componente)
    {
        using var tree = new RenderTreeBuilder();
        componente.GetType().GetMethod("BuildRenderTree", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(componente, [tree]);
        return Texto(tree);
    }

    private static string Texto(RenderTreeBuilder tree)
    {
        var texto = new StringBuilder();
#pragma warning disable BL0006 // Inspección Razor como las regresiones UI existentes, incluyendo el ChildContent contextual de EditForm.
        var frames = tree.GetFrames();
        foreach (var frame in frames.Array.Take(frames.Count))
        {
            if (frame.FrameType == RenderTreeFrameType.Text) texto.Append(frame.TextContent);
            else if (frame.FrameType == RenderTreeFrameType.Markup) texto.Append(frame.MarkupContent);
            else if (frame.FrameType == RenderTreeFrameType.Attribute)
            {
                // EditForm declara RenderFragment<EditContext>; el helper previo solo visitaba RenderFragment.
                var fragment = frame.AttributeValue switch
                {
                    RenderFragment simple => simple,
                    RenderFragment<EditContext> contextual => contextual(new EditContext(new object())),
                    _ => null,
                };
                if (fragment is null) continue;
                using var child = new RenderTreeBuilder();
                fragment(child);
                texto.Append(Texto(child));
            }
        }
#pragma warning restore BL0006
        return texto.ToString();
    }

    private static CompraNueva Preparar(ITipoCambioReferenciaService? servicio = null)
    {
        var pagina = new CompraNueva();
        Set(pagina, "Logger", NullLogger<CompraNueva>.Instance);
        if (servicio is not null) Set(pagina, "TipoCambioReferenciaService", servicio);
        Call(pagina, "SeleccionarProveedor", new ProveedorDto(1, "Proveedor", null, null, null));
        var modelo = Get<CompraFormModel>(pagina, "Modelo");
        modelo.Detalles[0].ProductoId = 1;
        modelo.Detalles[0].ProductoSeleccionado = new ProductoDto(1, "PRO-1", null, "Producto", null, null, null, null, null, 0m, 1, "Ropa");
        return pagina;
    }

    private static TipoCambioReferenciaDto Referencia(DateOnly solicitada, decimal valor, DateOnly? efectiva = null) =>
        new(MonedaCompra.USD, solicitada, efectiva ?? solicitada, valor, "Banco de Guatemala");

    private sealed class ReferenciaFalsa : ITipoCambioReferenciaService
    {
        public List<(DateOnly Fecha, CancellationToken Cancelacion, TaskCompletionSource<TipoCambioReferenciaResultado> Resultado)> Pendientes { get; } = [];
        public Task<TipoCambioReferenciaResultado> ConsultarAsync(MonedaCompra moneda, DateOnly fecha, CancellationToken cancellationToken = default)
        {
            var resultado = new TaskCompletionSource<TipoCambioReferenciaResultado>(TaskCreationOptions.RunContinuationsAsynchronously);
            Pendientes.Add((fecha, cancellationToken, resultado));
            return resultado.Task;
        }
        public void Completar(int indice, TipoCambioReferenciaDto referencia) =>
            Pendientes[indice].Resultado.SetResult(TipoCambioReferenciaResultado.Encontrada(referencia));
    }
}
