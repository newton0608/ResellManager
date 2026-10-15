using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ResellManager.Application.DTOs;
using ResellManager.Web.Endpoints;

namespace ResellManager.Tests;

public sealed class ImportacionTemporalProductosTests
{
    private const string Clave = "12345678901234567890123456789012ABCDEF";

    private static DefaultHttpContext Contexto(string? token = Clave)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Loopback;
        http.Request.Host = new HostString("localhost", 8080);
        if (token is not null) http.Request.Headers["X-Resell-Import-Key"] = token;
        return http;
    }

    private static IConfiguration Config(bool habilitada = true, DateTimeOffset? expira = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ImportacionProductos:Habilitada"] = habilitada.ToString(),
            ["ImportacionProductos:Token"] = Clave,
            ["ImportacionProductos:ExpiraUtc"] = (expira ?? DateTimeOffset.UtcNow.AddHours(1)).ToString("O")
        }).Build();

    [Fact]
    public void Api_CerradaPorDefectoYConTokenInvalido()
    {
        Assert.False(ImportacionTemporalProductosEndpoints.Autorizado(Contexto(), Config(false)));
        Assert.False(ImportacionTemporalProductosEndpoints.Autorizado(Contexto("otra-clave"), Config()));
        Assert.False(ImportacionTemporalProductosEndpoints.Autorizado(Contexto(null), Config()));
        Assert.True(ImportacionTemporalProductosEndpoints.Autorizado(Contexto(), Config()));
    }

    [Fact]
    public void Api_RechazaExpiracionYDuracionExcesiva()
    {
        Assert.False(ImportacionTemporalProductosEndpoints.Autorizado(Contexto(), Config(expira: DateTimeOffset.UtcNow.AddMinutes(-1))));
        Assert.False(ImportacionTemporalProductosEndpoints.Autorizado(Contexto(), Config(expira: DateTimeOffset.UtcNow.AddDays(3))));
    }

    [Fact]
    public void Api_RechazaOrigenExternoYProxyAunqueClaveValida()
    {
        var externa = Contexto();
        externa.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.20");
        Assert.False(ImportacionTemporalProductosEndpoints.Autorizado(externa, Config()));
        var host = Contexto();
        host.Request.Host = new HostString("app.resellmanager.tech");
        Assert.False(ImportacionTemporalProductosEndpoints.Autorizado(host, Config()));
        var proxy = Contexto();
        proxy.Request.Headers["X-Forwarded-For"] = "127.0.0.1";
        Assert.False(ImportacionTemporalProductosEndpoints.Autorizado(proxy, Config()));
    }

    [Fact]
    public void Validacion_RechazaCodigoDuplicadoYCategoriaInexistente()
    {
        var actual = new ProductoDto(1, "PRO-1", "7501234567890", "Producto", null, "Marca",
            "M1", null, null, 50, 2, "Perfumes");
        var input = new ProductoInput("7501234567890", "Producto nuevo", null, "Marca", "M2",
            null, null, 30, 99);
        var resultado = ImportacionTemporalProductosEndpoints.Revisar(input, [], [actual]);
        Assert.Contains(resultado.Errores, x => x.Contains("barras"));
        Assert.Contains(resultado.Errores, x => x.Contains("Categoría"));
    }

    [Fact]
    public void Validacion_RechazaDuplicadoExactoYAdvierteVariante()
    {
        var categoria = new CategoriaDto(2, "Perfumes", null);
        var actual = new ProductoDto(1, "PRO-1", null, "Perfume Rose", null, "Marca",
            "100 ml", null, null, 50, 2, "Perfumes");
        var duplicado = new ProductoInput(null, " perfume rose ", null, " marca ", "100 ml",
            null, null, 60, 2);
        var revision = ImportacionTemporalProductosEndpoints.Revisar(duplicado, [categoria], [actual]);
        Assert.Contains(revision.Errores, x => x.Contains("mismo nombre"));
        var variante = duplicado with { Modelo = "50 ml" };
        var revisionVariante = ImportacionTemporalProductosEndpoints.Revisar(variante, [categoria], [actual]);
        Assert.Empty(revisionVariante.Errores);
        Assert.NotEmpty(revisionVariante.Advertencias);
    }

    [Fact]
    public void Validacion_RechazaJerarquiaYContenidoIncompatible()
    {
        var categoria = new CategoriaDto(5, "Hija", null, CategoriaPadreId: 3);
        var input = new ProductoInput(null, "Nuevo", null, null, null, null, null,
            10, 5, ContenidoMl: 100, PesoGramos: 50, CategoriaPrincipalId: 4);
        var resultado = ImportacionTemporalProductosEndpoints.Revisar(input, [categoria, new CategoriaDto(3, "Raíz", null)], []);
        Assert.Equal(2, resultado.Errores.Count);
    }
    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    [InlineData(null, 0)]
    [InlineData(null, -1)]
    public void Validacion_RechazaMedidasNoPositivas(int? ml, int? gramos)
    {
        var input = new ProductoInput(null, "Prueba", null, null, null, null, null, 0, 1,
            ContenidoMl: ml, PesoGramos: gramos);
        var revision = ImportacionTemporalProductosEndpoints.Revisar(input, [new CategoriaDto(1, "Raíz", null)], []);
        Assert.NotEmpty(revision.Errores);
    }

    [Fact]
    public void Validacion_RevisaPresentacionYJerarquiaIgualQueServicio()
    {
        var input = new ProductoInput(null, "Prueba", null, null, null, null, null, 0, 3,
            Presentacion: new string('x', 101));
        var revision = ImportacionTemporalProductosEndpoints.Revisar(input,
            [new CategoriaDto(1, "Raíz", null), new CategoriaDto(2, "Hija", null, 1), new CategoriaDto(3, "Nieta", null, 2)], []);
        Assert.Equal(2, revision.Errores.Count);
    }

    [Fact]
    public void Variantes_PorContenidoColorTallaOPresentacionNoSonDuplicadosExactos()
    {
        var categoria = new CategoriaDto(1, "Prueba", null);
        var existente = new ProductoDto(1, "PRO-1", null, "Perfume", null, "Marca", null,
            null, null, 50, 1, "Prueba", ContenidoMl: 100);
        var input = new ProductoInput(null, "Perfume", null, "Marca", null, null, null, 50, 1, ContenidoMl: 100);
        foreach (var variante in new[] { input with { ContenidoMl = 50 }, input with { Color = "Azul" },
            input with { Talla = "M" }, input with { Presentacion = "Caja" } })
        {
            var revision = ImportacionTemporalProductosEndpoints.Revisar(variante, [categoria], [existente]);
            Assert.Empty(revision.Errores);
            Assert.NotEmpty(revision.Advertencias);
        }
    }

    [Fact]
    public void Autorizacion_ConfiguracionAusenteOMalformadaCierraLaApi()
    {
        var vacia = new ConfigurationBuilder().Build();
        Assert.False(ImportacionTemporalProductosEndpoints.Autorizado(Contexto(), vacia));
        var config = Config();
        config["ImportacionProductos:Habilitada"] = "valor inválido";
        Assert.False(ImportacionTemporalProductosEndpoints.Autorizado(Contexto(), config));
    }
}
