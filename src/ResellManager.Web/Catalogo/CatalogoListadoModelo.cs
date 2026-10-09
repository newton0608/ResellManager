using ResellManager.Application.DTOs;

namespace ResellManager.Web.Catalogo;

public sealed class CatalogoListadoModelo(ICatalogoPublicoClient cliente, ILogger logger) : IDisposable
{
    public IReadOnlyList<ProductoCatalogoDto> Productos { get; private set; } = [];
    public IReadOnlyList<CatalogoSeccionDto> Secciones { get; private set; } = [];
    public IReadOnlyList<CategoriaCatalogoDto> Categorias { get; private set; } = [];
    public IReadOnlyList<string> Marcas { get; private set; } = [];
    public CatalogoCategoriaContextoDto? Contexto { get; private set; }
    public string Termino { get; private set; } = string.Empty;
    public int? CategoriaId { get; private set; }
    public string? Marca { get; private set; }
    public bool TieneFiltros => !string.IsNullOrWhiteSpace(Termino) || CategoriaId.HasValue || !string.IsNullOrWhiteSpace(Marca);
    public bool EsPortada => !TieneFiltros;
    public bool Cargando { get; private set; } = true;
    public bool CargandoMas { get; private set; }
    public bool CargandoOpciones { get; private set; }
    public bool CategoriasCargadas { get; private set; }
    public bool HayMas { get; private set; }
    public int? SiguienteCursor { get; private set; }
    public bool HayMasRaices { get; private set; }
    public bool HayMasMarcas { get; private set; }
    public string? Error { get; private set; }
    public string? ErrorMas { get; private set; }
    public string? ErrorOpciones { get; private set; }
    private int? cursorRaices;
    private string? cursorMarcas;
    private long version;
    private bool descartado;
    private CancellationTokenSource? solicitud;
    private CancellationTokenSource? pagina;
    private readonly CancellationTokenSource vida = new();
    private Func<Task>? reintentarOpciones;

    public void EstablecerFiltros(string? termino, int? categoriaId, string? marca = null)
    {
        CancelarLecturas();
        Termino = termino ?? string.Empty;
        CategoriaId = categoriaId;
        Marca = string.IsNullOrWhiteSpace(marca) ? null : marca.Trim();
        Contexto = null;
        ErrorOpciones = null; reintentarOpciones = null;
    }
    private void CancelarLecturas()
    {
        version++;
        solicitud?.Cancel(); pagina?.Cancel();
        CargandoMas = false;
    }
    public Task BuscarAsync(string termino) { Termino = termino; return LeerInicialAsync(300); }
    public Task FiltrarCategoriaAsync(int? id) { CategoriaId = id; Contexto = null; ErrorOpciones = null; reintentarOpciones = null; return CargarAsync(); }
    public Task FiltrarMarcaAsync(string? marca) { Marca = string.IsNullOrWhiteSpace(marca) ? null : marca.Trim(); return CargarAsync(); }
    public Task LimpiarAsync() { EstablecerFiltros(null, null); return CargarAsync(); }
    public Task CargarAsync() => LeerInicialAsync(0);

    private async Task LeerInicialAsync(int demora)
    {
        if (descartado) return;
        CancelarLecturas();
        var revision = version;
        using var actual = CancellationTokenSource.CreateLinkedTokenSource(vida.Token);
        solicitud = actual;
        Cargando = true; Error = ErrorMas = null; HayMas = false; SiguienteCursor = null;
        Productos = []; Secciones = [];
        var termino = Termino.Trim(); var categoria = CategoriaId; var marca = Marca;
        try
        {
            if (demora > 0) await Task.Delay(demora, actual.Token);
            if (!CategoriasCargadas)
            {
                var raices = cliente.LeerRaicesAsync(ct: actual.Token);
                var marcas = cliente.LeerMarcasAsync(ct: actual.Token);
                await Task.WhenAll(raices, marcas);
                actual.Token.ThrowIfCancellationRequested();
                Categorias = raices.Result.Items; HayMasRaices = raices.Result.HasMore; cursorRaices = raices.Result.NextCursor;
                Marcas = marcas.Result.Items; HayMasMarcas = marcas.Result.HasMore; cursorMarcas = marcas.Result.NextCursor;
                CategoriasCargadas = true;
            }
            var contexto = categoria.HasValue ? await cliente.LeerContextoCategoriaAsync(categoria.Value, actual.Token) : null;
            if (termino.Length == 0 && !categoria.HasValue && marca is null)
            {
                var resultado = await cliente.LeerEscaparateAsync(ct: actual.Token);
                actual.Token.ThrowIfCancellationRequested();
                if (revision != version) return;
                Secciones = resultado.Secciones; HayMas = resultado.HasMore; SiguienteCursor = resultado.NextCursor;
            }
            else
            {
                var resultado = await cliente.ListarPaginaAsync(termino, categoria, marca, null, actual.Token);
                actual.Token.ThrowIfCancellationRequested();
                if (revision != version) return;
                Productos = resultado.Items; HayMas = resultado.HasMore; SiguienteCursor = resultado.NextCursor;
            }
            Contexto = contexto;
            if (Marca is not null) Marca = Marcas.FirstOrDefault(m => string.Equals(m, Marca, StringComparison.OrdinalIgnoreCase)) ?? Marca;
        }
        catch (OperationCanceledException) when (actual.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!actual.IsCancellationRequested && revision == version)
            {
                logger.LogError(ex, "No fue posible consultar la página pública del catálogo.");
                Error = "No pudimos cargar el catálogo. Intenta de nuevo en un momento.";
            }
        }
        finally { if (ReferenceEquals(solicitud, actual)) { solicitud = null; Cargando = false; } }
    }

    public async Task CargarMasAsync()
    {
        if (descartado || Cargando || CargandoMas || !HayMas) return;
        var revision = version; var esPortada = EsPortada;
        var termino = Termino.Trim(); var categoria = CategoriaId; var marca = Marca; var cursor = SiguienteCursor;
        using var actual = CancellationTokenSource.CreateLinkedTokenSource(vida.Token);
        pagina = actual; CargandoMas = true; ErrorMas = null;
        try
        {
            if (esPortada)
            {
                var resultado = await cliente.LeerEscaparateAsync(cursor, actual.Token);
                actual.Token.ThrowIfCancellationRequested();
                if (revision != version) return;
                Secciones = Secciones.Concat(resultado.Secciones).DistinctBy(s => s.Categoria.Id).ToArray();
                HayMas = resultado.HasMore; SiguienteCursor = resultado.NextCursor;
            }
            else
            {
                var resultado = await cliente.ListarPaginaAsync(termino, categoria, marca, cursor, actual.Token);
                actual.Token.ThrowIfCancellationRequested();
                if (revision != version) return;
                Productos = Productos.Concat(resultado.Items).DistinctBy(p => p.Id).ToArray();
                HayMas = resultado.HasMore; SiguienteCursor = resultado.NextCursor;
            }
        }
        catch (OperationCanceledException) when (actual.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!actual.IsCancellationRequested && revision == version)
            {
                logger.LogError(ex, "No fue posible consultar la siguiente página pública.");
                ErrorMas = "No pudimos cargar más. Los resultados anteriores siguen disponibles.";
            }
        }
        finally { if (ReferenceEquals(pagina, actual)) { pagina = null; CargandoMas = false; } }
    }

    public async Task CargarMasRaicesAsync()
    {
        if (descartado || CargandoOpciones || !HayMasRaices) return;
        var token = vida.Token;
        CargandoOpciones = true; ErrorOpciones = null;
        try
        {
            var resultado = await cliente.LeerRaicesAsync(cursorRaices, token);
            token.ThrowIfCancellationRequested();
            Categorias = Categorias.Concat(resultado.Items).DistinctBy(c => c.Id).ToArray();
            cursorRaices = resultado.NextCursor; HayMasRaices = resultado.HasMore;
        }
        catch (OperationCanceledException) when (vida.IsCancellationRequested) { }
        catch (Exception ex) { FalloOpciones(ex, CargarMasRaicesAsync); }
        finally { CargandoOpciones = false; }
    }
    public async Task CargarMasMarcasAsync()
    {
        if (descartado || CargandoOpciones || !HayMasMarcas) return;
        var token = vida.Token;
        CargandoOpciones = true; ErrorOpciones = null;
        try
        {
            var resultado = await cliente.LeerMarcasAsync(cursorMarcas, token);
            token.ThrowIfCancellationRequested();
            Marcas = Marcas.Concat(resultado.Items).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            cursorMarcas = resultado.NextCursor; HayMasMarcas = resultado.HasMore;
        }
        catch (OperationCanceledException) when (vida.IsCancellationRequested) { }
        catch (Exception ex) { FalloOpciones(ex, CargarMasMarcasAsync); }
        finally { CargandoOpciones = false; }
    }
    public async Task CargarMasSubcategoriasAsync()
    {
        if (descartado || CargandoOpciones || Contexto?.Subcategorias.HasMore != true) return;
        var revision = version; var contexto = Contexto;
        var token = vida.Token;
        CargandoOpciones = true; ErrorOpciones = null;
        try
        {
            var resultado = await cliente.LeerSubcategoriasAsync(contexto.Raiz.Id, contexto.Subcategorias.NextCursor, token);
            token.ThrowIfCancellationRequested();
            if (revision != version) return;
            Contexto = contexto with { Subcategorias = resultado with
            { Items = contexto.Subcategorias.Items.Concat(resultado.Items).DistinctBy(c => c.Id).ToArray() } };
        }
        catch (OperationCanceledException) when (vida.IsCancellationRequested) { }
        catch (Exception ex) { if (revision == version) FalloOpciones(ex, CargarMasSubcategoriasAsync); }
        finally { CargandoOpciones = false; }
    }
    private void FalloOpciones(Exception ex, Func<Task> reintento)
    {
        logger.LogError(ex, "No fue posible cargar más opciones públicas.");
        ErrorOpciones = "No pudimos cargar más opciones. Intenta de nuevo.";
        reintentarOpciones = reintento;
    }
    public Task ReintentarOpcionesAsync() => reintentarOpciones?.Invoke() ?? Task.CompletedTask;
    public void Dispose() { descartado = true; CancelarLecturas(); vida.Cancel(); vida.Dispose(); }
}
