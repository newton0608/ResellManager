using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;

namespace ResellManager.Application.Services;

public sealed class ProductoLookupService(
    IConsultaProductoCodigoBarras productos, IEnumerable<IProductoLookupProvider> proveedores) : IProductoLookupService
{
    private readonly IProductoLookupProvider[] fuentes = proveedores.ToArray();

    public async Task<ProductoLookupRonda> IniciarAsync(string codigo, CancellationToken ct = default)
    {
        var ronda = new ProductoLookupRonda(codigo);
        if (!ProductoLookupDatos.CodigoValido(codigo))
        {
            ronda.ErrorEntrada = "Escribe un código de hasta 100 caracteres imprimibles antes de buscar.";
            return ronda;
        }
        await ContinuarAsync(ronda, ct);
        return ronda;
    }

    public async Task ContinuarAsync(ProductoLookupRonda ronda, CancellationToken ct = default)
    {
        await ronda.Exclusividad.WaitAsync(ct);
        try
        {
            if (ronda.ErrorEntrada is not null || ronda.ProductoLocal is not null || ronda.Agotada) return;
            ronda.Candidato = null;
            // También se comprueba al continuar una ronda que estuvo abierta en revisión.
            ronda.ProductoLocal = await productos.ObtenerPorCodigoBarrasAsync(ronda.CodigoConsultado, ct);
            if (ronda.ProductoLocal is not null) return;
            while (ronda.SiguienteProveedor < fuentes.Length)
            {
                ct.ThrowIfCancellationRequested();
                var fuente = fuentes[ronda.SiguienteProveedor++];
                ProductoLookupRespuesta respuesta;
                try { respuesta = await fuente.ConsultarAsync(ronda.CodigoConsultado, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
                { respuesta = new(EstadoLookupProveedor.Timeout); }
                catch (Exception) { respuesta = new(EstadoLookupProveedor.NoDisponible); }
                ct.ThrowIfCancellationRequested();
                var candidato = respuesta.Candidato is not null
                    ? ProductoLookupDatos.Normalizar(respuesta.Candidato with { Fuente = fuente.Fuente }) : null;
                if (respuesta.Estado == EstadoLookupProveedor.Encontrado
                    && (candidato is null || !ProductoLookupDatos.EsUtil(candidato)
                        || (candidato.CodigoBarras is not null && !ProductoLookupDatos.CodigosEquivalentes(ronda.CodigoConsultado, candidato.CodigoBarras))))
                    respuesta = new(EstadoLookupProveedor.RespuestaInvalida);
                ronda.intentos.Add(new(fuente.Fuente, respuesta.Estado));
                if (respuesta.Estado != EstadoLookupProveedor.Encontrado) continue;
                ronda.Candidato = candidato;
                return;
            }
            ronda.Agotada = true;
        }
        finally { ronda.Exclusividad.Release(); }
    }
}
