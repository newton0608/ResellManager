using System.Globalization;
using ResellManager.Application.DTOs;

namespace ResellManager.Web.Catalogo;

public static class CatalogoPresentacion
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-GT");
    public static string Precio(decimal precioPublico) => $"Q {precioPublico.ToString("N2", Cultura)}";

    public static IEnumerable<(string Etiqueta, string Valor)> Caracteristicas(ProductoCatalogoDetalleDto producto)
    {
        var datos = new (string Etiqueta, string? Valor)[]
        {
            ("Marca", producto.Marca), ("Modelo", producto.Modelo), ("Color", producto.Color), ("Talla", producto.Talla),
            ("Contenido", producto.ContenidoMl is { } ml ? $"{ml.ToString("0.##", Cultura)} ml" : null),
            ("Peso", producto.PesoGramos is { } gramos ? $"{gramos.ToString("0.##", Cultura)} g" : null),
            ("Presentación", producto.Presentacion)
        };
        foreach (var (etiqueta, valor) in datos)
            if (!string.IsNullOrWhiteSpace(valor)) yield return (etiqueta, valor);
    }
}
