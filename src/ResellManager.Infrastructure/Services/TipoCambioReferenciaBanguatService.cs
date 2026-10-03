using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Enums;

namespace ResellManager.Infrastructure.Services;

/// <summary>Adaptador SOAP liviano para la referencia USD/GTQ del Banco de Guatemala.</summary>
public sealed class TipoCambioReferenciaBanguatService(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<TipoCambioReferenciaBanguatService> logger,
    TimeProvider timeProvider) : ITipoCambioReferenciaService
{
    public static readonly Uri Endpoint = new("https://www.banguat.gob.gt/variables/ws/TipoCambio.asmx");
    public static readonly TimeSpan TimeoutConsulta = TimeSpan.FromSeconds(4);
    private const string Fuente = "Banco de Guatemala";
    // Los identificadores SOAP son URIs del contrato, no conexiones HTTP.
    private const string NamespaceProveedor = "http://www.banguat.gob.gt/variables/ws/";
    private const string NamespaceSoap = "http://schemas.xmlsoap.org/soap/envelope/";
    private const int MaxBytesRespuesta = 256 * 1024;
    private const int DiasBusquedaAnterior = 7;
    private static readonly TimeZoneInfo ZonaGuatemala =
        TimeZoneInfo.FindSystemTimeZoneById("America/Guatemala");

    public async Task<TipoCambioReferenciaResultado> ConsultarAsync(
        MonedaCompra moneda, DateOnly fecha, CancellationToken cancellationToken = default)
    {
        // La moneda base no necesita proveedor ni una referencia artificial.
        if (moneda != MonedaCompra.USD)
        {
            return new(false, null, null);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return TipoCambioReferenciaResultado.NoDisponible();
        }

        var hoy = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), ZonaGuatemala).DateTime);
        if (fecha > hoy)
        {
            return TipoCambioReferenciaResultado.NoDisponible();
        }

        var clave = new ClaveCache(moneda, fecha);
        if (cache.TryGetValue(clave, out TipoCambioReferenciaDto? guardada) && guardada is not null)
        {
            return TipoCambioReferenciaResultado.Encontrada(guardada);
        }

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Cubre cabeceras, lectura del cuerpo y eventual rango, incluso con ResponseHeadersRead.
        limite.CancelAfter(TimeoutConsulta);
        try
        {
            var inicio = fecha.AddDays(-Math.Min(DiasBusquedaAnterior, fecha.DayNumber));
            TipoCambioReferenciaDto? referencia;
            if (fecha == hoy)
            {
                var dia = await ConsultarProveedorAsync("TipoCambioDia", null, limite.Token);
                referencia = LeerReferenciaDia(dia, fecha, inicio);
                if (referencia is null && !dia.Descendants(XName.Get("VarDolar", NamespaceProveedor)).Any())
                {
                    var rango = await ConsultarProveedorAsync("TipoCambioRango", (inicio, fecha), limite.Token);
                    referencia = LeerReferenciaHistorica(rango, fecha, inicio);
                }
            }
            else
            {
                var rango = await ConsultarProveedorAsync("TipoCambioRango", (inicio, fecha), limite.Token);
                referencia = LeerReferenciaHistorica(rango, fecha, inicio);
            }

            if (referencia is null)
            {
                logger.LogInformation("Banguat no proporcionó una referencia USD inequívoca para {Fecha}.", fecha);
                return TipoCambioReferenciaResultado.NoDisponible();
            }

            cache.Set(clave, referencia, fecha == hoy ? TimeSpan.FromMinutes(15) : TimeSpan.FromDays(30));
            return TipoCambioReferenciaResultado.Encontrada(referencia);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Consulta de referencia Banguat cancelada para {Fecha}.", fecha);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Consulta Banguat superó el tiempo disponible para {Fecha}.", fecha);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or XmlException or InvalidDataException)
        {
            // Se registra la clase del fallo sin volcar XML, contenido externo ni detalles al usuario.
            logger.LogWarning("Consulta Banguat falló para {Fecha}: {TipoError}.", fecha, ex.GetType().Name);
        }

        return TipoCambioReferenciaResultado.NoDisponible();
    }

    private async Task<XDocument> ConsultarProveedorAsync(
        string operacion, (DateOnly Inicio, DateOnly Fin)? rango, CancellationToken cancellationToken)
    {
        XNamespace proveedor = NamespaceProveedor;
        XNamespace soap = NamespaceSoap;
        var contenidoOperacion = new XElement(proveedor + operacion);
        if (rango is { } fechas)
        {
            contenidoOperacion.Add(
                new XElement(proveedor + "fechainit", fechas.Inicio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
                new XElement(proveedor + "fechafin", fechas.Fin.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
        }

        var sobre = new XDocument(new XElement(soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", soap),
            new XElement(soap + "Body", contenidoOperacion)));
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("SOAPAction", $"\"{NamespaceProveedor}{operacion}\"");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
        request.Content = new StringContent(sobre.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml");
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxBytesRespuesta)
        {
            throw new InvalidDataException("Respuesta Banguat demasiado grande.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var limitada = new MemoryStream();
        var buffer = new byte[8192];
        int leidos;
        while ((leidos = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (limitada.Length + leidos > MaxBytesRespuesta)
            {
                throw new InvalidDataException("Respuesta Banguat demasiado grande.");
            }
            await limitada.WriteAsync(buffer.AsMemory(0, leidos), cancellationToken);
        }
        limitada.Position = 0;
        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxBytesRespuesta,
            MaxCharactersFromEntities = 0
        };
        using var reader = XmlReader.Create(limitada, settings);
        var documento = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
        if (documento.Root?.Name != soap + "Envelope" || documento.Descendants(soap + "Fault").Any())
        {
            throw new InvalidDataException("Respuesta SOAP no válida.");
        }
        if (documento.Root.Element(soap + "Body")?.Element(proveedor + (operacion + "Response"))?
                .Element(proveedor + (operacion + "Result")) is null)
        {
            throw new InvalidDataException("Respuesta SOAP no corresponde a la operación.");
        }
        return documento;
    }

    private static TipoCambioReferenciaDto? LeerReferenciaDia(XDocument documento, DateOnly solicitada, DateOnly inicio)
    {
        XNamespace proveedor = NamespaceProveedor;
        var filas = documento.Descendants(proveedor + "CambioDolar").Elements(proveedor + "VarDolar")
            .Select(x => new FilaReferencia(LeerFecha(x.Element(proveedor + "fecha")?.Value),
                LeerValor(x.Element(proveedor + "referencia")?.Value)))
            .ToList();
        return ElegirReferencia(filas, solicitada, inicio);
    }

    private static TipoCambioReferenciaDto? LeerReferenciaHistorica(XDocument documento, DateOnly solicitada, DateOnly inicio)
    {
        XNamespace proveedor = NamespaceProveedor;
        var filas = documento.Descendants(proveedor + "Vars").Elements(proveedor + "Var")
            .Where(x => x.Element(proveedor + "moneda")?.Value.Trim() == "2")
            .Select(x =>
            {
                var compra = LeerValor(x.Element(proveedor + "compra")?.Value);
                var venta = LeerValor(x.Element(proveedor + "venta")?.Value);
                // El contrato histórico sólo ofrece compra/venta. Para USD aceptamos el valor
                // único cuando coinciden exactamente; si difieren no escogemos ni promediamos.
                // JM-126-2006 unificó la referencia USD; véase docs/28_MonedasDeCompra.md.
                return new FilaReferencia(LeerFecha(x.Element(proveedor + "fecha")?.Value),
                    compra.HasValue && compra == venta ? compra : null);
            }).ToList();
        return ElegirReferencia(filas, solicitada, inicio);
    }

    private static TipoCambioReferenciaDto? ElegirReferencia(
        IReadOnlyList<FilaReferencia> filas, DateOnly solicitada, DateOnly inicio)
    {
        var aplicables = filas.Where(x => x.Fecha >= inicio && x.Fecha <= solicitada).ToList();
        if (aplicables.Count == 0)
        {
            return null;
        }
        var efectiva = aplicables.Max(x => x.Fecha)!.Value;
        var ultimas = aplicables.Where(x => x.Fecha == efectiva).ToList();
        if (ultimas.Any(x => !x.Valor.HasValue) || ultimas.Select(x => x.Valor).Distinct().Count() != 1)
        {
            return null;
        }
        return new(MonedaCompra.USD, solicitada, efectiva, ultimas[0].Valor!.Value, Fuente);
    }

    private static DateOnly? LeerFecha(string? value) =>
        DateOnly.TryParseExact(value?.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var fecha) ? fecha : null;

    private static decimal? LeerValor(string? value) =>
        decimal.TryParse(value?.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out var valor) && valor > 0 ? valor : null;

    private sealed record FilaReferencia(DateOnly? Fecha, decimal? Valor);
    private readonly record struct ClaveCache(MonedaCompra Moneda, DateOnly Fecha);
}

