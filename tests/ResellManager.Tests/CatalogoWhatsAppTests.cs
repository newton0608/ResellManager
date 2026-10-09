using Microsoft.Extensions.Options;
using ResellManager.Web.Catalogo;

namespace ResellManager.Tests;

public sealed class CatalogoWhatsAppTests
{
    [Fact]
    public void Mensaje_CodificadoConNombreYUrlCanonicaNoPreviewNiPrecio()
    {
        var enlace = Crear("50255550123", "https://tienda.example").CrearEnlace(42, "Vitamina Á & B? #1");
        var url = new Uri(Assert.IsType<string>(enlace));
        Assert.Equal("wa.me", url.Host);
        Assert.Equal("/50255550123", url.AbsolutePath);
        Assert.Equal("?text=Hola, quisiera consultar por Vitamina Á & B? #1. https://tienda.example/producto/42", Uri.UnescapeDataString(url.Query));
        Assert.DoesNotContain("&B", url.Query);
        Assert.DoesNotContain("precio", enlace, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("+50255550123")]
    [InlineData("502 55550123")] [InlineData("50255550123&text=x")]
    [InlineData("123")] [InlineData("012345678")] [InlineData("1234567890123456")]
    public void NumeroInvalido_OcultarSinRomperCatalogo(string? numero) =>
        Assert.Null(Crear(numero, "https://tienda.example").CrearEnlace(1, "Producto"));

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("http://tienda.example")]
    [InlineData("https://localhost")] [InlineData("https://127.0.0.1")]
    [InlineData("https://user:pass@tienda.example")]
    [InlineData("https://tienda.example/catalogo")]
    [InlineData("https://tienda.example?x=1")]
    [InlineData("https://tienda.example/#fragment")]
    public void OrigenInvalido_NoGeneraEnlacePrivadoCorrupto(string? origen) =>
        Assert.Null(Crear("50255550123", origen).CrearEnlace(1, "Producto"));

    private static CatalogoWhatsApp Crear(string? numero, string? origen) => new(Options.Create(new CatalogoContactoOptions
    { WhatsAppNumero = numero, OrigenPublico = origen }));
}