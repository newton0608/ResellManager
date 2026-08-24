using System.Globalization;
using ResellManager.Application.DTOs;

namespace ResellManager.Web.Components.Clientes;

public static class ClientePresentacion
{
    private static readonly CultureInfo NumberCulture = CultureInfo.GetCultureInfo("en-US");

    public static string Moneda(decimal monto) => $"Q {monto.ToString("N2", NumberCulture)}";

    public static string NombreCompleto(ClienteDto cliente) =>
        string.IsNullOrWhiteSpace(cliente.Apellidos)
            ? cliente.Nombres
            : $"{cliente.Nombres} {cliente.Apellidos}";
}
