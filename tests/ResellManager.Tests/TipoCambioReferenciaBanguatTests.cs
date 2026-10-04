using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

public sealed class TipoCambioReferenciaBanguatTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 3);
    private static readonly DateOnly Ayer = new(2026, 10, 2);

    [Fact]
    public async Task Dia_UsaReferenciaDolarExplicitaYNoCompraVenta()
    {
        using var entorno = new Entorno(Respuesta(Dia("03/10/2026", "7.64136",
            "<CambioDia><Var><moneda>2</moneda><fecha>03/10/2026</fecha><venta>9</venta><compra>8</compra></Var></CambioDia>")));
        var result = await entorno.Service.ConsultarAsync(MonedaCompra.USD, Hoy);

        Assert.True(result.Disponible);
        Assert.Equal(new TipoCambioReferenciaDto(MonedaCompra.USD, Hoy, Hoy, 7.64136m,
            "Banco de Guatemala"), result.Referencia);
        Assert.Null(result.Mensaje);
        var request = Assert.Single(entorno.Requests);
        Assert.Equal("https", request.Uri.Scheme);
        Assert.Equal("POST", request.Metodo);
        Assert.Contains("TipoCambioDia", request.Accion);
        Assert.DoesNotContain("fechainit", request.Body);
    }

    [Fact]
    public async Task Historica_UsaValorUnicoCuandoCompraYVentaCoinciden()
    {
        using var entorno = new Entorno(Respuesta(Rango(Fila("02/10/2026", "7.64136", "7.641360"))));
        var result = await entorno.Service.ConsultarAsync(MonedaCompra.USD, Ayer);

        Assert.True(result.Disponible);
        Assert.Equal(7.64136m, result.Referencia!.Valor);
        Assert.Equal(Ayer, result.Referencia.FechaEfectiva);
        var request = Assert.Single(entorno.Requests);
        Assert.Contains("TipoCambioRango", request.Accion);
        Assert.Contains("<fechainit>25/09/2026</fechainit>", request.Body);
        Assert.Contains("<fechafin>02/10/2026</fechafin>", request.Body);
    }

    [Fact]
    public async Task Historica_CompraVentaDistintas_NoInventaNiPromediaReferencia()
    {
        using var entorno = new Entorno(Respuesta(Rango(
            Fila("01/10/2026", "7.64", "7.64"), Fila("02/10/2026", "7.60", "7.70"))));
        ComprobarFallback(await entorno.Service.ConsultarAsync(MonedaCompra.USD, Ayer));
    }

    [Fact]
    public async Task DiaSinPublicacion_ConsultaRangoYDevuelveFechaEfectivaAnterior()
    {
        using var entorno = new Entorno((request, _) => Task.FromResult(Respuesta(
            request.Headers.GetValues("SOAPAction").Single().Contains("TipoCambioDia", StringComparison.Ordinal)
                ? Sobre("TipoCambioDia", "<CambioDolar />")
                : Rango(Fila("02/10/2026", "7.64136", "7.64136")))));
        var result = await entorno.Service.ConsultarAsync(MonedaCompra.USD, Hoy);

        Assert.True(result.Disponible);
        Assert.Equal(Hoy, result.Referencia!.FechaSolicitada);
        Assert.Equal(Ayer, result.Referencia.FechaEfectiva);
        Assert.Equal(2, entorno.Requests.Count);
    }

    [Fact]
    public async Task FinDeSemana_EligeUltimaFechaAnteriorNuncaPosterior()
    {
        using var entorno = new Entorno(Respuesta(Rango(
            Fila("28/09/2026", "7.63147", "7.63147"),
            Fila("26/09/2026", "7.63682", "7.63682"),
            Fila("25/09/2026", "7.63", "7.63"))));
        var solicitada = new DateOnly(2026, 9, 27);
        var result = await entorno.Service.ConsultarAsync(MonedaCompra.USD, solicitada);

        Assert.True(result.Disponible);
        Assert.Equal(new DateOnly(2026, 9, 26), result.Referencia!.FechaEfectiva);
        Assert.Equal(7.63682m, result.Referencia.Valor);
    }

    [Theory]
    [InlineData("09/10/2026")]
    [InlineData("01/09/2026")]
    [InlineData("2026-10-02")]
    [InlineData("32/10/2026")]
    [InlineData("")]
    public async Task FechaInexistentePosteriorFueraDeVentanaOInvalida_ReferenciaNoDisponible(string fecha)
    {
        using var entorno = new Entorno(Respuesta(Rango(Fila(fecha, "7.64136", "7.64136"))));
        ComprobarFallback(await entorno.Service.ConsultarAsync(MonedaCompra.USD, Ayer));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-7.64")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("7,64136")]
    [InlineData("1,000.00")]
    [InlineData("")]
    public async Task ValorDiaInvalido_ReferenciaNoDisponibleSinUsarCompraVenta(string valor)
    {
        using var entorno = new Entorno(Respuesta(Dia("03/10/2026", valor)));
        ComprobarFallback(await entorno.Service.ConsultarAsync(MonedaCompra.USD, Hoy));
        Assert.Single(entorno.Requests);
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("-7.64", "-7.64")]
    [InlineData("7.64", "")]
    [InlineData("", "7.64")]
    public async Task ValorHistoricoInvalido_NoReferencia(string compra, string venta)
    {
        using var entorno = new Entorno(Respuesta(Rango(Fila("02/10/2026", compra, venta))));
        ComprobarFallback(await entorno.Service.ConsultarAsync(MonedaCompra.USD, Ayer));
    }

    [Fact]
    public async Task MonedaHistoricaDistintaDeUsd_NoSeUsa()
    {
        using var entorno = new Entorno(Respuesta(Rango(Fila("02/10/2026", "7.64", "7.64", "3"))));
        ComprobarFallback(await entorno.Service.ConsultarAsync(MonedaCompra.USD, Ayer));
    }

    [Fact]
    public async Task DuplicadosIncompatibles_NoSeEligeArbitrariamente()
    {
        using var entorno = new Entorno(Respuesta(Rango(
            Fila("02/10/2026", "7.64", "7.64"), Fila("02/10/2026", "7.65", "7.65"))));
        ComprobarFallback(await entorno.Service.ConsultarAsync(MonedaCompra.USD, Ayer));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Redirect)]
    public async Task ErrorHttp_DevuelveFallbackSinReintentos(HttpStatusCode status)
    {
        using var entorno = new Entorno(Respuesta("", status));
        ComprobarFallback(await entorno.Service.ConsultarAsync(MonedaCompra.USD, Hoy));
        Assert.Single(entorno.Requests);
    }

    [Fact]
    public async Task ErrorDns_DevuelveFallback()
    {
        using var entorno = new Entorno((_, _) => throw new HttpRequestException("DNS de prueba"));
        ComprobarFallback(await entorno.Service.ConsultarAsync(MonedaCompra.USD, Hoy));
    }

    [Fact]
    public async Task Timeout_DevuelveFallbackControlado()
    {
        using var entorno = new Entorno((_, _) => throw new TaskCanceledException("Timeout de prueba"));
        ComprobarFallback(await entorno.Service.ConsultarAsync(MonedaCompra.USD, Hoy));
        Assert.Equal(TimeSpan.FromSeconds(4), TipoCambioReferenciaBanguatService.TimeoutConsulta);
    }

    [Fact]
    public async Task Cancelacion_PreviaNoConsulta()
    {
        using var entorno = new Entorno(Respuesta(Dia("03/10/2026", "7.64136")));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        ComprobarFallback(await entorno.Service.ConsultarAsync(MonedaCompra.USD, Hoy, cts.Token));
        Assert.Empty(entorno.Requests);
    }

    [Fact]
    public async Task Cancelacion_ActivaLlegaAlHandlerYTerminaConsulta()
    {
        using var cts = new CancellationTokenSource();
        var iniciado = false;
        var cancelado = false;
        using var entorno = new Entorno((_, token) =>
        {
            iniciado = true;
            Assert.True(token.CanBeCanceled);
            Assert.False(cts.IsCancellationRequested);
            var respuestaPendiente = new TaskCompletionSource<HttpResponseMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var registro = token.Register(() =>
            {
                cancelado = true;
                respuestaPendiente.TrySetCanceled(token);
            });
            // La petición ya entró al handler y espera respuesta. Cancelar aquí verifica
            // el enlace del token sin depender de plazos ni del scheduler de la suite.
            cts.Cancel();
            Assert.True(token.IsCancellationRequested);
            return respuestaPendiente.Task;
        });

        ComprobarFallback(await entorno.Service.ConsultarAsync(MonedaCompra.USD, Hoy, cts.Token));
        Assert.True(iniciado);
        Assert.True(cancelado);
        Assert.Single(entorno.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<xml>")]
    [InlineData("<xml />")]
    [InlineData("<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><soap:Fault><faultstring>fallo privado</faultstring></soap:Fault></soap:Body></soap:Envelope>")]
    [InlineData("<!DOCTYPE soap:Envelope [<!ENTITY external SYSTEM 'file:///secreto'>]><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body>&external;</soap:Body></soap:Envelope>")]
    public async Task SoapFaultXmlInvalidoVacioODtd_DevuelveFallbackSinFiltrarContenido(string xml)
    {
        using var entorno = new Entorno(Respuesta(xml));
        ComprobarFallback(await entorno.Service.ConsultarAsync(MonedaCompra.USD, Hoy));
        Assert.Single(entorno.Requests);
    }

    [Fact]
    public async Task OperacionInesperada_NoSeAcepta()
    {
        using var entorno = new Entorno(Respuesta(Sobre("OtraOperacion",
            "<CambioDolar><VarDolar><fecha>03/10/2026</fecha><referencia>7.64</referencia></VarDolar></CambioDolar>")));
        ComprobarFallback(await entorno.Service.ConsultarAsync(MonedaCompra.USD, Hoy));
    }

    [Fact]
    public async Task RespuestaSinReferencia_NoDisponible()
    {
        using var entorno = new Entorno(Respuesta(Sobre("TipoCambioRango", "<Vars /><TotalItems>0</TotalItems>")));
        ComprobarFallback(await entorno.Service.ConsultarAsync(MonedaCompra.USD, Ayer));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RespuestaEnorme_SeRechazaConOSinContentLength(bool contentLength)
    {
        var bytes = Encoding.UTF8.GetBytes(new string('x', 256 * 1024 + 1));
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = contentLength
                ? new ByteArrayContent(bytes)
                : new StreamContent(new StreamNoSeek(bytes))
        };
        using var entorno = new Entorno(response);
        ComprobarFallback(await entorno.Service.ConsultarAsync(MonedaCompra.USD, Hoy));
    }

    [Theory]
    [InlineData("es-GT")]
    [InlineData("es-ES")]
    [InlineData("fr-FR")]
    [InlineData("ar-SA")]
    public async Task ParseoYFormatoSonIndependientesDeCulturaServidor(string cultura)
    {
        var anterior = CultureInfo.CurrentCulture;
        var anteriorUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultura);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultura);
            using var entorno = new Entorno(Respuesta(Rango(Fila("02/10/2026", "7.64136", "7.64136"))));
            var result = await entorno.Service.ConsultarAsync(MonedaCompra.USD, Ayer);
            Assert.True(result.Disponible);
            Assert.Equal(7.64136m, result.Referencia!.Valor);
            Assert.Equal(Ayer, result.Referencia.FechaEfectiva);
            Assert.Contains("<fechafin>02/10/2026</fechafin>", Assert.Single(entorno.Requests).Body);
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
            CultureInfo.CurrentUICulture = anteriorUi;
        }
    }

    [Fact]
    public async Task Gtq_NoConsultaNiDevuelveReferenciaExterna()
    {
        using var entorno = new Entorno(Respuesta(Dia("03/10/2026", "7.64136")));
        var result = await entorno.Service.ConsultarAsync(MonedaCompra.GTQ, Hoy);
        Assert.False(result.Disponible);
        Assert.Null(result.Referencia);
        Assert.Null(result.Mensaje);
        Assert.Empty(entorno.Requests);
    }

    [Fact]
    public async Task FechaFutura_NoConsultaNiInventaReferencia()
    {
        using var entorno = new Entorno(Respuesta(Dia("03/10/2026", "7.64136")));
        ComprobarFallback(await entorno.Service.ConsultarAsync(MonedaCompra.USD, Hoy.AddDays(1)));
        Assert.Empty(entorno.Requests);
    }

    [Fact]
    public async Task Cache_ExitoMismaMonedaFechaSeReutiliza()
    {
        using var entorno = new Entorno(Respuesta(Rango(Fila("02/10/2026", "7.64136", "7.64136"))));
        var first = await entorno.Service.ConsultarAsync(MonedaCompra.USD, Ayer);
        var second = await entorno.Service.ConsultarAsync(MonedaCompra.USD, Ayer);
        Assert.True(first.Disponible);
        Assert.Equal(first, second);
        Assert.Single(entorno.Requests);
    }

    [Fact]
    public async Task Cache_FechaFormaParteDeClave()
    {
        using var entorno = new Entorno((_, _) => Task.FromResult(Respuesta(Rango(
            Fila("01/10/2026", "7.64028", "7.64028"), Fila("02/10/2026", "7.64136", "7.64136")))));
        var primero = await entorno.Service.ConsultarAsync(MonedaCompra.USD, Ayer);
        var segundo = await entorno.Service.ConsultarAsync(MonedaCompra.USD, Ayer.AddDays(-1));
        Assert.Equal(7.64136m, primero.Referencia!.Valor);
        Assert.Equal(7.64028m, segundo.Referencia!.Valor);
        Assert.Equal(2, entorno.Requests.Count);
    }

    [Fact]
    public async Task Cache_ErrorNoSeAlmacenaYSePuedeRecuperar()
    {
        var llamadas = 0;
        using var entorno = new Entorno((_, _) => Task.FromResult(++llamadas == 1
            ? Respuesta("", HttpStatusCode.ServiceUnavailable)
            : Respuesta(Dia("03/10/2026", "7.64136"))));
        ComprobarFallback(await entorno.Service.ConsultarAsync(MonedaCompra.USD, Hoy));
        var recuperado = await entorno.Service.ConsultarAsync(MonedaCompra.USD, Hoy);
        Assert.True(recuperado.Disponible);
        Assert.Equal(2, entorno.Requests.Count);
    }

    [Fact]
    public async Task DiaSeDeterminaEnGuatemalaAunqueUtcYaSeaDiaSiguiente()
    {
        using var entorno = new Entorno(Respuesta(Dia("03/10/2026", "7.64136")),
            new DateTimeOffset(2026, 10, 4, 1, 0, 0, TimeSpan.Zero));
        Assert.True((await entorno.Service.ConsultarAsync(MonedaCompra.USD, Hoy)).Disponible);
        Assert.Contains("TipoCambioDia", Assert.Single(entorno.Requests).Accion);
    }

    private static void ComprobarFallback(TipoCambioReferenciaResultado result)
    {
        Assert.False(result.Disponible);
        Assert.Null(result.Referencia);
        Assert.Equal(TipoCambioReferenciaResultado.MensajeEntradaManual, result.Mensaje);
    }

    private static string Dia(string fecha, string referencia, string adicional = "") =>
        Sobre("TipoCambioDia", $"{adicional}<CambioDolar><VarDolar><fecha>{fecha}</fecha><referencia>{referencia}</referencia></VarDolar></CambioDolar><TotalItems>1</TotalItems>");

    private static string Rango(params string[] filas) =>
        Sobre("TipoCambioRango", $"<Vars>{string.Join("", filas)}</Vars><TotalItems>{filas.Length}</TotalItems>");

    private static string Fila(string fecha, string compra, string venta, string moneda = "2") =>
        $"<Var><moneda>{moneda}</moneda><fecha>{fecha}</fecha><venta>{venta}</venta><compra>{compra}</compra></Var>";

    private static string Sobre(string operacion, string resultado) =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><{operacion}Response xmlns=\"http://www.banguat.gob.gt/variables/ws/\"><{operacion}Result>{resultado}</{operacion}Result></{operacion}Response></soap:Body></soap:Envelope>";

    private static HttpResponseMessage Respuesta(string xml, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(xml, Encoding.UTF8, "text/xml") };

    private sealed record Request(Uri Uri, string Metodo, string Accion, string Body);

    private sealed class Entorno : IDisposable
    {
        private readonly MemoryCache cache = new(new MemoryCacheOptions());
        private readonly HttpClient client;
        public List<Request> Requests { get; } = [];
        public TipoCambioReferenciaBanguatService Service { get; }

        public Entorno(HttpResponseMessage response, DateTimeOffset? ahora = null)
            : this((_, _) => Task.FromResult(response), ahora) { }

        public Entorno(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder,
            DateTimeOffset? ahora = null)
        {
            client = new HttpClient(new Handler(async (request, ct) =>
            {
                Requests.Add(new Request(request.RequestUri!, request.Method.Method,
                    request.Headers.GetValues("SOAPAction").Single(),
                    await request.Content!.ReadAsStringAsync(ct)));
                return await responder(request, ct);
            })) { Timeout = TipoCambioReferenciaBanguatService.TimeoutConsulta };
            Service = new TipoCambioReferenciaBanguatService(client, cache,
                NullLogger<TipoCambioReferenciaBanguatService>.Instance,
                new Reloj(ahora ?? new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero)));
        }

        public void Dispose()
        {
            client.Dispose();
            cache.Dispose();
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            responder(request, ct);
    }

    private sealed class Reloj(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private sealed class StreamNoSeek(byte[] bytes) : Stream
    {
        private readonly MemoryStream contenido = new(bytes);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => contenido.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => contenido.ReadAsync(buffer, ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) contenido.Dispose();
            base.Dispose(disposing);
        }
    }
}
