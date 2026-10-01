namespace ResellManager.Application.DTOs;

public sealed record ImagenProductoPreparada(string IdentificadorTemporal);
public sealed record ImagenProductoGuardada(string RutaRelativa);
public sealed record ImagenProductoLectura(Stream Contenido, string ContentType);
