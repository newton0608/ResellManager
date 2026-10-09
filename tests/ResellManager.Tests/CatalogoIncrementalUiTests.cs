using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.DTOs;
using ResellManager.Web.Catalogo;

namespace ResellManager.Tests;

public sealed class CatalogoIncrementalUiTests
{
    [Fact]
    public async Task Portada_LecturasAcotadasYOpcionesIndependientesSinListadoCompleto()
    {
        var api = new Cliente(); using var modelo = new CatalogoListadoModelo(api, NullLogger.Instance);
        await modelo.CargarAsync();
        Assert.True(modelo.EsPortada); Assert.Empty(modelo.Productos);
        Assert.Equal(3, modelo.Secciones.Count); Assert.Equal(10, modelo.Secciones[0].Productos.Count);
        Assert.True(modelo.HayMas); Assert.Equal(3, modelo.SiguienteCursor);
        Assert.Equal(1, api.OpcionesRaices); Assert.Equal(1, api.OpcionesMarcas); Assert.Equal(0, api.ListadosAntiguos);
        await modelo.CargarMasAsync();
        Assert.Equal(4, modelo.Secciones.Count); Assert.False(modelo.HayMas);
        await modelo.FiltrarCategoriaAsync(1);
        Assert.False(modelo.EsPortada); Assert.Equal(16, modelo.Productos.Count); Assert.Empty(modelo.Secciones);
        Assert.Equal(1, api.OpcionesRaices); Assert.Equal(1, api.OpcionesMarcas); Assert.Equal(0, api.ListadosAntiguos);
    }

    [Fact]
    public async Task Paginas16_RetryConservaResultadosYCursorHastaFinSinDuplicar()
    {
        var api = new Cliente(); using var modelo = new CatalogoListadoModelo(api, NullLogger.Instance);
        modelo.EstablecerFiltros(null, 1); await modelo.CargarAsync();
        Assert.Equal(16, modelo.Productos.Count); Assert.Equal(16, modelo.SiguienteCursor);
        api.ErrorPagina = true; await modelo.CargarMasAsync();
        Assert.NotNull(modelo.ErrorMas); Assert.Null(modelo.Error); Assert.Equal(16, modelo.Productos.Count);
        Assert.Equal(16, modelo.SiguienteCursor); Assert.True(modelo.HayMas);
        api.ErrorPagina = false; await modelo.CargarMasAsync();
        Assert.Null(modelo.ErrorMas); Assert.Equal(32, modelo.Productos.Count);
        await modelo.CargarMasAsync(); Assert.Equal(40, modelo.Productos.Count); Assert.False(modelo.HayMas);
        Assert.Equal(40, modelo.Productos.Select(p => p.Id).Distinct().Count());
        var consultas = api.Paginas.Count; await modelo.CargarMasAsync(); Assert.Equal(consultas, api.Paginas.Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PaginaAnterior_NuncaMezclaResultadosDeFiltrosNuevos(bool fallida)
    {
        var api = new Cliente(); using var modelo = new CatalogoListadoModelo(api, NullLogger.Instance);
        modelo.EstablecerFiltros("vieja", 1, "Acmé"); await modelo.CargarAsync();
        var atrasada = new TaskCompletionSource<CatalogoProductosPaginaDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Atrasada = atrasada.Task;
        var vieja = modelo.CargarMasAsync(); Assert.True(modelo.CargandoMas);
        await modelo.FiltrarCategoriaAsync(2);
        if (fallida) atrasada.SetException(new InvalidOperationException("Error privado antiguo"));
        else atrasada.SetResult(new([new(900, "Antigua", 1, "Salud", 10, false, true)], false, null));
        await vieja;
        Assert.Equal(2, modelo.CategoriaId); Assert.Equal(16, modelo.Productos.Count);
        Assert.DoesNotContain(modelo.Productos, p => p.Id == 900); Assert.Null(modelo.ErrorMas);
        Assert.Equal(("vieja", (int?)2, "Acmé", (int?)null), api.Paginas.Last());
        Assert.False(modelo.CargandoMas);
    }

    [Fact]
    public async Task ObserverRepetido_SoloUnaSolicitudYFiltrosReinicianCursor()
    {
        var api = new Cliente(); using var modelo = new CatalogoListadoModelo(api, NullLogger.Instance);
        modelo.EstablecerFiltros(null, 1); await modelo.CargarAsync();
        var atrasada = new TaskCompletionSource<CatalogoProductosPaginaDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Atrasada = atrasada.Task;
        var pendiente = modelo.CargarMasAsync(); await modelo.CargarMasAsync();
        Assert.Equal(2, api.Paginas.Count);
        atrasada.SetResult(new(api.Productos.Skip(16).Take(16).ToArray(), true, 32)); await pendiente;
        await modelo.FiltrarMarcaAsync("Acmé"); Assert.Equal(16, modelo.Productos.Count); Assert.Equal(16, modelo.SiguienteCursor);
        Assert.Null(api.Paginas.Last().Cursor);
        await modelo.LimpiarAsync(); Assert.True(modelo.EsPortada); Assert.Null(modelo.CategoriaId); Assert.Null(modelo.Marca);
        Assert.Empty(modelo.Productos); Assert.Equal(0, api.ListadosAntiguos);
    }

    [Fact]
    public async Task UrlHija_ReconstruyeRaizYChipSeleccionadoSinPaginaCompleta()
    {
        var api = new Cliente(); using var modelo = new CatalogoListadoModelo(api, NullLogger.Instance);
        modelo.EstablecerFiltros("vitamina", 11, " acmé "); await modelo.CargarAsync();
        Assert.Equal(1, modelo.Contexto!.Raiz.Id); Assert.Equal(11, modelo.Contexto.Seleccionada.Id);
        Assert.Equal("Acmé", modelo.Marca);
        Assert.Equal(("vitamina", (int?)11, "acmé", (int?)null), api.Paginas.Single());
        Assert.All(modelo.Categorias, c => Assert.Null(c.CategoriaPadreId)); Assert.Equal(0, api.ListadosAntiguos);
    }

    private sealed class Cliente : ICatalogoPublicoClient
    {
        public int ListadosAntiguos, OpcionesRaices, OpcionesMarcas;
        public bool ErrorPagina;
        public Task<CatalogoProductosPaginaDto>? Atrasada;
        public List<(string? Termino, int? Categoria, string? Marca, int? Cursor)> Paginas = [];
        public readonly ProductoCatalogoDto[] Productos = Enumerable.Range(1,40).Select(i => new ProductoCatalogoDto(i, $"Producto {i}",1,"Salud",10,false,true,"Acmé")).ToArray();
        public Task<IReadOnlyList<ProductoCatalogoDto>> ListarAsync(string? termino = null, int? categoriaId = null, CancellationToken ct = default, string? marca = null)
        { ListadosAntiguos++; throw new InvalidOperationException("La UI no debe solicitar el listado total."); }
        public Task<ProductoCatalogoDetalleDto?> ObtenerDetalleAsync(int id, CancellationToken ct = default) => Task.FromResult<ProductoCatalogoDetalleDto?>(null);
        public Task<CatalogoProductosPaginaDto> ListarPaginaAsync(string? termino, int? categoriaId, string? marca, int? cursor, CancellationToken ct = default)
        {
            Paginas.Add((termino,categoriaId,marca,cursor));
            if (cursor.HasValue && Atrasada is { } pending) { Atrasada = null; return pending; }
            if (cursor.HasValue && ErrorPagina) throw new InvalidOperationException("Error privado de siguiente página");
            var items = Productos.Where(p => p.Id > (cursor ?? 0)).Take(16).ToArray();
            return Task.FromResult(new CatalogoProductosPaginaDto(items, items.LastOrDefault()?.Id < 40, items.LastOrDefault()?.Id));
        }
        public Task<CatalogoEscaparatePaginaDto> LeerEscaparateAsync(int? cursor = null, CancellationToken ct = default)
        {
            var raices = Enumerable.Range(1,4).Where(i => i > (cursor ?? 0)).Take(3).Select(i => new CatalogoSeccionDto(new(i,$"Raíz {i}"), Productos.Take(i==1?10:1).Select(p => p with { Id = i==1?p.Id:100+i, CategoriaId=i }).ToArray())).ToArray();
            return Task.FromResult(new CatalogoEscaparatePaginaDto(raices, raices.LastOrDefault()?.Categoria.Id < 4, raices.LastOrDefault()?.Categoria.Id));
        }
        public Task<CatalogoCategoriasPaginaDto> LeerRaicesAsync(int? cursor = null, CancellationToken ct = default)
        { OpcionesRaices++; return Task.FromResult(new CatalogoCategoriasPaginaDto([new(1,"Salud"),new(2,"Ropa"),new(3,"Perfumes"),new(4,"Accesorios")],false,null)); }
        public Task<CatalogoMarcasPaginaDto> LeerMarcasAsync(string? cursor = null, CancellationToken ct = default)
        { OpcionesMarcas++; return Task.FromResult(new CatalogoMarcasPaginaDto(["Acmé"],false,null)); }
        public Task<CatalogoCategoriaContextoDto?> LeerContextoCategoriaAsync(int id, CancellationToken ct = default)
        { var root = new CategoriaCatalogoDto(id==11?1:id,"Salud"); return Task.FromResult<CatalogoCategoriaContextoDto?>(new(root,id==11?new(11,"Vitaminas",1):root,new([new(11,"Vitaminas",1)],false,null))); }
        public Task<CatalogoCategoriasPaginaDto> LeerSubcategoriasAsync(int raiz, int? cursor = null, CancellationToken ct = default) => Task.FromResult(new CatalogoCategoriasPaginaDto([],false,null));
    }
}
