namespace ResellManager.Application.DTOs;

public sealed record ImagenProductoPreparada(string IdentificadorTemporal);
public sealed record ImagenProductoGuardada(string RutaRelativa);
public sealed record ImagenProductoLectura(Stream Contenido, string ContentType);
public sealed record ImagenProductoDto(Guid Id, int Orden, bool EsPortada);
public sealed record ImagenProductoEdicion(Guid? ImagenId = null, int? NuevaImagenIndice = null);
public sealed record GaleriaProductoEdicion(IReadOnlyList<ImagenProductoEdicion> ImagenesOrdenadas, int PortadaIndice = 0);
