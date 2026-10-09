namespace ResellManager.Application.DTOs;

public sealed record CatalogoProductosPaginaDto(
    IReadOnlyList<ProductoCatalogoDto> Items, bool HasMore, int? NextCursor);

public sealed record CategoriaCatalogoDto(int Id, string Nombre, int? CategoriaPadreId = null);

public sealed record CatalogoCategoriasPaginaDto(
    IReadOnlyList<CategoriaCatalogoDto> Items, bool HasMore, int? NextCursor);

public sealed record CatalogoMarcasPaginaDto(
    IReadOnlyList<string> Items, bool HasMore, string? NextCursor);

public sealed record CatalogoSeccionDto(
    CategoriaCatalogoDto Categoria, IReadOnlyList<ProductoCatalogoDto> Productos);

public sealed record CatalogoEscaparatePaginaDto(
    IReadOnlyList<CatalogoSeccionDto> Secciones, bool HasMore, int? NextCursor);

public sealed record CatalogoCategoriaContextoDto(
    CategoriaCatalogoDto Raiz, CategoriaCatalogoDto Seleccionada,
    CatalogoCategoriasPaginaDto Subcategorias);
