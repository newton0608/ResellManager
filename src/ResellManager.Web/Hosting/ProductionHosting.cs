using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

namespace ResellManager.Web.Hosting;

public static class ProductionHosting
{
    public static IServiceCollection AddProductionHosting(
        this IServiceCollection services, IConfiguration configuration)
    {
        var protection = services.AddDataProtection().SetApplicationName("ResellManager");
        var path = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(path))
        {
            var directory = new DirectoryInfo(Path.GetFullPath(path));
            directory.Create();
            protection.PersistKeysToFileSystem(directory);
        }

        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IConfiguration, IWebHostEnvironment>(ConfigureForwardedHeaders);
        return services;
    }

    public static void ConfigureForwardedHeaders(
        ForwardedHeadersOptions options, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var proxyText = configuration["ReverseProxy:KnownProxy"];
        var networkText = configuration["ReverseProxy:KnownNetwork"];
        var proxies = options.KnownProxies.ToArray();
        var networks = options.KnownNetworks.ToArray();
        var configured = false;

        if (!string.IsNullOrWhiteSpace(proxyText))
        {
            if (!IPAddress.TryParse(proxyText, out var proxy)
                || proxy.Equals(IPAddress.Any) || proxy.Equals(IPAddress.IPv6Any))
                throw new InvalidOperationException("ReverseProxy:KnownProxy debe ser una IP válida y específica.");
            options.KnownProxies.Add(proxy);
            configured = true;
        }

        if (!string.IsNullOrWhiteSpace(networkText))
        {
            if (!System.Net.IPNetwork.TryParse(networkText, out var network)
                || network.PrefixLength == 0)
                throw new InvalidOperationException("ReverseProxy:KnownNetwork debe ser una red CIDR válida y limitada (no /0).");
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(
                network.BaseAddress, network.PrefixLength));
            configured = true;
        }

        if (!configured)
        {
            if (environment.IsProduction())
                throw new InvalidOperationException("Production requiere ReverseProxy:KnownProxy o ReverseProxy:KnownNetwork.");
            options.ForwardedHeaders = ForwardedHeaders.None;
            return;
        }

        // Sustituir las entradas implícitas de loopback solo después de agregar confianza explícita.
        foreach (var proxy in proxies)
            options.KnownProxies.Remove(proxy);
        foreach (var network in networks)
            options.KnownNetworks.Remove(network);
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
    }
}
