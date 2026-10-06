using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;

namespace ResellManager.Infrastructure.Lookup;

public abstract class ProductoLookupHttpProvider(
    HttpClient http, IOptions<ProductoLookupOptions> options,
    ProductoLookupLimites limites, ILogger logger) : IProductoLookupProvider
{
    public abstract string Fuente { get; }
    protected ProductoLookupOptions Opciones => options.Value;
    protected abstract bool Habilitado { get; }
    protected abstract Uri Endpoint(string codigo);
    protected abstract ProductoLookupRespuesta Leer(JsonElement json, string codigo);
    protected virtual void Configurar(HttpRequestMessage request) { }
    protected virtual bool RedireccionPermitida(Uri destino) => false;

    public async Task<ProductoLookupRespuesta> ConsultarAsync(string codigo, CancellationToken ct = default)
    {
        if (!Habilitado) return new(EstadoLookupProveedor.NoDisponible);
        // Las APIs públicas consultan GTIN; un CODE-128 de texto sigue disponible para el alta manual.
        if (codigo.Length is not (8 or 12 or 13 or 14) || !codigo.All(char.IsAsciiDigit))
            return new(EstadoLookupProveedor.NoEncontrado);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(Opciones.TimeoutSegundos, 1, 30)));
        try
        {
            var destino = Endpoint(codigo);
            for (var salto = 0; salto < 4; salto++)
            {
                if (!limites.IntentarConsumir(Fuente)) return Registrar(new(EstadoLookupProveedor.LimitePeticiones));
                using var request = new HttpRequestMessage(HttpMethod.Get, destino);
                request.Headers.Accept.ParseAdd("application/json");
                request.Headers.UserAgent.ParseAdd(Opciones.UserAgent);
                Configurar(request);
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                limites.RegistrarRespuesta(Fuente, response);
                if (response.StatusCode == HttpStatusCode.Found && response.Headers.Location is { } location)
                {
                    var siguiente = location.IsAbsoluteUri ? location : new Uri(destino, location);
                    if (!RedireccionPermitida(siguiente)) return Registrar(new(EstadoLookupProveedor.RespuestaInvalida));
                    destino = siguiente;
                    continue;
                }
                if (response.StatusCode == HttpStatusCode.NotFound) return Registrar(new(EstadoLookupProveedor.NoEncontrado));
                if ((int)response.StatusCode == 429) return Registrar(new(EstadoLookupProveedor.LimitePeticiones));
                if (!response.IsSuccessStatusCode) return Registrar(new((int)response.StatusCode >= 500
                    ? EstadoLookupProveedor.NoDisponible : EstadoLookupProveedor.RespuestaInvalida));
                if (response.Content.Headers.ContentType?.MediaType != "application/json")
                    return Registrar(new(EstadoLookupProveedor.RespuestaInvalida));
                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                using var memoria = await LeerAcotadoAsync(stream, response.Content.Headers.ContentLength, 1024 * 1024, timeout.Token);
                using var json = await JsonDocument.ParseAsync(memoria, new() { MaxDepth = 32 }, timeout.Token);
                var resultado = Leer(json.RootElement, codigo);
                if (resultado.Estado == EstadoLookupProveedor.Encontrado)
                {
                    var candidato = resultado.Candidato is not null
                        ? ResellManager.Application.Common.ProductoLookupDatos.Normalizar(resultado.Candidato) : null;
                    resultado = candidato is not null && ResellManager.Application.Common.ProductoLookupDatos.EsUtil(candidato)
                        && ResellManager.Application.Common.ProductoLookupDatos.CodigosEquivalentes(codigo, candidato.CodigoBarras)
                            ? new(EstadoLookupProveedor.Encontrado, candidato)
                            : new(EstadoLookupProveedor.RespuestaInvalida);
                }
                return Registrar(resultado);
            }
            return Registrar(new(EstadoLookupProveedor.RespuestaInvalida));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return Registrar(new(EstadoLookupProveedor.Timeout)); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or FormatException)
        { return Registrar(new(EstadoLookupProveedor.RespuestaInvalida)); }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        { return Registrar(new(EstadoLookupProveedor.NoDisponible)); }
    }

    private ProductoLookupRespuesta Registrar(ProductoLookupRespuesta resultado)
    {
        logger.LogInformation("Consulta de producto: proveedor {Proveedor}, estado {Estado}.", Fuente, resultado.Estado);
        return resultado;
    }

    public static async Task<MemoryStream> LeerAcotadoAsync(Stream stream, long? longitud, int maximo, CancellationToken ct)
    {
        if (longitud > maximo) throw new InvalidDataException("Respuesta demasiado grande.");
        var memoria = new MemoryStream();
        try
        {
            var buffer = new byte[16 * 1024];
            int leidos;
            while ((leidos = await stream.ReadAsync(buffer, ct)) > 0)
            {
                if (memoria.Length + leidos > maximo) throw new InvalidDataException("Respuesta demasiado grande.");
                await memoria.WriteAsync(buffer.AsMemory(0, leidos), ct);
            }
            memoria.Position = 0;
            return memoria;
        }
        catch { memoria.Dispose(); throw; }
    }

    protected static string? Texto(JsonElement objeto, string campo) =>
        objeto.TryGetProperty(campo, out var valor) && valor.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(valor.GetString()) ? valor.GetString() : null;

    protected static ProductoLookupRespuesta Encontrado(ProductoLookupCandidato candidato) =>
        new(EstadoLookupProveedor.Encontrado, candidato);
}
