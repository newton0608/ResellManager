namespace ResellManager.Application.DTOs;

public sealed record ProductoCursorDto(string Nombre, int Id, bool CoincidenciaExacta, string? Contexto = null);
public sealed record ProductoPaginaDto(IReadOnlyList<ProductoDto> Items, bool HayMas, ProductoCursorDto? SiguienteCursor);
