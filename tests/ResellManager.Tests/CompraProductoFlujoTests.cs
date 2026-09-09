using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Compras;
using ResellManager.Web.Components.Pages;
using ResellManager.Web.Components.Productos;

namespace ResellManager.Tests;

public sealed class CompraProductoFlujoTests
{
    [Theory]
    [InlineData("camisa")]
    [InlineData("CAMISA")]
    [InlineData("7401234567890")]
    [InlineData("codigo")]
    public async Task BuscarProducto_ReutilizaTresReferenciasSinDistinguirMayusculas(string termino)
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new ProductoService(test.Db);
        var creado = await servicio.CrearAsync(Input(test.Categoria.Id, "7401234567890"));
        var busqueda = termino == "codigo" ? creado.Value!.CodigoInterno.ToLowerInvariant() : termino;
        var encontrados = await servicio.BuscarAsync(busqueda, limite: 12);
        Assert.Equal(creado.Value!.Id, Assert.Single(encontrados).Id);
    }

    [Fact]
    public async Task BuscarProducto_SinCoincidenciasYLimiteConCodigoExactoPrimero()
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new ProductoService(test.Db);
        for (var i = 0; i < 15; i++)
            await servicio.CrearAsync(Input(test.Categoria.Id, null) with { Nombre = $"AAA 74012 {i}" });
        var exacto = await servicio.CrearAsync(Input(test.Categoria.Id, "74012") with { Nombre = "ZZZ exacto" });
        var encontrados = await servicio.BuscarAsync("74012", limite: 12);
        Assert.Equal(12, encontrados.Count);
        Assert.Equal(exacto.Value!.Id, encontrados[0].Id);
        Assert.Empty(await servicio.BuscarAsync("sin-coincidencia"));
        Assert.Equal(16, (await servicio.BuscarAsync("74012")).Count); // El listado normal no se trunca.
    }

    [Theory]
    [InlineData(null)]
    [InlineData("7401234567890")]
    public async Task AltaDesdeCompra_SeleccionaProductoYConservaModeloCompleto(string? barras)
    {
        await using var test = await TestDatabase.CreateAsync();
        var compra = new CompraNueva();
        var modelo = Obtener<CompraFormModel>(compra, "Modelo");
        modelo.ProveedorId = test.Proveedor.Id;
        modelo.FechaCompra = new DateOnly(2026, 9, 1);
        modelo.FechaIngreso = new DateOnly(2026, 9, 2);
        modelo.Observaciones = "Compra sin perder";
        modelo.NumeroDocumento = "FACTURA-123";
        var linea = modelo.Detalles[0];
        linea.Cantidad = 3;
        linea.CostoUnitario = 45m;
        modelo.Detalles.Add(new DetalleCompraFormModel { ProductoId = test.Producto.Id, Cantidad = 2, CostoUnitario = 10m });
        var antes = modelo.ToInput("COM-PRUEBA");
        Invocar(compra, "AbrirAlta", linea);

        var panel = new ProductoAltaPanel();
        Establecer(panel, "ProductoService", new ProductoService(test.Db));
        Establecer(panel, "CategoriaService", new CategoriaService(test.Db));
        Establecer(panel, "Logger", NullLogger<ProductoAltaPanel>.Instance);
        Establecer(panel, "OnCreado", EventCallback.Factory.Create<ProductoDto>(new object(),
            producto => Invocar(compra, "ProductoCreado", producto)));
        await InvocarAsync(panel, "CargarCategoriasAsync");
        var productoModelo = new ProductoFormModel { Nombre = "Camisa nueva", CodigoBarras = barras, CategoriaId = test.Categoria.Id };
        await InvocarAsync(panel, "GuardarAsync", productoModelo);

        Assert.Null(Obtener<object?>(compra, "DetalleAlta"));
        Assert.Same(modelo, Obtener<CompraFormModel>(compra, "Modelo"));
        Assert.Same(linea, modelo.Detalles[0]);
        Assert.NotNull(linea.ProductoSeleccionado);
        Assert.Equal(linea.ProductoSeleccionado.Id, linea.ProductoId);
        Assert.Matches("^PRO-[A-F0-9]{32}$", linea.ProductoSeleccionado.CodigoInterno);
        Assert.Equal(barras, linea.ProductoSeleccionado.CodigoBarras);
        var despues = modelo.ToInput("COM-PRUEBA");
        Assert.Equal(antes.ProveedorId, despues.ProveedorId);
        Assert.Equal(antes.FechaCompra, despues.FechaCompra);
        Assert.Equal(antes.FechaIngreso, despues.FechaIngreso);
        Assert.Equal(antes.Origen, despues.Origen);
        Assert.Equal(antes.Observaciones, despues.Observaciones);
        Assert.Equal("FACTURA-123", modelo.NumeroDocumento);
        Assert.Equal(3, linea.Cantidad);
        Assert.Equal(45m, linea.CostoUnitario);
        Assert.Equal(antes.Detalles.Last(), despues.Detalles.Last());
        Assert.Contains("registrado y seleccionado", Obtener<string>(compra, "ConfirmacionProducto"));
        var resultado = await new CompraService(test.Db).RegistrarAsync(despues);
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal(155m, resultado.Value!.Total);
    }

    [Fact]
    public async Task Buscador_DebounceCancelaTextoAnteriorYSeleccionLimpiaResultados()
    {
        var servicio = new ProductoFalso();
        using var provider = new ServiceCollection().AddSingleton<IProductoService>(servicio).BuildServiceProvider();
        using var buscador = new ProductoBuscador();
        Establecer(buscador, "ScopeFactory", provider.GetRequiredService<IServiceScopeFactory>());
        Establecer(buscador, "Logger", NullLogger<ProductoBuscador>.Instance);
        ProductoDto? seleccionado = null;
        Establecer(buscador, "OnSeleccionado", EventCallback.Factory.Create<ProductoDto?>(new object(), producto => seleccionado = producto));
        await InvocarAsync(buscador, "CambiarTextoAsync", new ChangeEventArgs { Value = "c" });
        Assert.Empty(servicio.Consultas);
        var anterior = InvocarAsync(buscador, "CambiarTextoAsync", new ChangeEventArgs { Value = "ca" });
        var actual = InvocarAsync(buscador, "CambiarTextoAsync", new ChangeEventArgs { Value = "camisa" });
        await Task.WhenAll(anterior, actual);
        Assert.Equal(("camisa", 12), Assert.Single(servicio.Consultas));
        Assert.True(Obtener<bool>(buscador, "MostrarResultados"));
        await InvocarAsync(buscador, "SeleccionarAsync", servicio.Producto);
        Assert.Same(servicio.Producto, seleccionado);
        Assert.False(Obtener<bool>(buscador, "MostrarResultados"));
        await InvocarAsync(buscador, "CambiarTextoAsync", new ChangeEventArgs { Value = "" });
        Assert.Null(seleccionado);
    }

    private static ProductoInput Input(int categoria, string? barras) =>
        new(barras, "Camisa azul", null, null, null, null, null, 100m, categoria);
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static void Establecer(object x, string propiedad, object valor) => x.GetType().GetProperty(propiedad, Flags)!.SetValue(x, valor);
    private static T Obtener<T>(object x, string propiedad) => (T)x.GetType().GetProperty(propiedad, Flags)!.GetValue(x)!;
    private static object? Invocar(object x, string metodo, params object[] args) => x.GetType().GetMethod(metodo, Flags)!.Invoke(x, args);
    private static Task InvocarAsync(object x, string metodo, params object[] args) => (Task)Invocar(x, metodo, args)!;

    private sealed class ProductoFalso : IProductoService
    {
        public ProductoDto Producto { get; } = new(1, "PRO-PRUEBA", null, "Camisa", null, null, null, null, null, 0, 1, "Ropa");
        public List<(string, int?)> Consultas { get; } = [];
        public Task<IReadOnlyList<ProductoDto>> BuscarAsync(string termino, CancellationToken ct = default, int? limite = null)
        {
            Consultas.Add((termino, limite));
            return Task.FromResult<IReadOnlyList<ProductoDto>>([Producto]);
        }
        public Task<IReadOnlyList<ProductoDto>> ListarAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ServiceResult<ProductoDto>> ObtenerPorIdAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ServiceResult<ProductoDto>> CrearAsync(ProductoInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ServiceResult<ProductoDto>> EditarAsync(int id, ProductoInput input, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
