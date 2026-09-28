namespace ResellManager.Web.Components.Productos;

public enum UnidadVolumen { Ml, L }

public enum UnidadPeso { G, Kg, Lb }

public static class ProductoMedidasConversion
{
    // La escala canonica de Producto es de dos decimales.
    public static decimal? AContenidoMl(decimal? valor, UnidadVolumen unidad) =>
        valor is null ? null : Redondear(valor.Value * (unidad == UnidadVolumen.L ? 1000m : 1m));

    public static decimal? APesoGramos(decimal? valor, UnidadPeso unidad) =>
        valor is null ? null : Redondear(valor.Value * (unidad switch
        {
            UnidadPeso.Kg => 1000m,
            UnidadPeso.Lb => 453.59237m,
            _ => 1m,
        }));

    public static (decimal? Valor, UnidadVolumen Unidad) VolumenParaEditar(decimal? contenidoMl) =>
        contenidoMl is >= 1000m
            ? (contenidoMl / 1000m, UnidadVolumen.L)
            : (contenidoMl, UnidadVolumen.Ml);

    public static (decimal? Valor, UnidadPeso Unidad) PesoParaEditar(decimal? pesoGramos) =>
        pesoGramos is >= 1000m
            ? (pesoGramos / 1000m, UnidadPeso.Kg)
            : (pesoGramos, UnidadPeso.G);

    private static decimal Redondear(decimal valor) =>
        Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
