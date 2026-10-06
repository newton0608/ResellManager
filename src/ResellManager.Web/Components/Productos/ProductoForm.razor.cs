using Microsoft.AspNetCore.Components;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;

namespace ResellManager.Web.Components.Productos;

public partial class ProductoForm : IDisposable
{
    [Inject] private IProductoLookupService LookupService { get; set; } = null!;
    [Inject] private ILogger<ProductoForm> LookupLogger { get; set; } = null!;
    [Parameter] public bool LookupHabilitado { get; set; }
    private ProductoLookupRonda? Ronda;
    private bool BuscandoLookup;
    private bool RevisandoLookup;
    private string? MensajeLookup;
    private ProductoLookupImportacion Importacion = new();
    private CancellationTokenSource? CancelacionLookup;
    private ProductoFormModel? ModeloLookup;
    private string? VistaPreviaAnterior;
    private string? ErrorImagenAnterior;
    private bool DescartadoLookup;
    private bool ProductoLocalCoincide => Ronda?.ProductoLocal is not null && Ronda.CodigoConsultado == Modelo.CodigoBarras;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(ModeloLookup, Modelo)) return;
        CancelacionLookup?.Cancel();
        ModeloLookup = Modelo;
        Ronda = null;
        RevisandoLookup = false;
        MensajeLookup = null;
        Importacion = new();
        VistaPrevia = null;
        ErrorImagen = null;
    }

    private Task BuscarProductoAsync() => EjecutarBusquedaAsync(false);
    private Task BuscarOtraFuenteAsync() => EjecutarBusquedaAsync(true);

    private async Task EjecutarBusquedaAsync(bool siguiente)
    {
        if (!LookupHabilitado || Guardando || BuscandoLookup || DescartadoLookup) return;
        BuscandoLookup = true;
        RevisandoLookup = false;
        MensajeLookup = siguiente ? "Buscando en la siguiente fuente..." : "Buscando producto...";
        var modelo = Modelo;
        var codigo = modelo.CodigoBarras ?? string.Empty;
        using var cancelacion = new CancellationTokenSource();
        CancelacionLookup = cancelacion;
        try
        {
            ProductoLookupRonda ronda;
            if (siguiente && Ronda is not null && Ronda.CodigoConsultado == codigo)
            {
                ronda = Ronda;
                await LookupService.ContinuarAsync(ronda, cancelacion.Token);
            }
            else ronda = await LookupService.IniciarAsync(codigo, cancelacion.Token);
            if (DescartadoLookup || cancelacion.IsCancellationRequested || !ReferenceEquals(Modelo, modelo)
                || Modelo.CodigoBarras != codigo) return;
            Ronda = ronda;
            RevisandoLookup = ronda.Candidato is not null;
            MensajeLookup = Ronda.ErrorEntrada
                ?? (Ronda.ProductoLocal is not null ? $"Este código pertenece a {Ronda.ProductoLocal.Nombre}."
                : Ronda.Agotada ? "No hubo coincidencias útiles. Conservamos el código; puedes continuar manualmente o reintentar la búsqueda."
                : "Producto encontrado. Revisa los datos antes de usarlos.");
        }
        catch (OperationCanceledException) when (cancelacion.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (DescartadoLookup || cancelacion.IsCancellationRequested || !ReferenceEquals(Modelo, modelo)) return;
            LookupLogger.LogWarning("No se pudo completar la búsqueda de producto: {TipoError}.", ex.GetType().Name);
            Ronda = null;
            MensajeLookup = "No pudimos completar la búsqueda. Puedes continuar manualmente o reintentar.";
        }
        finally
        {
            if (ReferenceEquals(CancelacionLookup, cancelacion)) CancelacionLookup = null;
            BuscandoLookup = false;
        }
    }

    private async Task CodigoEscaneado(string codigo)
    {
        Modelo.CodigoBarras = codigo;
        CodigoManualCambiado();
        if (LookupHabilitado) await BuscarProductoAsync();
    }

    private void CodigoManualCambiado()
    {
        Ronda = null;
        RevisandoLookup = false;
        MensajeLookup = null;
    }

    private void DescartarCandidato()
    {
        Ronda = null;
        RevisandoLookup = false;
        MensajeLookup = "Puedes continuar manualmente. No se aplicaron los datos encontrados.";
    }

    private void UsarCandidato()
    {
        if (Guardando || BuscandoLookup || !RevisandoLookup || Ronda?.Candidato is null
            || Ronda.CodigoConsultado != Modelo.CodigoBarras) return;
        VistaPreviaAnterior = VistaPrevia;
        ErrorImagenAnterior = ErrorImagen;
        Importacion.Aplicar(Modelo, Ronda.Candidato);
        RevisandoLookup = false;
        Ronda = null;
        MensajeLookup = "Datos importados. Puedes corregirlos o deshacerlos antes de guardar.";
    }

    private void DeshacerImportacion()
    {
        if (Guardando || BuscandoLookup || !Importacion.PuedeDeshacer) return;
        Importacion.Deshacer(Modelo);
        VistaPrevia = VistaPreviaAnterior;
        ErrorImagen = ErrorImagenAnterior;
        VersionSelectorImagen++;
        Ronda = null;
        RevisandoLookup = false;
        MensajeLookup = "Datos importados deshechos. Restauramos el formulario anterior.";
    }

    private static string EstadoIntento(EstadoLookupProveedor estado) => estado switch
    {
        EstadoLookupProveedor.Encontrado => "encontrado",
        EstadoLookupProveedor.NoEncontrado => "sin coincidencia",
        EstadoLookupProveedor.Timeout => "tiempo agotado",
        EstadoLookupProveedor.LimitePeticiones => "límite de peticiones; puedes continuar manualmente",
        EstadoLookupProveedor.NoDisponible => "no disponible; puedes continuar manualmente",
        _ => "respuesta inválida; puedes continuar manualmente"
    };

    public void Dispose()
    {
        DescartadoLookup = true;
        CancelacionLookup?.Cancel();
    }
}
