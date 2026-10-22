namespace ResellManager.Application.DTOs;

public sealed record ProductoCursorDto(string Nombre, int Id, bool CoincidenciaExacta);
public sealed record ProductoPaginaDto(IReadOnlyList<ProductoDto> Items, bool HayMas, ProductoCursorDto? SiguienteCursor);
