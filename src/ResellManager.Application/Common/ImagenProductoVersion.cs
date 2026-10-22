using System.Security.Cryptography;
using System.Text;

namespace ResellManager.Application.Common;

public static class ImagenProductoVersion
{
    // La ruta privada nunca se expone; cada archivo confirmado recibe un GUID nuevo.
    public static string Obtener(string referencia) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(referencia)))[..24];
}
