using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ResellManager.Application.DTOs;

namespace ResellManager.Application.Common;

public static class ProductoLookupDatos
{
    // CODE-128 admite texto y espacios. No se modifican dígitos ni espacios del código aprobado.
    public static bool CodigoValido(string? codigo) => !string.IsNullOrWhiteSpace(codigo)
        && codigo.Length <= 100 && codigo.All(c => c is >= ' ' and <= '~');

    public static bool CodigosEquivalentes(string consultado, string? devuelto) =>
        consultado == devuelto || (!string.IsNullOrEmpty(devuelto)
            && consultado.All(char.IsAsciiDigit) && devuelto.All(char.IsAsciiDigit)
            && consultado.TrimStart('0') == devuelto.TrimStart('0'));

    public static bool EsUtil(ProductoLookupCandidato candidato) =>
        candidato.Nombre is not null || candidato.Descripcion is not null || candidato.Marca is not null
        || candidato.Modelo is not null || candidato.Color is not null || candidato.Talla is not null
        || candidato.Presentacion is not null || candidato.PesoGramos.HasValue || candidato.ContenidoMl.HasValue
        || candidato.ImagenUrl is not null;

    public static string? Texto(string? texto, int maximo)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Length > 64 * 1024) return null;
        try
        {
            texto = WebUtility.HtmlDecode(texto);
            texto = Regex.Replace(texto, @"<(script|style)\b[^>]*>.*?</\1>", " ",
                RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromMilliseconds(50));
            texto = Regex.Replace(texto, "<[^>]*>", " ", RegexOptions.None, TimeSpan.FromMilliseconds(50));

            texto = Regex.Replace(texto, @"\s+", " ", RegexOptions.None, TimeSpan.FromMilliseconds(50)).Trim();
            if (texto.Any(char.IsControl) || string.IsNullOrWhiteSpace(texto)) return null;
            if (texto.Length > maximo)
            {
                texto = texto[..maximo];
                if (char.IsHighSurrogate(texto[^1])) texto = texto[..^1];
            }
            return texto.TrimEnd();
        }
        catch (RegexMatchTimeoutException) { return null; }
    }

    public static ProductoLookupCandidato Normalizar(ProductoLookupCandidato candidato)
    {
        var peso = candidato.PesoGramos is > 0 ? decimal.Round(candidato.PesoGramos.Value, 2, MidpointRounding.AwayFromZero) : (decimal?)null;
        var volumen = candidato.ContenidoMl is > 0 ? decimal.Round(candidato.ContenidoMl.Value, 2, MidpointRounding.AwayFromZero) : (decimal?)null;
        if (peso.HasValue && volumen.HasValue) (peso, volumen) = (null, null);
        return candidato with
        {
            CodigoBarras = CodigoValido(candidato.CodigoBarras) ? candidato.CodigoBarras : null,
            Nombre = Texto(candidato.Nombre, 150), Descripcion = Texto(candidato.Descripcion, 500),
            Marca = Texto(candidato.Marca, 100), Modelo = Texto(candidato.Modelo, 100),
            Color = Texto(candidato.Color, 50), Talla = Texto(candidato.Talla, 30),
            Presentacion = Texto(candidato.Presentacion, 100), CategoriaExterna = Texto(candidato.CategoriaExterna, 500),
            Fuente = Texto(candidato.Fuente, 100) ?? string.Empty,
            PesoGramos = peso is > 0 ? peso : null, ContenidoMl = volumen is > 0 ? volumen : null,
            ImagenUrl = Uri.TryCreate(candidato.ImagenUrl, UriKind.Absolute, out var url)
                && url.Scheme == Uri.UriSchemeHttps && url.Port == 443 && string.IsNullOrEmpty(url.UserInfo)
                && url.AbsoluteUri.Length <= 2048 ? url.AbsoluteUri : null
        };
    }

    public static (decimal? ContenidoMl, decimal? PesoGramos) Medida(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Length > 100) return (null, null);
        var match = Regex.Match(texto.Trim(), @"^(\d+(?:\.\d+)?)\s*(ml|cl|l|g|kg|lb|oz)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
        if (!match.Success || !decimal.TryParse(match.Groups[1].Value, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var valor) || valor <= 0) return (null, null);
        try
        {
            return match.Groups[2].Value.ToLowerInvariant() switch
            {
                "ml" => (valor, null), "cl" => (valor * 10m, null), "l" => (valor * 1000m, null),
                "g" => (null, valor), "kg" => (null, valor * 1000m),
                "lb" => (null, valor * 453.59237m), "oz" => (null, valor * 28.349523125m),
                _ => (null, null)
            };
        }
        catch (OverflowException) { return (null, null); }
    }
}
