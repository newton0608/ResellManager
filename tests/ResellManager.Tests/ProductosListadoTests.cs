using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Web.Components.Pages;

namespace ResellManager.Tests;

public sealed class ProductosListadoTests
{
    [Fact]
    public async Task Scroll_SerializaSolicitudes_ConservaFiltroAplicadoYResultadosAnteError_YReiniciaBusqueda()
    {
        var pendiente = new TaskCompletionSource<ProductoPaginaDto>();
        var llamadas = 0;
        var servicio = new ServicioPaginas((_, _, _, ct) => ++llamadas switch
        {
            1 => Task.FromResult(new ProductoPaginaDto(Filas(1, 20), true, new("Producto 20", 20, false))),
            2 => pendiente.Task.WaitAsync(ct),
            3 => Task.FromResult(new ProductoPaginaDto(Filas(21, 20), false, null)),
            _ => Task.FromResult(new ProductoPaginaDto(Filas(99, 1), false, null))
        });
        var pagina = Crear(servicio);
        await Invocar(pagina, "CargarProductosAsync");
        Assert.Equal(20, Get<List<ProductoDto>>(pagina, "ProductosEncontrados").Count);
        Set(pagina, "Termino", "nuevo");
        Set(pagina, "CategoriaId", 9);
        var carga = Invocar(pagina, "CargarMasAsync");
        await Invocar(pagina, "CargarMasAsync");
        Assert.Equal(2, llamadas);
        Assert.Equal(string.Empty, servicio.Consultas[1].Termino);
        Assert.Null(servicio.Consultas[1].Categoria);
        pendiente.SetException(new InvalidOperationException("Fallo de lectura simulado"));
        await carga;
        Assert.NotNull(Get<string?>(pagina, "ErrorMas"));
        Assert.Equal(20, Get<List<ProductoDto>>(pagina, "ProductosEncontrados").Count);
        Assert.Equal(servicio.Consultas[1].Cursor, Get<ProductoCursorDto?>(pagina, "SiguienteCursor"));
        await Invocar(pagina, "CargarMasAsync");
        Assert.Equal(40, Get<List<ProductoDto>>(pagina, "ProductosEncontrados").Count);
        Assert.Null(Get<string?>(pagina, "ErrorMas"));
        Assert.False(Get<bool>(pagina, "HayMas"));
        await Invocar(pagina, "CargarMasAsync");
        Assert.Equal(3, llamadas);
        await Invocar(pagina, "BuscarAsync");
        Assert.Equal(99, Assert.Single(Get<List<ProductoDto>>(pagina, "ProductosEncontrados")).Id);
        Assert.Equal("nuevo", servicio.Consultas[^1].Termino);
        Assert.Equal(9, servicio.Consultas[^1].Categoria);
        Assert.Null(servicio.Consultas[^1].Cursor);
        await pagina.DisposeAsync();
    }

    [Fact]
    public async Task NavegarDuranteCarga_CancelaConsultaYSuResultadoNoModificaElListado()
    {
        var pendiente = new TaskCompletionSource<ProductoPaginaDto>();
        CancellationToken token = default;
        var pagina = Crear(new ServicioPaginas((_, _, _, ct) => { token = ct; return pendiente.Task.WaitAsync(ct); }));
        var carga = Invocar(pagina, "CargarProductosAsync");
        await pagina.DisposeAsync();
        await carga;
        Assert.True(token.IsCancellationRequested);
        Assert.Empty(Get<List<ProductoDto>>(pagina, "ProductosEncontrados"));
        Assert.Null(Get<string?>(pagina, "ErrorMessage"));
    }

    private static Productos Crear(IProductoService servicio)
    {
        var pagina = new Productos();
        Set(pagina, "ProductoService", servicio);
        Set(pagina, "CategoriaService", new Categorias());
        Set(pagina, "Logger", NullLogger<Productos>.Instance);
        return pagina;
    }
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static void Set(object objeto, string nombre, object valor) => objeto.GetType().GetProperty(nombre, Flags)!.SetValue(objeto, valor);
    private static T Get<T>(object objeto, string nombre) => (T)(objeto.GetType().GetProperty(nombre, Flags)?.GetValue(objeto)
        ?? objeto.GetType().GetField(nombre, Flags)?.GetValue(objeto))!;
    private static Task Invocar(object objeto, string metodo) => (Task)objeto.GetType().GetMethod(metodo, Flags)!.Invoke(objeto, null)!;
    private static List<ProductoDto> Filas(int inicio, int cantidad) => Enumerable.Range(inicio, cantidad)
        .Select(i => new ProductoDto(i, $"PRO-{i}", null, $"Producto {i}", null, null, null, null, null, 0m, 1, "Ropa")).ToList();

    private sealed class ServicioPaginas(Func<string?, int?, ProductoCursorDto?, CancellationToken, Task<ProductoPaginaDto>> consultar) : IProductoService
    {
        public List<(string? Termino, int? Categoria, ProductoCursorDto? Cursor)> Consultas { get; } = [];
        public Task<ProductoPaginaDto> ListarPaginaAsync(string? termino = null, int? categoriaId = null,
            ProductoCursorDto? cursor = null, int tamano = 20, CancellationToken ct = default)
        {
            Assert.Equal(20, tamano);
            Consultas.Add((termino, categoriaId, cursor));
            return consultar(termino, categoriaId, cursor, ct);
        }
        public Task<IReadOnlyList<ProductoDto>> ListarAsync(CancellationToken ct = default) => throw new InvalidOperationException("No debe consultar el catálogo completo.");
        public Task<IReadOnlyList<ProductoDto>> BuscarAsync(string termino, CancellationToken ct = default, int? limite = null, int? categoriaId = null) => throw new InvalidOperationException("La búsqueda también debe paginar.");
        public Task<ServiceResult<ProductoDto>> CrearAsync(ProductoInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ServiceResult<ProductoDto>> EditarAsync(int id, ProductoInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ServiceResult<ProductoDto>> ObtenerPorIdAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Categorias : ICategoriaService
    {
        public Task<IReadOnlyList<CategoriaDto>> ListarAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CategoriaDto>>([]);
        public Task<ServiceResult<CategoriaDto>> CrearAsync(CategoriaInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ServiceResult<CategoriaDto>> EditarAsync(int id, CategoriaInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ServiceResult<CategoriaDto>> ObtenerPorIdAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
