using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Infrastructure.Lookup;

namespace ResellManager.Tests;

public sealed class ProductoLookupProvidersTests
{
    private const string Codigo = ProductoLookupTests.Codigo;
    private const string OpenJson = """
        {"status":"success","product":{"code":"0123456789012","product_name_es":"Perfume",
        "brands":"Marca","quantity":"100 ml","generic_name":"Descripción","categories":"Perfumes",
        "image_front_url":"https://images.example.com/perfume.jpg"}}
        """;
    private const string UpcJson = """
        {"code":"OK","total":1,"items":[{"ean":"0123456789012","title":"Perfume alternativo","brand":"Otra marca",
        "model":"Modelo","color":"Azul","size":"M","weight":"2 lb","description":"Descripción alternativa",
        "category":"Beauty > Fragrances","images":["https://images.example.com/otro.png"],
        "lowest_recorded_price":99999,"currency":"USD","offers":[{"price":100}]}]}
        """;

    [Fact]
    public async Task OpenFacts_UsaApiUniversalUserAgentYCodigoSinModificar()
    {
        using var handler = new Handler(_ => Json(OpenJson));
        var resultado = await Open(handler).ConsultarAsync(Codigo);
        Assert.Equal(EstadoLookupProveedor.Encontrado, resultado.Estado);
        Assert.Equal("Perfume", resultado.Candidato!.Nombre);
        Assert.Equal(100m, resultado.Candidato.ContenidoMl);
        Assert.Null(resultado.Candidato.PesoGramos);
        var request = Assert.Single(handler.Peticiones);
        Assert.Equal("world.openfoodfacts.org", request.Url.Host);
        Assert.Equal("/api/v3/product/" + Codigo, request.Url.AbsolutePath);
        Assert.Contains("product_type=all", request.Url.Query);
        Assert.Contains("ResellManager/", request.UserAgent);
        Assert.Null(request.Clave);
    }

    [Fact]
    public async Task OpenFacts_RedireccionTransversalSoloAInstanciasOficiales()
    {
        using var handler = new Handler(request => request.RequestUri!.Host == "world.openfoodfacts.org"
            ? Redireccion("https://world.openbeautyfacts.org/api/v3/product/" + Codigo + "?product_type=all")
            : Json(OpenJson));
        var resultado = await Open(handler).ConsultarAsync(Codigo);
        Assert.Equal(EstadoLookupProveedor.Encontrado, resultado.Estado);
        Assert.Equal(2, handler.Peticiones.Count);
        Assert.Equal("world.openbeautyfacts.org", handler.Peticiones[1].Url.Host);
    }

    [Theory]
    [InlineData("https://localhost/api/v3/product/0123456789012")]
    [InlineData("http://world.openbeautyfacts.org/api/v3/product/0123456789012")]
    [InlineData("https://world.openbeautyfacts.org.evil.example/api/v3/product/0123456789012")]
    [InlineData("https://world.openbeautyfacts.org/other")]
    public async Task OpenFacts_NoSigueRedireccionAjenaOInsegura(string destino)
    {
        using var handler = new Handler(_ => Redireccion(destino));
        Assert.Equal(EstadoLookupProveedor.RespuestaInvalida, (await Open(handler).ConsultarAsync(Codigo)).Estado);
        Assert.Single(handler.Peticiones);
    }

    [Fact]
    public async Task Upcitemdb_UsaTrial_ExponeSoloDatosNormalizados_SinPrecios()
    {
        using var handler = new Handler(_ => Json(UpcJson));
        var resultado = await Upc(handler).ConsultarAsync(Codigo);
        Assert.Equal(EstadoLookupProveedor.Encontrado, resultado.Estado);
        Assert.Equal("Modelo", resultado.Candidato!.Modelo);
        Assert.Equal("Azul", resultado.Candidato.Color);
        Assert.Equal(907.18m, resultado.Candidato.PesoGramos);
        Assert.Null(resultado.Candidato.ContenidoMl);
        Assert.Equal("/prod/trial/lookup", Assert.Single(handler.Peticiones).Url.AbsolutePath);
        Assert.Null(handler.Peticiones[0].Clave);
        Assert.Equal("?upc=" + Codigo, handler.Peticiones[0].Url.Query);
        Assert.Null(typeof(ProductoLookupCandidato).GetProperty("PrecioSugerido"));
    }

    [Fact]
    public async Task Upcitemdb_ClaveConfiguradaSoloEnHeadersDelEndpointDePago()
    {
        using var handler = new Handler(_ => Json(UpcJson));
        var resultado = await Upc(handler, new() { UpcitemdbUserKey = "clave-ficticia-de-prueba" }).ConsultarAsync(Codigo);
        Assert.Equal(EstadoLookupProveedor.Encontrado, resultado.Estado);
        Assert.Equal("/prod/v1/lookup", handler.Peticiones[0].Url.AbsolutePath);
        Assert.Equal("clave-ficticia-de-prueba", handler.Peticiones[0].Clave);
        Assert.Equal("3scale", handler.Peticiones[0].TipoClave);
        Assert.DoesNotContain("clave", handler.Peticiones[0].Url.ToString());
    }

    [Theory]
    [InlineData(404, EstadoLookupProveedor.NoEncontrado)]
    [InlineData(429, EstadoLookupProveedor.LimitePeticiones)]
    [InlineData(500, EstadoLookupProveedor.NoDisponible)]
    [InlineData(401, EstadoLookupProveedor.RespuestaInvalida)]
    public async Task AmbosProveedores_DistinguenErroresHttp(int status, EstadoLookupProveedor esperado)
    {
        using var primero = new Handler(_ => new((HttpStatusCode)status));
        using var segundo = new Handler(_ => new((HttpStatusCode)status));
        Assert.Equal(esperado, (await Open(primero).ConsultarAsync(Codigo)).Estado);
        Assert.Equal(esperado, (await Upc(segundo).ConsultarAsync(Codigo)).Estado);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("""{"status":"success","product":{}}""")]
    [InlineData("""{"status":"success","product":{"code":"9999999999999","product_name":"Otro"}}""")]
    public async Task OpenFacts_JsonInvalidoOSinCandidatoUtil_NoLoPresenta(string json)
    {
        using var handler = new Handler(_ => Json(json));
        Assert.Equal(EstadoLookupProveedor.RespuestaInvalida, (await Open(handler).ConsultarAsync(Codigo)).Estado);
    }

    [Fact]
    public async Task Upcitemdb_ListaVaciaNoEncontrado_ProductoAjenoInvalido()
    {
        using var vacio = new Handler(_ => Json("""{"code":"OK","items":[]}"""));
        using var ajeno = new Handler(_ => Json("""{"code":"OK","items":[{"ean":"9999999999999","title":"Otro"}]}"""));
        Assert.Equal(EstadoLookupProveedor.NoEncontrado, (await Upc(vacio).ConsultarAsync(Codigo)).Estado);
        Assert.Equal(EstadoLookupProveedor.RespuestaInvalida, (await Upc(ajeno).ConsultarAsync(Codigo)).Estado);
    }

    [Fact]
    public async Task TimeoutDeConsulta_Recuperable_CancelacionDeUsuariaSePropaga()
    {
        using var handler = new HandlerAsync(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Json(OpenJson);
        });
        var provider = Open(handler, new() { TimeoutSegundos = 1 });
        Assert.Equal(EstadoLookupProveedor.Timeout, (await provider.ConsultarAsync(Codigo)).Estado);
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.ConsultarAsync(Codigo, cancelacion.Token));
    }

    [Fact]
    public async Task RespuestaExcesivaConOSinContentLength_SeRechaza()
    {
        using var grande = new Handler(_ => new(HttpStatusCode.OK)
        { Content = new StringContent(new string('x', 1024 * 1024 + 1), Encoding.UTF8, "application/json") });
        Assert.Equal(EstadoLookupProveedor.RespuestaInvalida, (await Open(grande).ConsultarAsync(Codigo)).Estado);
        await using var desconocida = new MemoryStream(new byte[200]);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ProductoLookupHttpProvider.LeerAcotadoAsync(desconocida, null, 100, CancellationToken.None));
    }

    [Fact]
    public async Task RetryAfterYReset_ImpidenNuevasPeticionesSinDormir()
    {
        var reloj = new Reloj();
        using var handler = new Handler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(2));
            return response;
        });
        var provider = Upc(handler, reloj: reloj);
        Assert.Equal(EstadoLookupProveedor.LimitePeticiones, (await provider.ConsultarAsync(Codigo)).Estado);
        Assert.Equal(EstadoLookupProveedor.LimitePeticiones, (await provider.ConsultarAsync(Codigo)).Estado);
        Assert.Single(handler.Peticiones);
        reloj.Avanzar(TimeSpan.FromMinutes(3));
        await provider.ConsultarAsync(Codigo);
        Assert.Equal(2, handler.Peticiones.Count);
    }

    [Fact]
    public async Task LimitesLocales_SeCompartenEntreSesiones_RespetanMinutoYDia()
    {
        var options = Options.Create(new ProductoLookupOptions { UpcitemdbPeticionesPorDia = 2, OpenFactsPeticionesPorMinuto = 500 });
        var reloj = new Reloj();
        var limites = new ProductoLookupLimites(options, reloj);
        using var handler = new Handler(_ => Json(UpcJson));
        var primero = new UpcitemdbProductoLookupProvider(new HttpClient(handler), options, limites,
            NullLogger<UpcitemdbProductoLookupProvider>.Instance);
        var segundo = new UpcitemdbProductoLookupProvider(new HttpClient(handler), options, limites,
            NullLogger<UpcitemdbProductoLookupProvider>.Instance);
        await primero.ConsultarAsync(Codigo);
        await segundo.ConsultarAsync(Codigo);
        Assert.Equal(EstadoLookupProveedor.LimitePeticiones, (await primero.ConsultarAsync(Codigo)).Estado);
        Assert.Equal(2, handler.Peticiones.Count);
        reloj.Avanzar(TimeSpan.FromMinutes(2));
        Assert.False(limites.IntentarConsumir("UPCitemdb"));
        for (var i = 0; i < 15; i++) Assert.True(limites.IntentarConsumir("Open Facts"));
        Assert.False(limites.IntentarConsumir("Open Facts"));
        reloj.Avanzar(TimeSpan.FromDays(1));
        Assert.True(limites.IntentarConsumir("UPCitemdb"));
    }

    [Fact]
    public async Task Codigo128DeTexto_NoHaceHttp_AltaManualSigueDisponible()
    {
        using var handler = new Handler(_ => throw new InvalidOperationException("No debe acceder a HTTP"));
        Assert.Equal(EstadoLookupProveedor.NoEncontrado, (await Open(handler).ConsultarAsync(" CODE 128 ")).Estado);
        Assert.Equal(EstadoLookupProveedor.NoEncontrado, (await Upc(handler).ConsultarAsync(" CODE 128 ")).Estado);
        Assert.Empty(handler.Peticiones);
    }

    [Fact]
    public async Task OpenFacts_NombreLocalizadoVacio_NoOcultaNombreDisponible()
    {
        using var handler = new Handler(_ => Json(
            """{"status":"success","product":{"code":"0123456789012","product_name_es":"","product_name":"Nombre disponible"}}"""));
        var resultado = await Open(handler).ConsultarAsync(Codigo);
        Assert.Equal(EstadoLookupProveedor.Encontrado, resultado.Estado);
        Assert.Equal("Nombre disponible", resultado.Candidato!.Nombre);
    }

    internal static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private static HttpResponseMessage Redireccion(string destino)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri(destino);
        return response;
    }
    private static OpenFactsProductoLookupProvider Open(HttpMessageHandler handler, ProductoLookupOptions? opciones = null)
    {
        var options = Options.Create(opciones ?? new());
        return new(new HttpClient(handler), options, new(options, new Reloj()), NullLogger<OpenFactsProductoLookupProvider>.Instance);
    }
    private static UpcitemdbProductoLookupProvider Upc(HttpMessageHandler handler, ProductoLookupOptions? opciones = null, Reloj? reloj = null)
    {
        var options = Options.Create(opciones ?? new());
        return new(new HttpClient(handler), options, new(options, reloj ?? new Reloj()), NullLogger<UpcitemdbProductoLookupProvider>.Instance);
    }
    internal sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respuesta) : HttpMessageHandler
    {
        public List<(Uri Url, string UserAgent, string? Clave, string? TipoClave)> Peticiones { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Peticiones.Add((request.RequestUri!, request.Headers.UserAgent.ToString(),
                request.Headers.TryGetValues("user_key", out var clave) ? clave.Single() : null,
                request.Headers.TryGetValues("key_type", out var tipo) ? tipo.Single() : null));
            return Task.FromResult(respuesta(request));
        }
    }
    internal sealed class HandlerAsync(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respuesta) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => respuesta(request, ct);
    }
    internal sealed class Reloj : TimeProvider
    {
        private DateTimeOffset ahora = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => ahora;
        public void Avanzar(TimeSpan tiempo) => ahora += tiempo;
    }
}
