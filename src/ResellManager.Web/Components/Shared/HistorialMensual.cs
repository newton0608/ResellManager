using System.Globalization;

namespace ResellManager.Web.Components.Shared;

public sealed record GrupoMensual<T>(int Anio, int Mes, string Titulo, IReadOnlyList<T> Registros);

public static class HistorialMensual
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-GT");

    public static IReadOnlyList<GrupoMensual<T>> Agrupar<T>(IEnumerable<T> registros,
        Func<T, DateOnly> fecha, Func<T, int> id) => registros
        .OrderByDescending(fecha).ThenByDescending(id)
        .GroupBy(x => (fecha(x).Year, fecha(x).Month))
        .Select(g => new GrupoMensual<T>(g.Key.Year, g.Key.Month,
            $"{Cultura.TextInfo.ToTitleCase(Cultura.DateTimeFormat.GetMonthName(g.Key.Month))} {g.Key.Year}", g.ToArray()))
        .ToArray();
}
