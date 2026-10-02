using ResellManager.Application.DTOs;

namespace ResellManager.Web.Catalogo;

public sealed class CatalogoListadoModelo(ICatalogoPublicoClient cliente, ILogger logger) : IDisposable
{
    public IReadOnlyList<ProductoCatalogoDto> Productos { get; private set; } = [];
    public IReadOnlyList<CategoriaCatalogoOpcion> Categorias { get; private set; } = [];
    public string Termino { get; private set; } = string.Empty;
    public int? CategoriaId { get; private set; }
    public bool Cargando { get; private set; } = true;
    public bool CategoriasCargadas { get; private set; }
    public string? Error { get; private set; }
    public bool TieneFiltros => !string.IsNullOrWhiteSpace(Termino) || CategoriaId.HasValue;
    private CancellationTokenSource? solicitud;
    private bool descartado;

    public void EstablecerFiltros(string? termino, int? categoriaId)
    {
        solicitud?.Cancel();
        Termino = termino ?? string.Empty;
        CategoriaId = categoriaId;
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
        try
        {
            if (demora > 0) await Task.Delay(demora, actual.Token);
            IReadOnlyList<ProductoCatalogoDto> encontrados;
            if (!CategoriasCargadas || (termino.Length == 0 && !categoria.HasValue))
            {
                var todos = await cliente.ListarAsync(ct: actual.Token);
                actual.Token.ThrowIfCancellationRequested();
                // Solo opciones del listado público completo; nunca categorías administrativas.
                Categorias = todos.Select(p => new CategoriaCatalogoOpcion(p.CategoriaId, p.Categoria))
                    .DistinctBy(c => c.Id).OrderBy(c => c.Nombre, StringComparer.CurrentCultureIgnoreCase).ToArray();
                CategoriasCargadas = true;
                encontrados = termino.Length == 0 && !categoria.HasValue
                    ? todos : await cliente.ListarAsync(termino, categoria, actual.Token);
            }
            else encontrados = await cliente.ListarAsync(termino, categoria, actual.Token);
            actual.Token.ThrowIfCancellationRequested();
            Productos = encontrados;
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

public sealed record CategoriaCatalogoOpcion(int Id, string Nombre);
