using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;
using SkiaSharp;

namespace ResellManager.Tests;

[Collection("Integración web")]
public sealed class ProductoGaleriaEndpointsTests : PruebaWebAislada
{
    [Fact]
    public async Task GaleriaAdministrativa_RequiereAutenticacionEnMetadatosYArchivos()
    {
        using var cliente = CrearCliente();
        foreach (var ruta in new[] { "/productos/1/imagen", "/productos/1/miniatura?v=falsa", "/productos/1/imagenes", $"/productos/1/imagenes/{Guid.NewGuid():D}" })
        {
            using var respuesta = await cliente.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
            Assert.Equal("/login", respuesta.Headers.Location!.AbsolutePath);
        }
    }

    [Fact]
    public async Task MiniaturaYCabeceras_ConservanAutenticacionVersionYRevalidacion_SinCachearErrores()
    {
        ProductoDto producto;
        ProductoInput input;
        IReadOnlyList<ImagenProductoDto> fotos;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
            var categoria = new Categoria { Nombre = "Miniaturas QA" };
            db.Add(categoria);
            await db.SaveChangesAsync();
            input = new(null, "Producto QA", null, null, null, null, null, 10m, categoria.Id);
            using var azul = Imagen(SKColors.Blue, 800, 1200);
            using var roja = Imagen(SKColors.Red, 800, 1200);
            var servicio = scope.ServiceProvider.GetRequiredService<IProductoConImagenService>();
            producto = (await servicio.CrearGaleriaAsync(input, [azul, roja])).Value!;
            fotos = (await servicio.ObtenerGaleriaAsync(producto.Id)).Value!;
        }
        using var cliente = CrearCliente();
        await IniciarSesionAsync(cliente);
        var version = ImagenProductoVersion.Obtener(producto.ImagenPrincipalRuta!);
        var url = $"/productos/{producto.Id}/miniatura?v={version}";
        using var miniatura = await cliente.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, miniatura.StatusCode);
        Assert.True(miniatura.Headers.CacheControl!.Private);
        Assert.False(miniatura.Headers.CacheControl.NoStore);
        Assert.Equal(TimeSpan.FromDays(1), miniatura.Headers.CacheControl.MaxAge);
        Assert.Equal("image/webp", miniatura.Content.Headers.ContentType!.MediaType);
        var bytes = await miniatura.Content.ReadAsByteArrayAsync();
        using (var bitmap = SKBitmap.Decode(bytes))
        {
            Assert.Equal(160, bitmap.Width);
            Assert.Equal(240, bitmap.Height);
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.IfNoneMatch.Add(miniatura.Headers.ETag!);
        using var sinCambios = await cliente.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotModified, sinCambios.StatusCode);
        Assert.Empty(await sinCambios.Content.ReadAsByteArrayAsync());
        using var anonimo = CrearCliente();
        using var solicitudAnonima = new HttpRequestMessage(HttpMethod.Get, url);
        solicitudAnonima.Headers.IfNoneMatch.Add(miniatura.Headers.ETag!);
        using var denegada = await anonimo.SendAsync(solicitudAnonima);
        Assert.Equal(HttpStatusCode.Redirect, denegada.StatusCode);
        Assert.Equal("/login", denegada.Headers.Location!.AbsolutePath);
        using var grande = await cliente.GetAsync($"/productos/{producto.Id}/imagen?v={version}");
        Assert.NotEqual(miniatura.Headers.ETag, grande.Headers.ETag);
        using (var bitmap = SKBitmap.Decode(await grande.Content.ReadAsByteArrayAsync())) Assert.Equal(1200, bitmap.Height);
        using var sinVersion = await cliente.GetAsync($"/productos/{producto.Id}/miniatura");
        Assert.True(sinVersion.Headers.CacheControl!.NoCache);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var resultado = await scope.ServiceProvider.GetRequiredService<IProductoConImagenService>()
                .EditarGaleriaAsync(producto.Id, input, new(fotos.Select(x => new ImagenProductoEdicion(x.Id)).ToList(), 1), []);
            producto = resultado.Value!;
        }
        var nuevaVersion = ImagenProductoVersion.Obtener(producto.ImagenPrincipalRuta!);
        Assert.NotEqual(version, nuevaVersion);
        using var nueva = await cliente.GetAsync($"/productos/{producto.Id}/miniatura?v={nuevaVersion}");
        Assert.Equal(HttpStatusCode.OK, nueva.StatusCode);
        Assert.NotEqual(bytes, await nueva.Content.ReadAsByteArrayAsync());
        foreach (var ruta in new[] { url, $"/productos/{producto.Id}/imagen?v={version}", "/productos/2147483647/miniatura" })
        {
            using var error = await cliente.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
            Assert.True(error.Headers.CacheControl!.NoStore);
            Assert.Null(error.Headers.ETag);
            Assert.Empty(await error.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    public async Task FotoHeredada_UsaGrandeSinCacheHastaGenerarMiniatura_DespuesSirveDerivada()
    {
        ProductoDto producto;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
            var categoria = new Categoria { Nombre = "Heredados QA" };
            db.Add(categoria);
            await db.SaveChangesAsync();
            using var foto = Imagen(SKColors.Blue, 800, 1200);
            producto = (await scope.ServiceProvider.GetRequiredService<IProductoConImagenService>()
                .CrearAsync(new(null, "Existente QA", null, null, null, null, null, 0m, categoria.Id), foto)).Value!;
        }
        var partes = producto.ImagenPrincipalRuta!.Split('/');
        File.Delete(Path.ChangeExtension(Path.Combine(factory.RutaImagenes, partes[1], partes[2]), ".miniatura-v1.webp"));
        using var cliente = CrearCliente();
        await IniciarSesionAsync(cliente);
        var url = $"/productos/{producto.Id}/miniatura?v={ImagenProductoVersion.Obtener(producto.ImagenPrincipalRuta)}";
        using var heredada = await cliente.GetAsync(url);
        Assert.True(heredada.Headers.CacheControl!.NoStore);
        Assert.Null(heredada.Headers.ETag);
        using (var bitmap = SKBitmap.Decode(await heredada.Content.ReadAsByteArrayAsync())) Assert.Equal(1200, bitmap.Height);
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.Equal(new ResultadoMiniaturas(1, 0, 0), await scope.ServiceProvider.GetRequiredService<ProductoMiniaturasService>().GenerarPendientesAsync());
        using var derivada = await cliente.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, derivada.StatusCode);
        Assert.NotNull(derivada.Headers.ETag);
        using (var bitmap = SKBitmap.Decode(await derivada.Content.ReadAsByteArrayAsync())) Assert.Equal(240, bitmap.Height);
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

    private static MemoryStream Imagen(SKColor color, int ancho = 16, int alto = 24)
    {
        using var bitmap = new SKBitmap(ancho, alto);
        bitmap.Erase(color);
        using var imagen = SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(SKEncodedImageFormat.Png, 100);
        return new MemoryStream(datos.ToArray());
    }
}
