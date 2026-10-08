using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
using SkiaSharp;

namespace ResellManager.Tests;

[Collection("Integración web")]
public sealed class ProductoGaleriaEndpointsTests : PruebaWebAislada
{
    [Fact]
    public async Task GaleriaAdministrativa_RequiereAutenticacionEnMetadatosYArchivos()
    {
        using var cliente = CrearCliente();
        foreach (var ruta in new[] { "/productos/1/imagenes", $"/productos/1/imagenes/{Guid.NewGuid():D}" })
        {
            using var respuesta = await cliente.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
            Assert.Equal("/login", respuesta.Headers.Location!.AbsolutePath);
        }
    }

    [Fact]
    public async Task CambiarPortada_ActualizaEndpointsAntiguosYConservaGaleriaOrdenada()
    {
        ProductoInput input;
        ProductoDto producto;
        IReadOnlyList<ImagenProductoDto> imagenes;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
            var categoria = new Categoria { Nombre = "Galería" };
            var proveedor = new Proveedor { Nombre = "Proveedor ficticio" };
            db.AddRange(categoria, proveedor);
            await db.SaveChangesAsync();
            input = new(null, "Producto con reverso", null, null, null, null, null, 10m, categoria.Id);
            using var primera = Imagen(SKColors.Blue);
            using var segunda = Imagen(SKColors.Red);
            var servicio = scope.ServiceProvider.GetRequiredService<IProductoConImagenService>();
            var creado = await servicio.CrearGaleriaAsync(input, [primera, segunda]);
            Assert.True(creado.IsSuccess, creado.ErrorMessage);
            producto = creado.Value!;
            imagenes = (await servicio.ObtenerGaleriaAsync(producto.Id)).Value!;
            var compra = await scope.ServiceProvider.GetRequiredService<ICompraService>().RegistrarAsync(
                new($"COM-{Guid.NewGuid():N}", new(2026, 10, 8), new(2026, 10, 8), OrigenCompra.CompraLocal,
                    proveedor.Id, null, [new(producto.Id, 1, 5m)], null));
            Assert.True(compra.IsSuccess, compra.ErrorMessage);
        }
        using var cliente = CrearCliente();
        using var inicial = await cliente.GetAsync($"/api/catalogo/productos/{producto.Id}/imagen");
        var portadaAnterior = await inicial.Content.ReadAsByteArrayAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var editado = await scope.ServiceProvider.GetRequiredService<IProductoConImagenService>()
                .EditarGaleriaAsync(producto.Id, input, new(imagenes.Select(x => new ImagenProductoEdicion(x.Id)).ToList(), 1), []);
            Assert.True(editado.IsSuccess, editado.ErrorMessage);
        }
        using var portada = await cliente.GetAsync($"/api/catalogo/productos/{producto.Id}/imagen");
        using var opaca = await cliente.GetAsync($"/api/catalogo/productos/{producto.Id}/imagenes/{imagenes[1].Id:D}");
        Assert.Equal(HttpStatusCode.OK, portada.StatusCode);
        var portadaNueva = await portada.Content.ReadAsByteArrayAsync();
        Assert.Equal(portadaNueva, await opaca.Content.ReadAsByteArrayAsync());
        Assert.NotEqual(portadaAnterior, portadaNueva);
        await IniciarSesionAsync(cliente);
        using var antiguaAdmin = await cliente.GetAsync($"/productos/{producto.Id}/imagen");
        using var nuevaAdmin = await cliente.GetAsync($"/productos/{producto.Id}/imagenes/{imagenes[1].Id:D}");
        using var metadatos = await cliente.GetAsync($"/productos/{producto.Id}/imagenes");
        Assert.Equal(HttpStatusCode.OK, antiguaAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, nuevaAdmin.StatusCode);
        Assert.Equal(portadaNueva, await antiguaAdmin.Content.ReadAsByteArrayAsync());
        Assert.Equal(portadaNueva, await nuevaAdmin.Content.ReadAsByteArrayAsync());
        using var json = JsonDocument.Parse(await metadatos.Content.ReadAsStringAsync());
        var fotos = json.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, fotos.Length);
        Assert.False(fotos[0].GetProperty("esPortada").GetBoolean());
        Assert.True(fotos[1].GetProperty("esPortada").GetBoolean());
        Assert.All(fotos, x => Assert.Equal(new[] { "esPortada", "id", "orden" }, x.EnumerateObject().Select(x => x.Name).Order().ToArray()));
    }

    private HttpClient CrearCliente() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost")
    });

    private static async Task IniciarSesionAsync(HttpClient cliente)
    {
        using var pagina = await cliente.GetAsync("/login");
        var contenido = await pagina.Content.ReadAsStringAsync();
        var etiqueta = Regex.Match(contenido, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase);
        var token = Regex.Match(etiqueta.Value, "value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        Assert.True(token.Success);
        using var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["correo"] = AplicacionAutenticacionFactory.CorreoUsuario,
            ["contrasena"] = AplicacionAutenticacionFactory.ContrasenaValida,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value)
        });
        using var respuesta = await cliente.PostAsync("/account/login", formulario);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
    }

    private static MemoryStream Imagen(SKColor color)
    {
        using var bitmap = new SKBitmap(16, 24);
        bitmap.Erase(color);
        using var imagen = SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(SKEncodedImageFormat.Png, 100);
        return new MemoryStream(datos.ToArray());
    }
}
