using System.Net;
using Microsoft.Extensions.Options;

namespace ResellManager.Web.Catalogo;

public sealed class CatalogoContactoOptions
{
    public const string Seccion = "CatalogoContacto";
    public string? WhatsAppNumero { get; set; }
    public string? OrigenPublico { get; set; }
}

// Configuración de servidor: nunca se deriva el origen compartido del request o Preview.
public sealed class CatalogoWhatsApp(IOptions<CatalogoContactoOptions> options)
{
    public string? CrearEnlace(int productoId, string nombre)
    {
        var configuracion = options.Value;
        var numero = configuracion.WhatsAppNumero;
        if (productoId <= 0 || string.IsNullOrWhiteSpace(nombre)
            || numero is null || numero.Length is < 8 or > 15 || numero[0] == '0'
            || numero.Any(c => c is < '0' or > '9')
            || !Uri.TryCreate(configuracion.OrigenPublico, UriKind.Absolute, out var origen)
            || origen.Scheme != Uri.UriSchemeHttps || !origen.IsDefaultPort
            || origen.UserInfo.Length != 0 || origen.Query.Length != 0 || origen.Fragment.Length != 0
            || origen.AbsolutePath != "/" || origen.HostNameType != UriHostNameType.Dns
            || origen.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || !origen.Host.Contains('.') || IPAddress.TryParse(origen.Host, out _))
            return null;
        var url = new Uri(origen, $"producto/{productoId}").AbsoluteUri;
        return $"https://wa.me/{numero}?text={Uri.EscapeDataString($"Hola, quisiera consultar por {nombre}. {url}")}";
    }
}