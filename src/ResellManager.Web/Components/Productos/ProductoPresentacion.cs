using System.Globalization;
using ResellManager.Application.DTOs;

namespace ResellManager.Web.Components.Productos;

public static class ProductoPresentacion
{
    private static readonly CultureInfo NumberCulture = CultureInfo.GetCultureInfo("en-US");

    public static string PrecioSugerido(decimal monto) =>
        $"Q {monto.ToString("N2", NumberCulture)}";

    public static string DatosBreves(ProductoDto producto)
    {
        var partes = new[] { producto.Marca, producto.Modelo, producto.Talla }
            .Where(valor => !string.IsNullOrWhiteSpace(valor));

        return string.Join(" · ", partes);
    }

    public static string TextoOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? "Sin información" : valor;
}
