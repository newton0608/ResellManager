using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using ResellManager.Application.DTOs;

namespace ResellManager.Web.Components.Shared;

public sealed class FiltroHistorialModelo
{
    public string Termino { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    public bool Activo => !string.IsNullOrWhiteSpace(Termino) || !string.IsNullOrEmpty(Estado) || Desde.HasValue || Hasta.HasValue;
    public FiltroHistorial ToInput() => new(Termino, Desde, Hasta);
    public string? Validar() => ToInput().RangoValido ? null : "La fecha Desde no puede ser posterior a Hasta.";

    public string? Cargar(string? termino, string? desde, string? hasta, string? estado = null)
    {
        Termino = termino ?? string.Empty;
        Estado = estado?.ToLowerInvariant() ?? string.Empty;
        var validoDesde = LeerFecha(desde, out var inicio);
        var validoHasta = LeerFecha(hasta, out var fin);
        Desde = inicio; Hasta = fin;
        return !validoDesde || !validoHasta ? "La fecha del filtro no es válida. Selecciona Desde y Hasta nuevamente." : Validar();
    }

    public void Limpiar() { Termino = string.Empty; Estado = string.Empty; Desde = null; Hasta = null; }

    public string Url(string ruta, int? clienteId = null)
    {
        var valores = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(Termino)) valores["buscar"] = Termino.Trim();
        if (!string.IsNullOrEmpty(Estado)) valores["estado"] = Estado;
        if (Desde.HasValue) valores["desde"] = Desde.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (Hasta.HasValue) valores["hasta"] = Hasta.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (clienteId.HasValue) valores["cliente"] = clienteId.Value.ToString(CultureInfo.InvariantCulture);
        return QueryHelpers.AddQueryString(ruta, valores);
    }

    public Task NavegarAsync(NavigationManager navigation, string ruta, Func<Task> recargar, int? clienteId = null)
    {
        var url = Url(ruta, clienteId);
        if (navigation.ToAbsoluteUri(url).AbsoluteUri == navigation.Uri) return recargar();
        navigation.NavigateTo(url);
        return Task.CompletedTask;
    }

    private static bool LeerFecha(string? valor, out DateOnly? fecha)
    {
        fecha = null;
        if (string.IsNullOrWhiteSpace(valor)) return true;
        if (!DateOnly.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return false;
        fecha = parsed;
        return true;
    }
}
