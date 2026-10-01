namespace ResellManager.Application.DTOs;

public sealed record ProductoCatalogoDto(
    int Id,
    string Nombre,
    int CategoriaId,
    string Categoria,
    decimal PrecioPublico,
    bool TieneImagenPrincipal,
    bool Disponible);

public sealed record ProductoCatalogoDetalleDto(
    int Id,
    string Nombre,
    string? Descripcion,
    string? Marca,
    string? Modelo,
    string? Color,
    string? Talla,
    decimal? ContenidoMl,
    decimal? PesoGramos,
    string? Presentacion,
    int CategoriaId,
    string Categoria,
    decimal PrecioPublico,
    bool TieneImagenPrincipal,
    bool Disponible);
