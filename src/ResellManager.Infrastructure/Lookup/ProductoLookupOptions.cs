namespace ResellManager.Infrastructure.Lookup;

public sealed class ProductoLookupOptions
{
    public const string Seccion = "ProductoLookup";
    public string UserAgent { get; set; } = "ResellManager/1.0 (+https://github.com/newton0608/ResellManager)";
    public int TimeoutSegundos { get; set; } = 6;
    public bool OpenFactsHabilitado { get; set; } = true;
    public bool UpcitemdbHabilitado { get; set; } = true;
    public string? UpcitemdbUserKey { get; set; }
    public int OpenFactsPeticionesPorMinuto { get; set; } = 15;
    public int UpcitemdbPeticionesPorMinuto { get; set; } = 6;
    public int UpcitemdbPeticionesPorDia { get; set; } = 100;
}
