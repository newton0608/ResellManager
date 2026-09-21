using System.Net;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ResellManager.Web.Hosting;

namespace ResellManager.Tests;

[Collection("Integración web")]
public sealed class ProductionHostingTests
{
    [Theory]
    [InlineData("ReverseProxy:KnownProxy", "not-an-ip")]
    [InlineData("ReverseProxy:KnownProxy", "0.0.0.0")]
    [InlineData("ReverseProxy:KnownNetwork", "172.29.213.0/33")]
    [InlineData("ReverseProxy:KnownNetwork", "0.0.0.0/0")]
    [InlineData("ReverseProxy:KnownNetwork", "::/0")]
    [InlineData("ReverseProxy:KnownNetwork", "invalid")]
    public void ProductionRechazaConfiguracionInvalida(string key, string value)
    {
        var builder = CrearBuilder("Production", new() { [key] = value });
        Assert.Throws<InvalidOperationException>(() => ProductionHosting.ConfigureForwardedHeaders(
            new(), builder.Configuration, builder.Environment));
    }

    [Fact]
    public void ProductionExigeProxyExplicito_DevelopmentNoProcesaCabecerasSinProxy()
    {
        var production = CrearBuilder("Production", new());
        Assert.Throws<InvalidOperationException>(() => ProductionHosting.ConfigureForwardedHeaders(
            new(), production.Configuration, production.Environment));
        var development = CrearBuilder("Development", new());
        var options = new ForwardedHeadersOptions();
        ProductionHosting.ConfigureForwardedHeaders(options, development.Configuration, development.Environment);
        Assert.Equal(ForwardedHeaders.None, options.ForwardedHeaders);
    }

    [Theory]
    [InlineData("ReverseProxy:KnownProxy", "172.29.213.2", "172.29.213.2", true)]
    [InlineData("ReverseProxy:KnownProxy", "172.29.213.2", "172.29.213.3", false)]
    [InlineData("ReverseProxy:KnownProxy", "172.29.213.2", "127.0.0.1", false)]
    [InlineData("ReverseProxy:KnownNetwork", "172.29.213.0/28", "172.29.213.2", true)]
    [InlineData("ReverseProxy:KnownNetwork", "172.29.213.0/28", "172.29.214.2", false)]
    [InlineData("ReverseProxy:KnownProxy", "::ffff:172.29.213.2", "::ffff:172.29.213.2", true)]
    public async Task SoloProxyExplicitoPuedeCambiarEsquemaYCliente(
        string key, string value, string remote, bool trusted)
    {
        var builder = CrearBuilder("Production", new() { [key] = value });
        builder.WebHost.UseTestServer();
        builder.Services.AddProductionHosting(builder.Configuration);
        await using var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
            return next(context);
        });
        app.UseForwardedHeaders();
        app.Run(context => context.Response.WriteAsync(
            $"{context.Request.Scheme}|{context.Connection.RemoteIpAddress}|{context.Request.Host}"));
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-Proto", "http, https");
        request.Headers.Add("X-Forwarded-For", "198.51.100.9, 203.0.113.8");
        request.Headers.Add("X-Forwarded-Host", "attacker.invalid");
        var response = await client.SendAsync(request);
        Assert.Equal(trusted ? "https|203.0.113.8|localhost" : $"http|{remote}|localhost",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void VariablesEntorno_PersistenClavesEntreProveedores()
    {
        var path = Path.Combine(Path.GetTempPath(), $"resellmanager-keys-{Guid.NewGuid():N}");
        var prefix = $"RM_TEST_{Guid.NewGuid():N}_";
        var values = new Dictionary<string, string>
        {
            ["DataProtection__KeysPath"] = path,
            ["ReverseProxy__KnownNetwork"] = "172.29.213.0/28",
            ["ConnectionStrings__ResellManager"] = "Data Source=/app/data/database/resellmanager.db",
            ["AlmacenamientoComprobantes__DirectorioBase"] = "/app/data/comprobantes",
            ["AllowedHosts"] = "app.resellmanager.tech"
        };
        try
        {
            foreach (var (key, value) in values)
                Environment.SetEnvironmentVariable(prefix + key, value);
            var config = new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build();
            foreach (var (key, value) in values)
                Assert.Equal(value, config[key.Replace("__", ":")]);

            string encrypted;
            using (var provider = CrearServicios(config))
            {
                Assert.Equal("ResellManager",
                    provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
                var repository = Assert.IsType<FileSystemXmlRepository>(
                    provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository);
                Assert.Equal(path, repository.Directory.FullName);
                encrypted = provider.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("deployment-test").Protect("persistencia");
            }
            Assert.NotEmpty(Directory.GetFiles(path, "key-*.xml"));
            using var restarted = CrearServicios(config);
            Assert.Equal("persistencia", restarted.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("deployment-test").Unprotect(encrypted));
        }
        finally
        {
            foreach (var key in values.Keys)
                Environment.SetEnvironmentVariable(prefix + key, null);
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
    }

    [Fact]
    public void DevelopmentNoExigeDirectorioDataProtection()
    {
        using var provider = CrearServicios(new ConfigurationBuilder().Build());
        Assert.Null(provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository);
    }

    [Theory]
    [InlineData("Production", CookieSecurePolicy.Always)]
    [InlineData("Development", CookieSecurePolicy.SameAsRequest)]
    public async Task HostReal_ArrancaConHealthYCookiesEsperadas(string environment, CookieSecurePolicy policy)
    {
        var path = Path.Combine(Path.GetTempPath(), $"resellmanager-host-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        var previousKeysPath = Environment.GetEnvironmentVariable("DataProtection__KeysPath");
        Environment.SetEnvironmentVariable("DataProtection__KeysPath", Path.Combine(path, "keys"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IStartupFilter, ProxyTestStartupFilter>();
                    services.AddHttpsRedirection(options => options.HttpsPort = 443);
                });
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ResellManager"] = $"Data Source={Path.Combine(path, "test.db")};Pooling=False",
                    ["AlmacenamientoComprobantes:DirectorioBase"] = Path.Combine(path, "receipts"),
                    ["UsuarioInicial:Correo"] = "",
                    ["UsuarioInicial:Contrasena"] = "",
                    ["ReverseProxy:KnownProxy"] = "172.29.213.2",
                    ["AllowedHosts"] = "app.resellmanager.tech"
                }));
            });
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://app.resellmanager.tech"),
                AllowAutoRedirect = false
            });
            var response = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("OK", await response.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.GetAsync("https://attacker.invalid/health")).StatusCode);
            var cookie = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(IdentityConstants.ApplicationScheme);
            Assert.Equal(Path.Combine(path, "keys"), Assert.IsType<FileSystemXmlRepository>(
                factory.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository).Directory.FullName);
            Assert.Equal(policy, cookie.Cookie.SecurePolicy);
            Assert.True(cookie.Cookie.HttpOnly);
            Assert.Equal(SameSiteMode.Lax, cookie.Cookie.SameSite);
            Assert.Equal(TimeSpan.FromHours(8), cookie.ExpireTimeSpan);
            Assert.True(cookie.SlidingExpiration);
            using var forwarded = new HttpRequestMessage(HttpMethod.Get, "http://app.resellmanager.tech/health");
            forwarded.Headers.Add("X-Forwarded-For", "203.0.113.8");
            forwarded.Headers.Add("X-Forwarded-Proto", "https");
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(forwarded)).StatusCode);
            using var spoofed = new HttpRequestMessage(HttpMethod.Get, "http://app.resellmanager.tech/health");
            spoofed.Headers.Add("X-Test-Unknown-Proxy", "true");
            spoofed.Headers.Add("X-Forwarded-For", "203.0.113.8");
            spoofed.Headers.Add("X-Forwarded-Proto", "https");
            var rejected = await client.SendAsync(spoofed);
            Assert.Equal(HttpStatusCode.TemporaryRedirect, rejected.StatusCode);
            Assert.Equal("https", rejected.Headers.Location!.Scheme);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DataProtection__KeysPath", previousKeysPath);
            Directory.Delete(path, true);
        }
    }

    private sealed class ProxyTestStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, following) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(
                    context.Request.Headers.ContainsKey("X-Test-Unknown-Proxy") ? "172.29.214.2" : "172.29.213.2");
                return following(context);
            });
            next(app);
        };
    }

    private static WebApplicationBuilder CrearBuilder(string environment, Dictionary<string, string?> values)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(values);
        return builder;
    }

    private static ServiceProvider CrearServicios(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProductionHosting(configuration);
        return services.BuildServiceProvider();
    }
}
