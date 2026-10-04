using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using ResellManager.Web.Catalogo;

namespace ResellManager.Tests;

[Collection("Integración web")]
public sealed class VirtuosaStoreUiTests
{
    [Theory]
    [InlineData("/catalogo")]
    [InlineData("/catalogo/999")]
    public async Task PaginasPublicas_MuestranMarcaYFaviconDeVirtuosaSinPanelAdministrativo(string ruta)
    {
        using var factory = new AplicacionAutenticacionFactory();
        using var cliente = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var respuesta = await cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Null(respuesta.Headers.Location);

        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.Contains("Virtuosa Store", html);
        Assert.Contains("class=\"storefront", html);
        Assert.Contains("aria-label=\"Virtuosa Store, inicio del catálogo\"", html);
        Assert.Contains("src=\"/branding/virtuosa/icon-white.jpg\"", html);
        Assert.Contains("id=\"catalogo-busqueda\"", html);

        var iconos = Regex.Matches(html, "<link[^>]*rel=\"icon\"[^>]*>", RegexOptions.IgnoreCase);
        Assert.NotEmpty(iconos);
        Assert.Contains("href=\"/branding/virtuosa/icon-wine.jpg\"", iconos[^1].Value);

        Assert.DoesNotContain("class=\"rm-ui", html);
        Assert.DoesNotContain("app-sidebar", html);
        Assert.DoesNotContain("ResellManager, inicio del catálogo", html);
        Assert.DoesNotContain("Catálogo de ResellManager", html);
        Assert.DoesNotContain("href=\"/compras\"", html);
        Assert.DoesNotContain("href=\"/login\"", html);
    }

    [Theory]
    [InlineData("/branding/virtuosa/icon-white.jpg")]
    [InlineData("/branding/virtuosa/icon-wine.jpg")]
    public async Task AssetsDeMarca_SonPublicosYSeSirvenComoImagen(string ruta)
    {
        using var factory = new AplicacionAutenticacionFactory();
        using var cliente = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var respuesta = await cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("image/jpeg", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await respuesta.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task BusquedaTienda_EntregaTerminoYEnvioAlCatalogoYLiberacionRespetaPropietario()
    {
        var estado = new BusquedaTiendaEstado();
        var primeraPagina = new object();
        var paginaActual = new object();
        var recibidos = new List<string>();
        var cambios = 0;
        var envios = 0;
        estado.Cambio += () => cambios++;

        estado.Registrar(primeraPagina, termino =>
        {
            recibidos.Add("antigua:" + termino);
            return Task.CompletedTask;
        }, () => Task.CompletedTask);
        estado.Registrar(paginaActual, termino =>
        {
            recibidos.Add(termino);
            return Task.CompletedTask;
        }, () =>
        {
            envios++;
            return Task.CompletedTask;
        });

        estado.Liberar(primeraPagina);
        Assert.True(estado.TieneCatalogo);
        await estado.BuscarAsync("perfume");
        estado.FijarTermino("perfume");
        await estado.EnviarAsync();

        Assert.Equal("perfume", estado.Termino);
        Assert.Equal(["perfume"], recibidos);
        Assert.Equal(1, cambios);
        Assert.Equal(1, envios);

        estado.Liberar(paginaActual);
        Assert.False(estado.TieneCatalogo);
        await estado.BuscarAsync("ropa");
        await estado.EnviarAsync();
        Assert.Equal("ropa", estado.Termino);
        Assert.Equal(["perfume"], recibidos);
        Assert.Equal(1, envios);
    }
}