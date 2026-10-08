using ResellManager.Application.DTOs;

namespace ResellManager.Web.Catalogo;

public sealed class CatalogoListadoModelo(ICatalogoPublicoClient cliente, ILogger logger) : IDisposable
{
    public IReadOnlyList<ProductoCatalogoDto> Productos { get; private set; } = [];
    public IReadOnlyList<CategoriaCatalogoOpcion> Categorias { get; private set; } = [];
    public IReadOnlyList<string> Marcas { get; private set; } = [];
    public string? Marca { get; private set; }
    public string Termino { get; private set; } = string.Empty;
    public int? CategoriaId { get; private set; }
    public bool Cargando { get; private set; } = true;
    public bool CategoriasCargadas { get; private set; }
    public string? Error { get; private set; }
    public bool TieneFiltros => !string.IsNullOrWhiteSpace(Termino) || CategoriaId.HasValue || !string.IsNullOrWhiteSpace(Marca);
    private CancellationTokenSource? solicitud;
    private bool descartado;

    public void EstablecerFiltros(string? termino, int? categoriaId, string? marca = null)
    {
        solicitud?.Cancel();
        Termino = termino ?? string.Empty;
        CategoriaId = categoriaId;
        Marca = string.IsNullOrWhiteSpace(marca) ? null : marca.Trim();
    }

    public Task BuscarAsync(string termino)
    {
        Termino = termino;
        return LeerAsync(300);
    }

    public Task FiltrarCategoriaAsync(int? categoriaId)
    {
        CategoriaId = categoriaId;
        return CargarAsync();
    }

    public Task FiltrarMarcaAsync(string? marca)
    {
        Marca = string.IsNullOrWhiteSpace(marca) ? null : marca.Trim();
        return CargarAsync();
    }

    public Task LimpiarAsync()
    {
        EstablecerFiltros(null, null);
        return CargarAsync();
    }

    public Task CargarAsync() => LeerAsync(0);

    private async Task LeerAsync(int demora)
    {
        if (descartado) return;
        solicitud?.Cancel();
        using var actual = new CancellationTokenSource();
        solicitud = actual;
        Cargando = true;
        Error = null;
        var termino = Termino.Trim();
        var categoria = CategoriaId;
        var marca = Marca;
        try
        {
            if (demora > 0) await Task.Delay(demora, actual.Token);
            IReadOnlyList<ProductoCatalogoDto> encontrados;
            if (!CategoriasCargadas || (termino.Length == 0 && !categoria.HasValue && string.IsNullOrWhiteSpace(marca)))
            {
                var todos = await cliente.ListarAsync(ct: actual.Token);
                actual.Token.ThrowIfCancellationRequested();
                // Solo opciones del listado público completo; nunca categorías administrativas.
                var categorias = todos.Select(p => new CategoriaCatalogoOpcion(p.CategoriaId, p.Categoria, p.CategoriaPadreId))
                    .Concat(todos.Where(p => p.CategoriaPadreId.HasValue).Select(p =>
                        new CategoriaCatalogoOpcion(p.CategoriaPadreId!.Value, p.CategoriaPadreNombre!, null)))
                    .DistinctBy(c => c.Id).ToArray();
                Categorias = categorias.Where(c => c.PadreId is null)
                    .OrderBy(c => c.Nombre, StringComparer.CurrentCultureIgnoreCase)
                    .SelectMany(raiz => new[] { raiz }.Concat(categorias.Where(c => c.PadreId == raiz.Id)
                        .OrderBy(c => c.Nombre, StringComparer.CurrentCultureIgnoreCase))).ToArray();
                Marcas = todos.Select(p => p.Marca?.Trim()).Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => m!).OrderBy(m => m, StringComparer.Ordinal)
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(m => m, StringComparer.CurrentCultureIgnoreCase).ToArray();
                CategoriasCargadas = true;
                encontrados = termino.Length == 0 && !categoria.HasValue && string.IsNullOrWhiteSpace(marca)
                    ? todos : await cliente.ListarAsync(termino, categoria, actual.Token, marca);
            }
            else encontrados = await cliente.ListarAsync(termino, categoria, actual.Token, marca);
            actual.Token.ThrowIfCancellationRequested();
            Productos = encontrados;
            if (Marca is not null)
                Marca = Marcas.FirstOrDefault(m => string.Equals(m, Marca, StringComparison.OrdinalIgnoreCase)) ?? Marca;
        }
        catch (OperationCanceledException) when (actual.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!actual.IsCancellationRequested)
            {
                logger.LogError(ex, "No fue posible consultar el catálogo público.");
                Productos = [];
                Error = "No pudimos cargar los productos. Intenta de nuevo en un momento.";
            }
        }
        finally
        {
            if (ReferenceEquals(solicitud, actual)) { solicitud = null; Cargando = false; }
        }
    }

    public void Dispose() { descartado = true; solicitud?.Cancel(); }
}

public sealed record CategoriaCatalogoOpcion(int Id, string Nombre, int? PadreId = null);
