using System.Net;
using System.Text.RegularExpressions;

namespace ResellManager.Tests;

internal static class HtmlPrueba
{
    public static void TituloPrincipal(string html, string id, string texto)
    {
        var titulo = Regex.Match(html, "<h1\\b[^>]*\\bid=\"" + Regex.Escape(id) + "\"[^>]*>(.*?)</h1>", RegexOptions.Singleline);
        Assert.True(titulo.Success, $"No se encontró el título principal {id}.");
        Assert.Equal(texto, Texto(titulo.Groups[1].Value));
    }

    public static void Enlace(string html, string ruta, string texto)
    {
        var enlaces = Regex.Matches(html, "<a\\b[^>]*\\bhref=\"" + Regex.Escape(ruta) + "\"[^>]*>(.*?)</a>", RegexOptions.Singleline);
        Assert.Contains(enlaces.Cast<Match>(), enlace => Texto(enlace.Groups[1].Value) == texto);
    }

    private static string Texto(string contenido) => Regex.Replace(
        WebUtility.HtmlDecode(Regex.Replace(contenido, "<[^>]+>", string.Empty)), "\\s+", " ").Trim();
}