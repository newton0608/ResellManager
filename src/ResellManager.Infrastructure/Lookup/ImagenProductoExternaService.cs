using System.Net;
using Microsoft.Extensions.Logging;
using ResellManager.Application.Common;
using ResellManager.Application.Interfaces;
using ResellManager.Infrastructure.Storage;

namespace ResellManager.Infrastructure.Lookup;

public sealed class ImagenProductoExternaService(HttpClient http, ILogger<ImagenProductoExternaService> logger)
    : IImagenProductoExternaService
{
    private const int MaximoRedirecciones = 3;

    public async Task<ServiceResult<Stream>> DescargarAsync(string url, CancellationToken ct = default)
    {
        if (!DestinoImagenProductoSeguro.UrlPermitida(url, out var destino)) return Fallo("DestinoInseguro");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var actual = destino!;
            var visitados = new HashSet<string>(StringComparer.Ordinal);
            for (var salto = 0; ; salto++)
            {
                // También valida cada Location resuelta; el handler comprueba DNS/IP antes de conectar.
                if (!DestinoImagenProductoSeguro.UrlPermitida(actual.AbsoluteUri, out _))
                    return Fallo("DestinoInseguro");
                // El fragmento no se envía por HTTP y no debe permitir repetir la misma petición.
                if (!visitados.Add(actual.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped)))
                    return Fallo("CicloRedireccion");
                using var request = new HttpRequestMessage(HttpMethod.Get, actual);
                request.Headers.Accept.ParseAdd("image/jpeg,image/png,image/webp");
                using var respuesta = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (respuesta.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found
                    or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                {
                    if (salto >= MaximoRedirecciones) return Fallo("LimiteRedirecciones", (int)respuesta.StatusCode);
                    if (respuesta.Headers.Location is not { } location || !Uri.TryCreate(actual, location, out var siguiente))
                        return Fallo("RedireccionSinDestino", (int)respuesta.StatusCode);
                    actual = siguiente;
                    continue;
                }
                if (!respuesta.IsSuccessStatusCode) return Fallo("EstadoHttp", (int)respuesta.StatusCode);
                if (respuesta.Content.Headers.ContentType?.MediaType is not ("image/jpeg" or "image/png" or "image/webp"))
                    return Fallo("TipoNoPermitido", (int)respuesta.StatusCode);
                await using var stream = await respuesta.Content.ReadAsStreamAsync(timeout.Token);
                var memoria = await ProductoLookupHttpProvider.LeerAcotadoAsync(
                    stream, respuesta.Content.Headers.ContentLength, (int)AlmacenamientoImagenesProductoLocal.TamanoMaximoBytes, timeout.Token);
                return ServiceResult<Stream>.Ok(memoria);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException or IOException or InvalidDataException)
        { return Fallo(ex.GetType().Name); }
    }

    private ServiceResult<Stream> Fallo(string motivo, int? estadoHttp = null)
    {
        logger.LogWarning("No se pudo obtener la imagen externa del producto: {Motivo}, HTTP {EstadoHttp}.", motivo, estadoHttp);
        return ServiceResult<Stream>.Failure("No pudimos obtener la imagen externa. El producto se guardará sin ella.");
    }
}
