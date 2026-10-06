using Microsoft.Extensions.Logging;
using ResellManager.Application.Common;
using ResellManager.Application.Interfaces;
using ResellManager.Infrastructure.Storage;

namespace ResellManager.Infrastructure.Lookup;

public sealed class ImagenProductoExternaService(HttpClient http, ILogger<ImagenProductoExternaService> logger)
    : IImagenProductoExternaService
{
    public async Task<ServiceResult<Stream>> DescargarAsync(string url, CancellationToken ct = default)
    {
        if (!DestinoImagenProductoSeguro.UrlPermitida(url, out var destino)) return Fallo("DestinoInseguro");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, destino);
            request.Headers.Accept.ParseAdd("image/jpeg,image/png,image/webp");
            using var respuesta = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!respuesta.IsSuccessStatusCode) return Fallo("EstadoHttp");
            if (respuesta.Content.Headers.ContentType?.MediaType is not ("image/jpeg" or "image/png" or "image/webp"))
                return Fallo("TipoNoPermitido");
            await using var stream = await respuesta.Content.ReadAsStreamAsync(timeout.Token);
            var memoria = await ProductoLookupHttpProvider.LeerAcotadoAsync(
                stream, respuesta.Content.Headers.ContentLength, (int)AlmacenamientoImagenesProductoLocal.TamanoMaximoBytes, timeout.Token);
            return ServiceResult<Stream>.Ok(memoria);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException or IOException or InvalidDataException)
        { return Fallo(ex.GetType().Name); }
    }

    private ServiceResult<Stream> Fallo(string motivo)
    {
        logger.LogWarning("No se pudo obtener la imagen externa del producto: {Motivo}.", motivo);
        return ServiceResult<Stream>.Failure("No pudimos obtener la imagen externa. El producto se guardará sin ella.");
    }
}
