using ResellManager.Application.DTOs;

namespace ResellManager.Web.Components.Categorias;

public static class CategoriaPresentacion
{
    public static string Etiqueta(CategoriaDto categoria) => categoria.CategoriaPadreId.HasValue
        ? $"{categoria.CategoriaPadre} → {categoria.Nombre}"
        : categoria.Nombre;

    public static string Etiqueta(ProductoDto producto, IReadOnlyList<CategoriaDto> categorias)
    {
        var categoria = categorias.FirstOrDefault(x => x.Id == producto.CategoriaId);
        return categoria is null ? producto.Categoria : Etiqueta(categoria);
    }

    public static IReadOnlyList<CategoriaDto> Ordenar(IReadOnlyList<CategoriaDto> categorias)
    {
        var ordenadas = new List<CategoriaDto>();
        foreach (var raiz in categorias.Where(x => x.CategoriaPadreId is null).OrderBy(x => x.Nombre))
        {
            ordenadas.Add(raiz);
            ordenadas.AddRange(categorias.Where(x => x.CategoriaPadreId == raiz.Id).OrderBy(x => x.Nombre));
        }
        return ordenadas;
    }
}
