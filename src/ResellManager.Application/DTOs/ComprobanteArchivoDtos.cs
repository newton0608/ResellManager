namespace ResellManager.Application.DTOs;

public sealed record ArchivoComprobanteInput(
    Stream Contenido,
    string NombreOriginal,
    string ContentType
);

public sealed record DatosComprobanteCompraInput(
    string? NumeroDocumento,
    DateOnly Fecha,
    string? Observaciones
);

public sealed record ComprobantePreparadoDto(
    string IdentificadorTemporal,
    string RutaRelativa,
    string NombreArchivo,
    string ContentType,
    long TamanoBytes
);

public sealed record ComprobanteGuardadoDto(
    string RutaRelativa,
    string NombreArchivo,
    string ContentType,
    long TamanoBytes
);

public sealed record ArchivoComprobanteLecturaDto(
    Stream Contenido,
    string ContentType,
    long TamanoBytes
);
