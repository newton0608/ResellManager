using System.Net;
using System.Net.Sockets;

namespace ResellManager.Infrastructure.Lookup;

public static class DestinoImagenProductoSeguro
{
    public static bool UrlPermitida(string? valor, out Uri? url)
    {
        url = null;
        if (valor is null || valor.Length > 2048 || !Uri.TryCreate(valor, UriKind.Absolute, out var destino)
            || destino.Scheme != Uri.UriSchemeHttps || destino.Port != 443 || !string.IsNullOrEmpty(destino.UserInfo)
            || destino.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6))
            return false;
        var host = destino.IdnHost.TrimEnd('.').Trim('[', ']');
        if (IPAddress.TryParse(host, out var ip))
        {
            if (!DireccionPublica(ip)) return false;
        }
        else if (!host.Contains('.') || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || new[] { ".localhost", ".local", ".internal", ".lan", ".home", ".test", ".invalid" }
                .Any(sufijo => host.EndsWith(sufijo, StringComparison.OrdinalIgnoreCase))) return false;
        url = destino;
        return true;
    }

    public static bool DireccionPublica(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip)) return false;
        var b = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
            return b[0] is not (0 or 10 or 127) && b[0] < 224
                && !(b[0] == 100 && b[1] is >= 64 and <= 127)
                && !(b[0] == 169 && b[1] == 254)
                && !(b[0] == 172 && b[1] is >= 16 and <= 31)
                && !(b[0] == 192 && (b[1] == 168 || (b[1] == 0 && b[2] is 0 or 2)))
                && !(b[0] == 198 && (b[1] is 18 or 19 || (b[1] == 51 && b[2] == 100)))
                && !(b[0] == 203 && b[1] == 0 && b[2] == 113);
        return ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.ScopeId == 0 && (b[0] & 0xe0) == 0x20
            && !(b[0] == 0x20 && b[1] == 0x02)
            && !(b[0] == 0x20 && b[1] == 0x01
                && ((b[2] == 0x0d && b[3] == 0xb8) || (b[2] == 0 && b[3] <= 0x3f)));
    }

    public static SocketsHttpHandler CrearHandler() => new()
    {
        AllowAutoRedirect = false, UseProxy = false, UseCookies = false,
        ConnectCallback = (contexto, ct) => ConectarAsync(contexto.DnsEndPoint, ct)
    };

    public static async ValueTask<Stream> ConectarAsync(DnsEndPoint destino, CancellationToken ct)
    {
        if (destino.Port != 443) throw new HttpRequestException("Puerto de imagen no permitido.");
        var direcciones = await Dns.GetHostAddressesAsync(destino.Host, ct);
        if (direcciones.Length == 0 || direcciones.Any(ip => !DireccionPublica(ip)))
            throw new HttpRequestException("Destino de imagen no permitido.");
        // La conexión se fija a la IP validada; no hay una segunda resolución susceptible a DNS rebinding.
        foreach (var ip in direcciones)
        {
            var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(ip, 443), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException) { socket.Dispose(); }
            catch { socket.Dispose(); throw; }
        }
        throw new HttpRequestException("Destino de imagen no disponible.");
    }
}
