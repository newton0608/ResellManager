namespace ResellManager.Application.DTOs;

public sealed record ClienteInput(string Nombres, string? Apellidos, string Telefono, string? Direccion, string? Observaciones);
public sealed record ClienteDto(int Id, string Nombres, string? Apellidos, string Telefono, string? Direccion, string? Observaciones, decimal Saldo);

public sealed record CategoriaInput(string Nombre, string? Observaciones, int? CategoriaPadreId = null);
public sealed record CategoriaDto(int Id, string Nombre, string? Observaciones,
    int? CategoriaPadreId = null, string? CategoriaPadre = null, bool TieneSubcategorias = false);

public sealed record ProductoInput(string? CodigoBarras, string Nombre, string? Descripcion, string? Marca, string? Modelo, string? Color, string? Talla, decimal PrecioSugerido, int CategoriaId, decimal? ContenidoMl = null, decimal? PesoGramos = null, string? Presentacion = null, int? CategoriaPrincipalId = null);
public sealed record ProductoDto(int Id, string CodigoInterno, string? CodigoBarras, string Nombre, string? Descripcion, string? Marca, string? Modelo, string? Color, string? Talla, decimal PrecioSugerido, int CategoriaId, string Categoria, decimal? ContenidoMl = null, decimal? PesoGramos = null, string? Presentacion = null, string? ImagenPrincipalRuta = null);

public sealed record ProveedorInput(string Nombre, string? Telefono, string? CodigoPais, string? Descripcion);
public sealed record ProveedorDto(int Id, string Nombre, string? Telefono, string? CodigoPais, string? Descripcion);
