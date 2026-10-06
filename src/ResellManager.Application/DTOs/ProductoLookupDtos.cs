namespace ResellManager.Application.DTOs;

public enum EstadoLookupProveedor
{
    Encontrado, NoEncontrado, Timeout, LimitePeticiones, NoDisponible, RespuestaInvalida
}

public sealed record ProductoLookupCandidato
{
    public string? CodigoBarras { get; init; }
    public string? Nombre { get; init; }
    public string? Descripcion { get; init; }
    public string? Marca { get; init; }
    public string? Modelo { get; init; }
    public string? Color { get; init; }
    public string? Talla { get; init; }
    public string? Presentacion { get; init; }
    public decimal? PesoGramos { get; init; }
    public decimal? ContenidoMl { get; init; }
    public string? CategoriaExterna { get; init; }
    public string? ImagenUrl { get; init; }
    public string Fuente { get; init; } = string.Empty;
}

public sealed record ProductoLookupRespuesta(
    EstadoLookupProveedor Estado, ProductoLookupCandidato? Candidato = null);

public sealed record ProductoLookupIntento(string Fuente, EstadoLookupProveedor Estado);

public sealed class ProductoLookupRonda(string codigoConsultado)
{
    public string CodigoConsultado { get; } = codigoConsultado;
    public string? ErrorEntrada { get; internal set; }
    public ProductoDto? ProductoLocal { get; internal set; }
    public ProductoLookupCandidato? Candidato { get; internal set; }
    public IReadOnlyList<ProductoLookupIntento> Intentos => intentos.AsReadOnly();
    public bool Agotada { get; internal set; }
    internal int SiguienteProveedor { get; set; }
    internal SemaphoreSlim Exclusividad { get; } = new(1, 1);
    internal readonly List<ProductoLookupIntento> intentos = [];
}
