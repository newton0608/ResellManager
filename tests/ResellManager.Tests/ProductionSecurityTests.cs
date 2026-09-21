using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ResellManager.Tests;

[Collection("Integración web")]
public sealed class ProductionSecurityTests : PruebaWebAislada
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Login_LimitaPorIp_SoloConfiaEnForwardedHeadersDelProxyConocido(bool trusted)
    {
        using var app = CrearAppConProxy();
        using var client = CrearCliente(app);
        var token = await ObtenerTokenAsync(client);
        var peer = trusted ? "172.29.213.2" : "198.51.100.10";

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var response = await EnviarLoginAsync(client, token, peer, "203.0.113.8");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/login?error=credenciales", response.Headers.Location?.OriginalString);
        }

        using var limited = await EnviarLoginAsync(client, token, peer, "203.0.113.8");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        ComprobarHeaders(limited);
        Assert.DoesNotContain("ausente@example.test", await limited.Content.ReadAsStringAsync());

        // Misma conexión del proxy y distinto XFF: contador independiente solo si es confiable.
        using var otherForwarded = await EnviarLoginAsync(client, token, peer, "203.0.113.9");
        Assert.Equal(trusted ? HttpStatusCode.Redirect : HttpStatusCode.TooManyRequests,
            otherForwarded.StatusCode);

        // Un origen directo distinto tiene su propio contador incluso con XFF falsificado.
        using var otherPeer = await EnviarLoginAsync(client, token, "198.51.100.11", "203.0.113.8");
        Assert.Equal(HttpStatusCode.Redirect, otherPeer.StatusCode);
        if (trusted)
        {
            using var mapped = await EnviarLoginAsync(client, token, peer, "::ffff:203.0.113.8");
            Assert.Equal(HttpStatusCode.TooManyRequests, mapped.StatusCode);
        }

        // La IP que agotó login puede seguir consultando health, páginas y estáticos.
        for (var attempt = 0; attempt < 12; attempt++)
        {
            foreach (var path in new[] { "/health", "/login", "/app.js" })
            {
                using var request = CrearSolicitud(HttpMethod.Get, path, peer, "203.0.113.8");
                using var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }
        using var negotiate = CrearSolicitud(HttpMethod.Post, "/_blazor/negotiate?negotiateVersion=1",
            peer, "203.0.113.8");
        using var signalR = await client.SendAsync(negotiate);
        Assert.Equal(HttpStatusCode.OK, signalR.StatusCode);
    }

    [Fact]
    public void RateLimiting_SinPoliticaGlobal_SoloPostAccountLoginTienePolitica()
    {
        using var client = CrearCliente(factory);
        var options = factory.Services.GetRequiredService<IOptions<RateLimiterOptions>>().Value;
        Assert.Null(options.GlobalLimiter);
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>() is not null)
            .ToList();
        var login = Assert.Single(endpoints);
        Assert.Equal("/account/login", login.RoutePattern.RawText);
        Assert.Equal("Login", login.Metadata.GetMetadata<EnableRateLimitingAttribute>()!.PolicyName);
        Assert.Equal("POST", Assert.Single(login.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods));
    }

    [Theory]
    [InlineData("/health", HttpStatusCode.OK)]
    [InlineData("/login", HttpStatusCode.OK)]
    [InlineData("/app.js", HttpStatusCode.OK)]
    [InlineData("/no-existe-security-test", HttpStatusCode.Redirect)]
    [InlineData("/account/login", HttpStatusCode.MethodNotAllowed)]
    public async Task Headers_EnEndpointsPaginasEstaticosYErrores(string path, HttpStatusCode status)
    {
        using var client = CrearCliente(factory);
        using var response = await client.GetAsync(path);
        Assert.Equal(status, response.StatusCode);
        ComprobarHeaders(response);
    }

    private static void ComprobarHeaders(HttpResponseMessage response)
    {
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    private WebApplicationFactory<Program> CrearAppConProxy() => factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ReverseProxy:KnownProxy"] = "172.29.213.2" }));
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, SimularConexion>());
    });

    private static HttpClient CrearCliente(WebApplicationFactory<Program> app) => app.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    private static async Task<string> ObtenerTokenAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/login");
        var input = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>");
        Assert.True(input.Success);
        var value = Regex.Match(input.Value, "value=\"([^\"]+)\"");
        Assert.True(value.Success);
        return WebUtility.HtmlDecode(value.Groups[1].Value);
    }

    private static async Task<HttpResponseMessage> EnviarLoginAsync(
        HttpClient client, string token, string peer, string forwarded)
    {
        using var request = CrearSolicitud(HttpMethod.Post, "/account/login", peer, forwarded);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["correo"] = "ausente@example.test",
            ["contrasena"] = "entrada-invalida",
            ["__RequestVerificationToken"] = token
        });
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage CrearSolicitud(HttpMethod method, string path, string peer, string forwarded)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Test-Peer", peer);
        request.Headers.Add("X-Forwarded-For", forwarded);
        request.Headers.Add("X-Forwarded-Proto", "https");
        return request;
    }

    // Solo en TestServer: simula la IP del socket antes del pipeline real de Program.
    private sealed class SimularConexion : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, following) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(
                    context.Request.Headers["X-Test-Peer"].FirstOrDefault() ?? "172.29.213.2");
                return following(context);
            });
            next(app);
        };
    }
}
