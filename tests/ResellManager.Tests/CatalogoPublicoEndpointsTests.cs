using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
using SkiaSharp;

namespace ResellManager.Tests;

[Collection("Integración web")]
public sealed class CatalogoPublicoEndpointsTests : PruebaWebAislada
{
    [Fact]
    public async Task ListadoYDetalle_SonAnonimosYExponenSoloInformacionComercial()
    {
        var escenario = await CrearProductoAsync();
        await CrearProductoAsync(disponible: false);
        using var cliente = CrearCliente();
        using var listado = await cliente.GetAsync(
            $"/api/catalogo/productos?termino=PERFUME&categoriaId={escenario.CategoriaId}");
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        Assert.True(listado.Headers.CacheControl!.NoStore);
        Assert.Equal("application/json", listado.Content.Headers.ContentType!.MediaType);
        using var jsonListado = JsonDocument.Parse(await listado.Content.ReadAsStringAsync());
        var producto = Assert.Single(jsonListado.RootElement.EnumerateArray());
        Assert.Equal(escenario.ProductoId, producto.GetProperty("id").GetInt32());
        Assert.Equal(123.45m, producto.GetProperty("precioPublico").GetDecimal());
        Assert.True(producto.GetProperty("disponible").GetBoolean());
        Assert.True(producto.GetProperty("tieneImagenPrincipal").GetBoolean());
        AssertCampos(producto, "id", "nombre", "categoriaId", "categoria", "precioPublico",
            "tieneImagenPrincipal", "disponible");

        using var detalle = await cliente.GetAsync($"/api/catalogo/productos/{escenario.ProductoId}");
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);
        Assert.True(detalle.Headers.CacheControl!.NoStore);
        var contenido = await detalle.Content.ReadAsStringAsync();
        using var jsonDetalle = JsonDocument.Parse(contenido);
        AssertCampos(jsonDetalle.RootElement, "id", "nombre", "descripcion", "marca", "modelo",
            "color", "talla", "contenidoMl", "pesoGramos", "presentacion", "categoriaId",
            "categoria", "precioPublico", "tieneImagenPrincipal", "disponible");
        foreach (var datoPrivado in new[] { "PRIVADO", "7501234567890", "PRO-", "COM-",
            "74.12", escenario.RutaImagen!, factory.RutaImagenes, factory.RutaBaseDatos })
            Assert.DoesNotContain(datoPrivado, contenido);
    }

    [Theory]
    [InlineData("sinStock")]
    [InlineData("reservada")]
    [InlineData("vendida")]
    public async Task SinDisponibilidad_ListadoDetalleEImagenOcultanProducto(string condicion)
    {
        var escenario = await CrearProductoAsync(disponible: condicion != "sinStock");
        if (condicion != "sinStock") await OcuparUnidadAsync(escenario, condicion == "reservada");
        using var cliente = CrearCliente();
        using var listado = await cliente.GetAsync("/api/catalogo/productos");
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        using var json = JsonDocument.Parse(await listado.Content.ReadAsStringAsync());
        Assert.Empty(json.RootElement.EnumerateArray());

        foreach (var ruta in new[] { $"/api/catalogo/productos/{escenario.ProductoId}",
            $"/api/catalogo/productos/{escenario.ProductoId}/imagen" })
        {
            using var respuesta = await cliente.GetAsync(ruta);
            await AssertNoEncontradoAsync(respuesta);
        }
    }

    [Theory]
    [InlineData("/api/catalogo/productos/2147483647")]
    [InlineData("/api/catalogo/productos/2147483647/imagen")]
    public async Task Inexistente_Devuelve404SinRedireccionNiPaginaAdministrativa(string ruta)
    {
        using var cliente = CrearCliente();
        using var respuesta = await cliente.GetAsync(ruta);
        await AssertNoEncontradoAsync(respuesta);
    }

    [Fact]
    public async Task ImagenPublica_SirveWebpProcesadoYEndpointAdministrativoConservaAutenticacion()
    {
        var escenario = await CrearProductoAsync();
        using var cliente = CrearCliente();
        using var respuesta = await cliente.GetAsync($"/api/catalogo/productos/{escenario.ProductoId}/imagen");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("image/webp", respuesta.Content.Headers.ContentType!.MediaType);
        Assert.True(respuesta.Headers.CacheControl!.NoStore);
        Assert.Equal("nosniff", Assert.Single(respuesta.Headers.GetValues("X-Content-Type-Options")));
        using var stream = new MemoryStream(await respuesta.Content.ReadAsByteArrayAsync());
        using var codec = SKCodec.Create(stream);
        Assert.NotNull(codec);
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);

        using var administrativa = await cliente.GetAsync($"/productos/{escenario.ProductoId}/imagen");
        Assert.Equal(HttpStatusCode.Redirect, administrativa.StatusCode);
        Assert.Equal("/login", administrativa.Headers.Location!.AbsolutePath);
    }

    [Theory]
    [InlineData("sinImagen")]
    [InlineData("archivoAusente")]
    [InlineData("traversal")]
    [InlineData("rutaFisica")]
    [InlineData("otroProducto")]
    public async Task ImagenNoAccesible_Devuelve404SinRevelarRutas(string condicion)
    {
        var escenario = await CrearProductoAsync(imagen: condicion != "sinImagen");
        if (condicion != "sinImagen")
        {
            var ruta = condicion switch
            {
                "archivoAusente" => $"productos/{escenario.ProductoId}/imagen-principal-{Guid.NewGuid():N}.webp",
                "traversal" => $"productos/{escenario.ProductoId}/../../secreto.webp",
                "rutaFisica" => Path.Combine(factory.RutaImagenes, "secreto.webp"),
                "otroProducto" => (await CrearProductoAsync(disponible: false)).RutaImagen,
                _ => throw new InvalidOperationException()
            };
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
            (await db.Productos.FindAsync(escenario.ProductoId))!.ImagenPrincipalRuta = ruta;
            await db.SaveChangesAsync();
        }
        using var cliente = CrearCliente();
        using var respuesta = await cliente.GetAsync($"/api/catalogo/productos/{escenario.ProductoId}/imagen");
        await AssertNoEncontradoAsync(respuesta);
    }

    [Fact]
    public void EndpointsPublicos_PermitenSoloGetYDeclaranAccesoAnonimo()
    {
        using var cliente = CrearCliente();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(x => x.RoutePattern.RawText!.StartsWith("/api/catalogo/productos", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(3, endpoints.Length);
        Assert.All(endpoints, endpoint =>
        {
            Assert.NotNull(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
            Assert.Equal("GET", Assert.Single(endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods));
        });
        var administrativa = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>().Single(x => x.RoutePattern.RawText == "/productos/{productoId:int}/imagen");
        Assert.NotNull(administrativa.Metadata.GetMetadata<IAuthorizeData>());
        Assert.Null(administrativa.Metadata.GetMetadata<IAllowAnonymous>());
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task MetodosDeEscritura_NoEstanExpuestos(string metodo)
    {
        var escenario = await CrearProductoAsync(imagen: false);
        using var cliente = CrearCliente();
        foreach (var ruta in new[] { "/api/catalogo/productos",
            $"/api/catalogo/productos/{escenario.ProductoId}",
            $"/api/catalogo/productos/{escenario.ProductoId}/imagen" })
        {
            using var solicitud = new HttpRequestMessage(new HttpMethod(metodo), ruta);
            using var respuesta = await cliente.SendAsync(solicitud);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, respuesta.StatusCode);
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
        Assert.Single(await db.UnidadesInventario.ToListAsync());
        Assert.Equal(EstadoUnidadInventario.Disponible, (await db.UnidadesInventario.SingleAsync()).Estado);
        Assert.Empty(await db.Pedidos.ToListAsync());
        Assert.Empty(await db.Ventas.ToListAsync());
    }

    private HttpClient CrearCliente() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    });

    private static async Task AssertNoEncontradoAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Null(respuesta.Headers.Location);
        Assert.True(respuesta.Headers.CacheControl!.NoStore);
        Assert.Equal(string.Empty, await respuesta.Content.ReadAsStringAsync());
    }

    private static void AssertCampos(JsonElement producto, params string[] permitidos) =>
        Assert.Equal(permitidos.OrderBy(x => x), producto.EnumerateObject().Select(x => x.Name).OrderBy(x => x));

    private async Task<Escenario> CrearProductoAsync(bool disponible = true, bool imagen = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
        var categoria = new Categoria { Nombre = "Perfumes", Observaciones = "NOTA-CATEGORIA-PRIVADO" };
        var proveedor = new Proveedor { Nombre = "PROVEEDOR-PRIVADO" };
        var cliente = new Cliente { Nombres = "CLIENTE-PRIVADO", Telefono = "CONTACTO-PRIVADO" };
        db.AddRange(categoria, proveedor, cliente);
        await db.SaveChangesAsync();
        var input = new ProductoInput("7501234567890", "Perfume público", "Fragancia floral", "Marca",
            "Modelo", null, null, 123.45m, categoria.Id, ContenidoMl: 100m, Presentacion: "Frasco");
        ProductoDto producto;
        if (imagen)
        {
            using var bitmap = new SKBitmap(16, 16);
            bitmap.Erase(SKColors.Blue);
            using var image = SKImage.FromBitmap(bitmap);
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            using var contenido = new MemoryStream(png.ToArray());
            var resultado = await scope.ServiceProvider.GetRequiredService<IProductoConImagenService>()
                .CrearAsync(input, contenido);
            Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
            producto = resultado.Value!;
        }
        else
        {
            var resultado = await scope.ServiceProvider.GetRequiredService<IProductoService>().CrearAsync(input);
            Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
            producto = resultado.Value!;
        }
        int? unidadId = null;
        if (disponible)
        {
            var compra = await scope.ServiceProvider.GetRequiredService<ICompraService>().RegistrarAsync(
                new CompraInput($"COM-{Guid.NewGuid():N}", new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 1),
                    OrigenCompra.CompraLocal, proveedor.Id, "NOTA-COMPRA-PRIVADO",
                    [new DetalleCompraInput(producto.Id, 1, 74.12m)], null));
            Assert.True(compra.IsSuccess, compra.ErrorMessage);
            unidadId = await db.UnidadesInventario.Where(x => x.ProductoId == producto.Id).Select(x => x.Id).SingleAsync();
        }
        return new(producto.Id, categoria.Id, cliente.Id, unidadId, producto.ImagenPrincipalRuta);
    }

    private async Task OcuparUnidadAsync(Escenario escenario, bool reservar)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var pedido = await scope.ServiceProvider.GetRequiredService<IPedidoService>().CrearAsync(new PedidoInput(
            $"PED-{Guid.NewGuid():N}", new DateOnly(2026, 2, 2), TipoPedido.Apartado, CanalVenta.Otro,
            escenario.ClienteId, null, [new DetallePedidoInput(escenario.ProductoId, 1, 123.45m, null)]));
        Assert.True(pedido.IsSuccess, pedido.ErrorMessage);
        if (reservar)
        {
            var reserva = await scope.ServiceProvider.GetRequiredService<IInventarioService>().ReservarAsync(
                escenario.UnidadId!.Value, pedido.Value!.Detalles.Single().Id);
            Assert.True(reserva.IsSuccess, reserva.ErrorMessage);
        }
        else
        {
            var venta = await scope.ServiceProvider.GetRequiredService<IVentaService>().RegistrarDesdePedidoAsync(
                new VentaInput(pedido.Value!.Id, $"VEN-{Guid.NewGuid():N}", new DateOnly(2026, 2, 2), null,
                    [new DetalleVentaInput(escenario.UnidadId!.Value, null, null, 99m, null)]));
            Assert.True(venta.IsSuccess, venta.ErrorMessage);
        }
    }

    private sealed record Escenario(int ProductoId, int CategoriaId, int ClienteId, int? UnidadId, string? RutaImagen);
}
