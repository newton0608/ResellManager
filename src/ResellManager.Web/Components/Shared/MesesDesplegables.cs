namespace ResellManager.Web.Components.Shared;

public sealed class MesesDesplegables
{
    private readonly HashSet<(int, int)> abiertos = [];
    public bool Abierto(int anio, int mes) => abiertos.Contains((anio, mes));
    public void Alternar(int anio, int mes) { if (!abiertos.Remove((anio, mes))) abiertos.Add((anio, mes)); }
    public void Reiniciar<T>(IReadOnlyList<GrupoMensual<T>> grupos)
    {
        abiertos.Clear();
        if (grupos.Count > 0) abiertos.Add((grupos[0].Anio, grupos[0].Mes));
    }
}
