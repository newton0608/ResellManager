using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Application.Services;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Lookup;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Storage;
using ResellManager.Web.Components.Catalogo;
using ResellManager.Web.Components.Pages;
using ResellManager.Web.Components.Productos;
using SkiaSharp;
using static ResellManager.Tests.ProductoLookupProvidersTests;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

[Collection("Integración web")]
public sealed class FlujoImagenExternaTests
{
    [Theory]
    [InlineData("Open Facts", false)]
    [InlineData("Open Facts", true)]
    [InlineData("UPCitemdb", true)]
    public async Task AceptarYGuardar_DescargadorReal_PersisteYSirveImagenAdministrativaYPublica(string fuente, bool redireccion)
    {
        const string origen = "https://world.openfoodfacts.org/images/products/prueba.jpg";
        const string destino = "https://images.openfoodfacts.org/images/products/prueba.jpg";
        var imagen = Png();
        using var imagenHttp = new Handler(request => redireccion && request.RequestUri!.AbsoluteUri == origen
            ? ImagenExternaRedireccionesTests.Redireccion(301, destino)
            : new(HttpStatusCode.OK) { Content = new ByteArrayContent(imagen) { Headers = { ContentType = new("image/png") } } });
        using var baseFactory = new AplicacionAutenticacionFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddHttpClient<IImagenProductoExternaService, ImagenProductoExternaService>()
                .ConfigurePrimaryHttpMessageHandler(() => imagenHttp)));
        using var administrativa = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        using var anonima = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });

        int id;
        string ruta;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var servicios = scope.ServiceProvider;
            var db = servicios.GetRequiredService<ResellManagerDbContext>();
            var categoria = new Categoria { Nombre = "Categoría de prueba" };
            db.Categorias.Add(categoria);
            await db.SaveChangesAsync();
            var page = new ProductoEdicion();
            Set(page, "ProductoService", servicios.GetRequiredService<IProductoService>());
            Set(page, "ProductoConImagenService", servicios.GetRequiredService<IProductoConImagenService>());
            Set(page, "AltaProductoAsistidaService", servicios.GetRequiredService<IAltaProductoAsistidaService>());
            Set(page, "CategoriaService", servicios.GetRequiredService<ICategoriaService>());
            Set(page, "Logger", NullLogger<ProductoEdicion>.Instance);
            var navegacion = new Navegacion();
            Set(page, "Navigation", navegacion);
            await CallAsync(page, "CargarAsync");
            var modelo = Get<ProductoFormModel>(page, "Modelo");
            modelo.Restaurar(ProductoLookupTests.ModeloManual());
            modelo.CategoriaId = categoria.Id;
            using var providerHttp = new Handler(_ => Json(fuente == "Open Facts"
                ? """{"status":"success","product":{"code":"0123456789012","product_name":"Producto importado de prueba","image_front_url":"https://world.openfoodfacts.org/images/products/prueba.jpg"}}"""
                : """{"code":"OK","items":[{"ean":"0123456789012","title":"Producto importado de prueba","images":["https://world.openfoodfacts.org/images/products/prueba.jpg"]}]}"""));
            using var providerClient = new HttpClient(providerHttp);
            var opciones = Options.Create(new ProductoLookupOptions());
            var limites = new ProductoLookupLimites(opciones, new Reloj());
            IProductoLookupProvider provider = fuente == "Open Facts"
                ? new OpenFactsProductoLookupProvider(providerClient, opciones, limites, NullLogger<OpenFactsProductoLookupProvider>.Instance)
                : new UpcitemdbProductoLookupProvider(providerClient, opciones, limites, NullLogger<UpcitemdbProductoLookupProvider>.Instance);
            var ronda = await new ProductoLookupService(servicios.GetRequiredService<IConsultaProductoCodigoBarras>(), [provider])
                .IniciarAsync(modelo.CodigoBarras!);
            Assert.NotNull(ronda.Candidato);
            new ProductoLookupImportacion().Aplicar(modelo, ronda.Candidato);
            Assert.Equal(origen, modelo.ImagenExternaUrl);
            Assert.Empty(imagenHttp.Peticiones);
            Assert.Empty(await db.Productos.ToListAsync());

            await CallAsync(page, "GuardarAsync", modelo);
            Assert.Null(Get<string?>(page, "ErrorGuardado"));
            Assert.EndsWith("mensaje=producto-creado", navegacion.Uri);
            var persistido = Assert.Single(await db.Productos.AsNoTracking().ToListAsync());
            id = persistido.Id;
            ruta = Assert.IsType<string>(persistido.ImagenPrincipalRuta);
            Assert.StartsWith($"productos/{id}/imagen-principal-", ruta);
            Assert.EndsWith(".webp", ruta);
            Assert.DoesNotContain("https", ruta);
            Assert.Equal(redireccion ? 2 : 1, imagenHttp.Peticiones.Count);
            if (redireccion) Assert.Equal(destino, imagenHttp.Peticiones[1].Url.AbsoluteUri);
            var carpeta = servicios.GetRequiredService<IOptions<AlmacenamientoImagenesProductoOptions>>().Value.DirectorioBase;
            var partes = ruta.Split('/');
            var archivo = Path.Combine(carpeta, partes[1], partes[2]);
            Assert.True(File.Exists(archivo), archivo);
            Assert.Single(Directory.GetFiles(carpeta, "*", SearchOption.AllDirectories));
        }

        await IniciarSesionAsync(administrativa);
        using var privada = await administrativa.GetAsync($"/productos/{id}/imagen");
        var bytesPrivados = await AssertWebpAsync(privada);
        var detalle = WebUtility.HtmlDecode(await administrativa.GetStringAsync($"/productos/{id}"));
        Assert.Contains($"src=\"/productos/{id}/imagen\"", detalle);
        Assert.DoesNotContain("Sin imagen principal", detalle);
        var listado = await administrativa.GetStringAsync("/productos");
        Assert.Contains($"src=\"/productos/{id}/imagen\"", listado);
        using var accesoPrivadoAnonimo = await anonima.GetAsync($"/productos/{id}/imagen");
        Assert.Equal(HttpStatusCode.Redirect, accesoPrivadoAnonimo.StatusCode);

        // Tener imagen no publica un producto sin unidades comercialmente disponibles.
        Assert.Empty((await anonima.GetFromJsonAsync<ProductoCatalogoDto[]>("/api/catalogo/productos"))!);
        foreach (var url in new[] { $"/api/catalogo/productos/{id}", $"/api/catalogo/productos/{id}/imagen" })
        {
            using var oculta = await anonima.GetAsync(url);
            Assert.Equal(HttpStatusCode.NotFound, oculta.StatusCode);
        }
        await AgregarUnidadDisponibleAsync(factory, id);

        var publico = await anonima.GetFromJsonAsync<ProductoCatalogoDetalleDto>($"/api/catalogo/productos/{id}");
        Assert.NotNull(publico);
        Assert.True(publico.TieneImagenPrincipal);
        Assert.True(publico.Disponible);
        Assert.True(Assert.Single((await anonima.GetFromJsonAsync<ProductoCatalogoDto[]>("/api/catalogo/productos"))!).TieneImagenPrincipal);
        using var respuestaPublica = await anonima.GetAsync($"/api/catalogo/productos/{id}/imagen");
        Assert.Equal(bytesPrivados, await AssertWebpAsync(respuestaPublica));
        Assert.True(respuestaPublica.Headers.CacheControl!.NoStore);
        Assert.Equal("nosniff", Assert.Single(respuestaPublica.Headers.GetValues("X-Content-Type-Options")));

        await using var scopeVista = factory.Services.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(scopeVista.ServiceProvider, scopeVista.ServiceProvider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var resultado = await renderer.RenderComponentAsync<ImagenCatalogo>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["ProductoId"] = publico.Id, ["Nombre"] = publico.Nombre,
                ["TieneImagen"] = publico.TieneImagenPrincipal, ["Prioritaria"] = true
            }));
            return resultado.ToHtmlString();
        });
        Assert.Contains($"src=\"/api/catalogo/productos/{id}/imagen\"", html);
        Assert.DoesNotContain(origen, html);
    }

    [Fact]
    public async Task RedireccionPrivada_GuardaConAvisoSinRutaNiArchivosYNoAccedeAlDestino()
    {
        using var imagenHttp = new Handler(_ => ImagenExternaRedireccionesTests.Redireccion(302, "https://127.0.0.1/secreto"));
        using var baseFactory = new AplicacionAutenticacionFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddHttpClient<IImagenProductoExternaService, ImagenProductoExternaService>()
                .ConfigurePrimaryHttpMessageHandler(() => imagenHttp)));
        using var cliente = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
        var categoria = new Categoria { Nombre = "Prueba" };
        db.Categorias.Add(categoria);
        await db.SaveChangesAsync();
        var resultado = await scope.ServiceProvider.GetRequiredService<IAltaProductoAsistidaService>().CrearAsistidoAsync(
            new("CODIGO-PRUEBA", "Producto sin imagen", null, null, null, null, null, 50, categoria.Id),
            null, "https://images.openfoodfacts.org/prueba.png");
        Assert.True(resultado.Producto.IsSuccess, resultado.Producto.ErrorMessage);
        Assert.NotNull(resultado.AvisoImagen);
        Assert.Null(Assert.Single(await db.Productos.AsNoTracking().ToListAsync()).ImagenPrincipalRuta);
        Assert.Single(imagenHttp.Peticiones);
        var carpeta = scope.ServiceProvider.GetRequiredService<IOptions<AlmacenamientoImagenesProductoOptions>>().Value.DirectorioBase;
        Assert.False(Directory.Exists(carpeta));
    }

    private static async Task AgregarUnidadDisponibleAsync(WebApplicationFactory<Program> factory, int id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
        var proveedor = new Proveedor { Nombre = "Proveedor de prueba" };
        db.Proveedores.Add(proveedor);
        await db.SaveChangesAsync();
        var compra = await scope.ServiceProvider.GetRequiredService<ICompraService>().RegistrarAsync(new(
            $"COM-{Guid.NewGuid():N}", new(2026, 10, 5), new(2026, 10, 5), OrigenCompra.CompraLocal,
            proveedor.Id, null, [new DetalleCompraInput(id, 1, 25)], null));
        Assert.True(compra.IsSuccess, compra.ErrorMessage);
        Assert.Equal(EstadoUnidadInventario.Disponible, (await db.UnidadesInventario.SingleAsync(x => x.ProductoId == id)).Estado);
    }

    private static byte[] Png()
    {
        using var bitmap = new SKBitmap(32, 32);
        bitmap.Erase(SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    private static async Task<byte[]> AssertWebpAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType!.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var stream = new MemoryStream(bytes);
        using var codec = SKCodec.Create(stream);
        Assert.NotNull(codec);
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        return bytes;
    }

    private static async Task IniciarSesionAsync(HttpClient cliente)
    {
        var html = await cliente.GetStringAsync("/login");
        var input = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>");
        var token = WebUtility.HtmlDecode(Regex.Match(input.Value, "value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(token);
        using var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["correo"] = AplicacionAutenticacionFactory.CorreoUsuario,
            ["contrasena"] = AplicacionAutenticacionFactory.ContrasenaValida,
            ["__RequestVerificationToken"] = token
        });
        using var response = await cliente.PostAsync("/account/login", formulario);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
