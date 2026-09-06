using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Categorias;
using ResellManager.Web.Components.Productos;

namespace ResellManager.Tests;

public sealed class CategoriaModuloTests
{
    [Fact]
    public void Formulario_ExigeSolamenteNombre()
    {
        var errores = new List<ValidationResult>();
        var invalido = new CategoriaFormModel();

        var esValido = Validator.TryValidateObject(
            invalido,
            new ValidationContext(invalido),
            errores,
            validateAllProperties: true);

        Assert.False(esValido);
        Assert.Single(errores);

        var valido = new CategoriaFormModel { Nombre = "Ropa" };
        Assert.True(Validator.TryValidateObject(
            valido,
            new ValidationContext(valido),
            [],
            validateAllProperties: true));
    }

    [Fact]
    public async Task ListarCrearEditar_UsaICategoriaServiceExistente()
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new CategoriaService(test.Db);

        var invalido = await servicio.CrearAsync(new CategoriaInput(" ", null));
        var creado = await servicio.CrearAsync(new CategoriaInput("Ropa", "Prendas"));
        var editado = await servicio.EditarAsync(
            creado.Value!.Id,
            new CategoriaInput("Ropa y accesorios", "Catálogo general"));
        var listado = await servicio.ListarAsync();

        Assert.False(invalido.IsSuccess);
        Assert.True(creado.IsSuccess, creado.ErrorMessage);
        Assert.True(editado.IsSuccess, editado.ErrorMessage);
        Assert.Equal("Ropa y accesorios", editado.Value!.Nombre);
        Assert.Contains(listado, categoria => categoria.Id == creado.Value.Id);
    }
}

public sealed class ProductoModuloTests
{
    [Fact]
    public void Formulario_ValidaRequeridosPrecioYCategoriaReal()
    {
        var modelo = new ProductoFormModel();
        var errores = new List<ValidationResult>();

        var esValido = Validator.TryValidateObject(
            modelo,
            new ValidationContext(modelo),
            errores,
            validateAllProperties: true);

        Assert.False(esValido);
        Assert.Contains(errores, error => error.MemberNames.Contains(nameof(ProductoFormModel.CodigoInterno)));
        Assert.Contains(errores, error => error.MemberNames.Contains(nameof(ProductoFormModel.Nombre)));
        var referenciasInvalidas = new ProductoFormModel
        {
            CodigoInterno = "BLU-001",
            Nombre = "Blusa negra",
            PrecioSugerido = -1m,
        };
        var erroresReferencias = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(
            referenciasInvalidas,
            new ValidationContext(referenciasInvalidas),
            erroresReferencias,
            validateAllProperties: true));
        Assert.Contains(erroresReferencias, error => error.MemberNames.Contains(nameof(ProductoFormModel.PrecioSugerido)));
        Assert.Contains(erroresReferencias, error => error.MemberNames.Contains(nameof(ProductoFormModel.CategoriaId)));


        var valido = new ProductoFormModel
        {
            CodigoInterno = "BLU-001",
            Nombre = "Blusa negra",
            PrecioSugerido = 250m,
            CategoriaId = 1,
        };

        Assert.True(Validator.TryValidateObject(
            valido,
            new ValidationContext(valido),
            [],
            validateAllProperties: true));
    }

    [Fact]
    public void Presentacion_MuestraPrecioSugeridoConDosDecimales()
    {
        var producto = new ProductoDto(
            7,
            "BLU-001",
            null,
            "Blusa negra",
            null,
            "Nike",
            null,
            null,
            "M",
            250m,
            3,
            "Ropa");

        Assert.Equal("Q 250.00", ProductoPresentacion.PrecioSugerido(producto.PrecioSugerido));
        Assert.Equal("Nike · M", ProductoPresentacion.DatosBreves(producto));
    }

    [Fact]
    public async Task ListarBuscarCrearEditar_UsaIProductoServiceYCategoriaSeleccionada()
    {
        await using var test = await TestDatabase.CreateAsync();
        var categorias = new CategoriaService(test.Db);
        var productos = new ProductoService(test.Db);
        var categoriaNueva = await categorias.CrearAsync(new CategoriaInput("Calzado", null));

        var creado = await productos.CrearAsync(
            new ProductoInput(
                "TEN-001",
                "740000000001",
                "Tenis urbano",
                "Edición casual",
                "Marca Uno",
                "M-01",
                "Negro",
                "42",
                699.90m,
                test.Categoria.Id));
        var listado = await productos.ListarAsync();
        var encontrados = await productos.BuscarAsync("740000000001");
        var editado = await productos.EditarAsync(
            creado.Value!.Id,
            (creado.Value with
            {
                Nombre = "Tenis urbano actualizado",
                PrecioSugerido = 749.50m,
                CategoriaId = categoriaNueva.Value!.Id,
                Categoria = categoriaNueva.Value.Nombre,
            }).ToInput());

        Assert.True(creado.IsSuccess, creado.ErrorMessage);
        Assert.Contains(listado, producto => producto.Id == creado.Value.Id);
        Assert.Contains(encontrados, producto => producto.Id == creado.Value.Id);
        Assert.True(editado.IsSuccess, editado.ErrorMessage);
        Assert.Equal("Tenis urbano actualizado", editado.Value!.Nombre);
        Assert.Equal(749.50m, editado.Value.PrecioSugerido);
        Assert.Equal(categoriaNueva.Value.Id, editado.Value.CategoriaId);
        Assert.Equal("Calzado", editado.Value.Categoria);
    }

    [Fact]
    public async Task CrearProducto_SinCategoriaValida_EsRechazadoPorBackend()
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new ProductoService(test.Db);

        var resultado = await servicio.CrearAsync(
            new ProductoInput(
                "INVALIDO-001",
                null,
                "Producto inválido",
                null,
                null,
                null,
                null,
                null,
                10m,
                int.MaxValue));

        Assert.False(resultado.IsSuccess);
        Assert.Equal("Categoría no encontrada.", resultado.ErrorMessage);
    }

    [Fact]
    public void ModeloDeFormulario_NoIntroduceConceptosDeInventarioOCosto()
    {
        var nombres = typeof(ProductoFormModel)
            .GetProperties()
            .Select(propiedad => propiedad.Name)
            .ToArray();

        Assert.DoesNotContain(nombres, nombre => nombre.Contains("Inventario", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(nombres, nombre => nombre.Contains("Unidad", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(nombres, nombre => nombre.Contains("Stock", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(nombres, nombre => nombre.Contains("Costo", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(nombres, nombre => nombre.Contains("Margen", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(nombres, nombre => nombre.Contains("Utilidad", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class CatalogoModuloIntegracionTests : PruebaWebAislada
{
    [Theory]
    [InlineData("/categorias")]
    [InlineData("/categorias/nueva")]
    [InlineData("/categorias/3/editar")]
    [InlineData("/productos")]
    [InlineData("/productos/nuevo")]
    [InlineData("/productos/7")]
    [InlineData("/productos/7/editar")]
    public async Task RutasDeCatalogo_RequierenAutenticacion(string ruta)
    {
        using var cliente = CrearCliente(factory);

        var respuesta = await cliente.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/login", respuesta.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task PaginasAutenticadas_RenderizanListadosFormulariosCategoriaYDetalle()
    {
        var categoriaService = new CategoriaServiceFalso(
            [new CategoriaDto(3, "Ropa de prueba", "Categoría desde servicio")]);
        var productoService = new ProductoServiceFalso();
        using var aplicacion = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICategoriaService>();
                services.RemoveAll<IProductoService>();
                services.AddSingleton<ICategoriaService>(categoriaService);
                services.AddSingleton<IProductoService>(productoService);
            });
        });
        using var cliente = CrearCliente(aplicacion);
        await IniciarSesionAsync(cliente);

        var categorias = WebUtility.HtmlDecode(await cliente.GetStringAsync("/categorias"));
        var categoriaNueva = WebUtility.HtmlDecode(await cliente.GetStringAsync("/categorias/nueva"));
        var categoriaEditar = WebUtility.HtmlDecode(await cliente.GetStringAsync("/categorias/3/editar"));
        var productos = WebUtility.HtmlDecode(await cliente.GetStringAsync("/productos"));
        var productoNuevo = WebUtility.HtmlDecode(await cliente.GetStringAsync("/productos/nuevo"));
        var productoEditar = WebUtility.HtmlDecode(await cliente.GetStringAsync("/productos/7/editar"));
        var detalle = WebUtility.HtmlDecode(await cliente.GetStringAsync("/productos/7"));

        Assert.Contains("Ropa de prueba", categorias);
        Assert.Contains("Nombre", categoriaNueva);
        Assert.Contains("Categoría desde servicio", categoriaEditar);
        Assert.Contains("BLU-007", productos);
        Assert.Contains("Blusa Nike negra", productos);
        Assert.Contains("Precio sugerido", productos);
        Assert.Contains("Q 250.00", productos);
        Assert.Contains("Ropa de prueba", productoNuevo);
        Assert.Contains("Blusa Nike negra", productoEditar);
        Assert.Contains("Código de barras", detalle);
        Assert.Contains("740000000007", detalle);
        Assert.Contains("Sin información", detalle);
        Assert.DoesNotContain(">Costo<", detalle, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Stock<", detalle, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Utilidad<", detalle, StringComparison.OrdinalIgnoreCase);
        Assert.True(categoriaService.ListarFueInvocado);
        Assert.True(categoriaService.ObtenerFueInvocado);
        Assert.True(productoService.ListarFueInvocado);
        Assert.True(productoService.ObtenerFueInvocado);
    }

    [Fact]
    public async Task ProductoNuevo_SinCategorias_ExplicaBloqueoYEnlazaACategorias()
    {
        using var aplicacion = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICategoriaService>();
                services.AddSingleton<ICategoriaService>(new CategoriaServiceFalso([]));
            });
        });
        using var cliente = CrearCliente(aplicacion);
        await IniciarSesionAsync(cliente);

        var contenido = WebUtility.HtmlDecode(await cliente.GetStringAsync("/productos/nuevo"));

        Assert.Contains("No hay categorías disponibles", contenido);
        Assert.Contains("Ir a categorías", contenido);
        Assert.Contains("disabled", contenido);
    }

    private static HttpClient CrearCliente(WebApplicationFactory<Program> aplicacion) =>
        aplicacion.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

    private static async Task IniciarSesionAsync(HttpClient cliente)
    {
        var paginaLogin = await cliente.GetAsync("/login");
        var token = await ObtenerTokenAntiforgeryAsync(paginaLogin);
        using var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["correo"] = AplicacionAutenticacionFactory.CorreoUsuario,
            ["contrasena"] = AplicacionAutenticacionFactory.ContrasenaValida,
            ["__RequestVerificationToken"] = token,
        });

        var respuesta = await cliente.PostAsync("/account/login", formulario);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
    }

    private static async Task<string> ObtenerTokenAntiforgeryAsync(HttpResponseMessage respuesta)
    {
        respuesta.EnsureSuccessStatusCode();
        var contenido = await respuesta.Content.ReadAsStringAsync();
        var etiqueta = Regex.Match(
            contenido,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>",
            RegexOptions.IgnoreCase);
        var valor = Regex.Match(etiqueta.Value, "value=\"([^\"]+)\"", RegexOptions.IgnoreCase);

        Assert.True(valor.Success);
        return WebUtility.HtmlDecode(valor.Groups[1].Value);
    }

    private sealed class CategoriaServiceFalso(IReadOnlyList<CategoriaDto> categorias)
        : ICategoriaService
    {
        public bool ListarFueInvocado { get; private set; }
        public bool ObtenerFueInvocado { get; private set; }

        public Task<ServiceResult<CategoriaDto>> CrearAsync(
            CategoriaInput input,
            CancellationToken ct = default) =>
            Task.FromResult(ServiceResult<CategoriaDto>.Failure("No disponible en esta prueba."));

        public Task<ServiceResult<CategoriaDto>> EditarAsync(
            int id,
            CategoriaInput input,
            CancellationToken ct = default) =>
            Task.FromResult(ServiceResult<CategoriaDto>.Failure("No disponible en esta prueba."));

        public Task<ServiceResult<CategoriaDto>> ObtenerPorIdAsync(
            int id,
            CancellationToken ct = default)
        {
            ObtenerFueInvocado = true;
            var categoria = categorias.FirstOrDefault(item => item.Id == id);
            return Task.FromResult(
                categoria is null
                    ? ServiceResult<CategoriaDto>.Failure("Categoría no encontrada.")
                    : ServiceResult<CategoriaDto>.Ok(categoria));
        }

        public Task<IReadOnlyList<CategoriaDto>> ListarAsync(CancellationToken ct = default)
        {
            ListarFueInvocado = true;
            return Task.FromResult(categorias);
        }
    }

    private sealed class ProductoServiceFalso : IProductoService
    {
        private readonly ProductoDto producto =
            new(
                7,
                "BLU-007",
                "740000000007",
                "Blusa Nike negra",
                null,
                "Nike",
                "Classic",
                "Negro",
                "M",
                250m,
                3,
                "Ropa de prueba");

        public bool ListarFueInvocado { get; private set; }
        public bool ObtenerFueInvocado { get; private set; }

        public Task<ServiceResult<ProductoDto>> CrearAsync(
            ProductoInput input,
            CancellationToken ct = default) =>
            Task.FromResult(ServiceResult<ProductoDto>.Failure("No disponible en esta prueba."));

        public Task<ServiceResult<ProductoDto>> EditarAsync(
            int id,
            ProductoInput input,
            CancellationToken ct = default) =>
            Task.FromResult(ServiceResult<ProductoDto>.Failure("No disponible en esta prueba."));

        public Task<ServiceResult<ProductoDto>> ObtenerPorIdAsync(
            int id,
            CancellationToken ct = default)
        {
            ObtenerFueInvocado = true;
            return Task.FromResult(
                id == producto.Id
                    ? ServiceResult<ProductoDto>.Ok(producto)
                    : ServiceResult<ProductoDto>.Failure("Producto no encontrado."));
        }

        public Task<IReadOnlyList<ProductoDto>> ListarAsync(CancellationToken ct = default)
        {
            ListarFueInvocado = true;
            return Task.FromResult<IReadOnlyList<ProductoDto>>([producto]);
        }

        public Task<IReadOnlyList<ProductoDto>> BuscarAsync(
            string termino,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProductoDto>>([producto]);
    }
}

internal static class ProductoDtoTestExtensions
{
    public static ProductoInput ToInput(this ProductoDto producto) =>
        new(
            producto.CodigoInterno,
            producto.CodigoBarras,
            producto.Nombre,
            producto.Descripcion,
            producto.Marca,
            producto.Modelo,
            producto.Color,
            producto.Talla,
            producto.PrecioSugerido,
            producto.CategoriaId);
}
