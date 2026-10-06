using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Infrastructure.Lookup;
using ResellManager.Infrastructure.Storage;
using static ResellManager.Tests.ProductoLookupProvidersTests;

namespace ResellManager.Tests;

public sealed class ImagenExternaRedireccionesTests
{
    private const string Origen = "https://world.openfoodfacts.org/images/products/prueba.jpg";
    private const string Final = "https://images.openfoodfacts.org/images/products/prueba.jpg";
    private static readonly byte[] Imagen = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(303)]
    [InlineData(307)]
    [InlineData(308)]
    public async Task RedireccionHttpsPublica_DescargaDestinoYDevuelveStreamIndependienteDesdeElInicio(int estado)
    {
        using var handler = new Handler(request => request.RequestUri!.AbsoluteUri == Origen
            ? Redireccion(estado, Final) : ImagenValida());
        using var http = new HttpClient(handler);
        var resultado = await Servicio(http).DescargarAsync(Origen);
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        await using var contenido = resultado.Value!;
        Assert.Equal(0, contenido.Position);
        using var copia = new MemoryStream();
        await contenido.CopyToAsync(copia);
        Assert.Equal(Imagen, copia.ToArray());
        Assert.Equal(new[] { Origen, Final }, handler.Peticiones.Select(x => x.Url.AbsoluteUri));
    }

    [Theory]
    [InlineData("/cdn/prueba.png", "https://world.openfoodfacts.org/cdn/prueba.png")]
    [InlineData("../prueba.png", "https://world.openfoodfacts.org/images/prueba.png")]
    [InlineData("//images.openfoodfacts.org/prueba.png", "https://images.openfoodfacts.org/prueba.png")]
    public async Task LocationRelativa_ResuelveContraLaPeticionActualYValidaAntesDeSeguir(string location, string final)
    {
        using var handler = new Handler(request => request.RequestUri!.AbsoluteUri == Origen
            ? Redireccion(302, location) : ImagenValida());
        using var http = new HttpClient(handler);
        var resultado = await Servicio(http).DescargarAsync(Origen);
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        await resultado.Value!.DisposeAsync();
        Assert.Equal(final, handler.Peticiones[1].Url.AbsoluteUri);
    }

    [Theory]
    [InlineData("http://images.openfoodfacts.org/p.png")]
    [InlineData("https://images.openfoodfacts.org:444/p.png")]
    [InlineData("https://usuario:clave@images.openfoodfacts.org/p.png")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://localhost/p.png")]
    [InlineData("https://127.0.0.1/p.png")]
    [InlineData("https://10.0.0.1/p.png")]
    [InlineData("https://169.254.169.254/p.png")]
    [InlineData("https://[::1]/p.png")]
    [InlineData("https://[::ffff:192.168.1.1]/p.png")]
    [InlineData("https://printer.internal/p.png")]
    public async Task RedireccionInsegura_NoHacePeticionAlDestino(string destino)
    {
        using var handler = new Handler(_ => Redireccion(302, destino));
        using var http = new HttpClient(handler);
        Assert.False((await Servicio(http).DescargarAsync(Origen)).IsSuccess);
        Assert.Single(handler.Peticiones);
    }

    [Fact]
    public async Task CadaSaltoSeRevalida_UnoPublicoNoAutorizaElSiguientePrivado()
    {
        using var handler = new Handler(request => Redireccion(302,
            request.RequestUri!.AbsoluteUri == Origen ? Final : "https://192.168.1.1/secreto"));
        using var http = new HttpClient(handler);
        Assert.False((await Servicio(http).DescargarAsync(Origen)).IsSuccess);
        Assert.Equal(new[] { Origen, Final }, handler.Peticiones.Select(x => x.Url.AbsoluteUri));
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public async Task LimiteDeTresRedirecciones_NoEnviaUnaQuintaPeticion(int saltos, bool exito)
    {
        var peticiones = 0;
        using var handler = new Handler(_ => peticiones++ < saltos
            ? Redireccion(302, $"https://images.openfoodfacts.org/p{peticiones}.png") : ImagenValida());
        using var http = new HttpClient(handler);
        var resultado = await Servicio(http).DescargarAsync(Origen);
        Assert.Equal(exito, resultado.IsSuccess);
        if (resultado.Value is not null) await resultado.Value.DisposeAsync();
        Assert.Equal(4, handler.Peticiones.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CicloInclusoConFragmento_NoRepiteUnaPeticion(bool fragmento)
    {
        using var handler = new Handler(request => Redireccion(302,
            request.RequestUri!.AbsoluteUri == Origen ? Final : Origen + (fragmento ? "#otra" : "")));
        using var http = new HttpClient(handler);
        Assert.False((await Servicio(http).DescargarAsync(Origen)).IsSuccess);
        Assert.Equal(2, handler.Peticiones.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://[")]
    public async Task LocationAusenteOMalformada_FallaControladamente(string? location)
    {
        using var handler = new Handler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            if (location is not null) response.Headers.TryAddWithoutValidation("Location", location);
            return response;
        });
        using var http = new HttpClient(handler);
        Assert.False((await Servicio(http).DescargarAsync(Origen)).IsSuccess);
        Assert.Single(handler.Peticiones);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(304)]
    [InlineData(305)]
    [InlineData(403)]
    public async Task EstadoQueNoEsRedireccionAdmitida_NoSigueLocation(int estado)
    {
        using var handler = new Handler(_ => Redireccion(estado, Final));
        using var http = new HttpClient(handler);
        Assert.False((await Servicio(http).DescargarAsync(Origen)).IsSuccess);
        Assert.Single(handler.Peticiones);
    }

    [Theory]
    [InlineData("html")]
    [InlineData("longitud")]
    [InlineData("stream")]
    public async Task DestinoFinal_ConservaValidacionDeMimeYTamaño(string condicion)
    {
        using var handler = new Handler(request =>
        {
            if (request.RequestUri!.AbsoluteUri == Origen) return Redireccion(302, Final);
            if (condicion == "html") return new(HttpStatusCode.OK) { Content = new StringContent("<html>error</html>") };
            var response = ImagenValida();
            if (condicion == "longitud")
                response.Content.Headers.ContentLength = AlmacenamientoImagenesProductoLocal.TamanoMaximoBytes + 1;
            else
                response.Content = new StreamContent(new SinLongitud(new MemoryStream(new byte[AlmacenamientoImagenesProductoLocal.TamanoMaximoBytes + 1])));
            response.Content.Headers.ContentType = new("image/png");
            return response;
        });
        using var http = new HttpClient(handler);
        Assert.False((await Servicio(http).DescargarAsync(Origen)).IsSuccess);
        Assert.Equal(2, handler.Peticiones.Count);
    }

    [Fact]
    public async Task Cadena_CancelacionSolicitanteEnDestinoFinal_SePropaga()
    {
        var tokens = new List<CancellationToken>();
        using var cancelacion = new CancellationTokenSource();
        using var handler = new HandlerAsync((request, ct) =>
        {
            tokens.Add(ct);
            if (request.RequestUri!.AbsoluteUri == Origen) return Task.FromResult(Redireccion(302, Final));
            cancelacion.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(ct);
        });
        using var http = new HttpClient(handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Servicio(http).DescargarAsync(Origen, cancelacion.Token));
        Assert.Equal(2, tokens.Count);
        Assert.True(cancelacion.IsCancellationRequested);
    }

    [Fact]
    public async Task TimeoutTotal_IncluyeRedirecciones_NoConcedeDiezSegundosPorSalto()
    {
        var llamadas = 0;
        using var handler = new HandlerAsync(async (_, ct) =>
        {
            llamadas++;
            await Task.Delay(TimeSpan.FromSeconds(6), ct);
            return llamadas == 1 ? Redireccion(302, Final) : ImagenValida();
        });
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        Assert.False((await Servicio(http).DescargarAsync(Origen)).IsSuccess);
        Assert.Equal(2, llamadas);
    }

    private sealed class SinLongitud(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => inner.ReadAsync(buffer, ct);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }

    internal static ImagenProductoExternaService Servicio(HttpClient http) =>
        new(http, NullLogger<ImagenProductoExternaService>.Instance);
    internal static HttpResponseMessage Redireccion(int estado, string location) => new((HttpStatusCode)estado)
    { Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) } };
    private static HttpResponseMessage ImagenValida() => new(HttpStatusCode.OK)
    { Content = new ByteArrayContent(Imagen) { Headers = { ContentType = new MediaTypeHeaderValue("image/png") } } };
}
