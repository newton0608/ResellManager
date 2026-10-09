using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ResellManager.Application.DTOs;
using ResellManager.Web.Components.Categorias;
using ResellManager.Web.Components.Productos;

namespace ResellManager.Tests;

public sealed class CatalogoAdministracionV14Tests
{
    [Fact]
    public void GaleriaMixta_ConservaIdentificadoresOrdenPortadaYLecturasNuevas()
    {
        var id = Guid.NewGuid();
        var modelo = new ProductoFormModel();
        modelo.CargarGaleria([new ImagenProductoDto(id, 0, true)]);
        modelo.Galeria[0].EsPortada = false;
        modelo.Galeria.Insert(0, new ProductoImagenFormModel { Contenido = [10], EsPortada = true });
        modelo.Galeria.Add(new ProductoImagenFormModel { Contenido = [20] });
        var edicion = modelo.ToGaleriaEdicion();
        Assert.Equal(0, edicion.PortadaIndice);
        Assert.Equal(new ImagenProductoEdicion(NuevaImagenIndice: 0), edicion.ImagenesOrdenadas[0]);
        Assert.Equal(new ImagenProductoEdicion(id), edicion.ImagenesOrdenadas[1]);
        Assert.Equal(new ImagenProductoEdicion(NuevaImagenIndice: 1), edicion.ImagenesOrdenadas[2]);
        using var lecturas = new ProductoGaleriaArchivos(modelo);
        Assert.Equal(2, lecturas.Imagenes.Count);
        Assert.Equal(10, lecturas.Imagenes[0].ReadByte());
        Assert.Equal(20, lecturas.Imagenes[1].ReadByte());
    }

    [Fact]
    public void CategoriasAdministracion_OrdenaRaicesEHijasYMuestraRutaSinImpedirElegirRaiz()
    {
        CategoriaDto[] categorias = [
            new(3, "Vitaminas", null, 2, "Salud"),
            new(2, "Salud", null, TieneSubcategorias: true),
            new(1, "Belleza", null),
            new(4, "Cuidado", null, 2, "Salud")];
        Assert.Equal(new[] { 1, 2, 4, 3 }, CategoriaPresentacion.Ordenar(categorias).Select(x => x.Id));
        Assert.Equal("Salud → Vitaminas", CategoriaPresentacion.Etiqueta(categorias[0]));
        Assert.Equal("Salud", CategoriaPresentacion.Etiqueta(categorias[1]));
        var modelo = CategoriaFormModel.FromDto(categorias[0]);
        Assert.Equal(2, modelo.CategoriaPadreId);
        Assert.Equal(2, modelo.ToInput().CategoriaPadreId);
        modelo.CategoriaPadreId = null;
        Assert.Null(modelo.ToInput().CategoriaPadreId);
    }

    [Fact]
    public async Task CategoriaConHijas_FormularioExplicaYBloqueaCambioDePadre()
    {
        await using var servicios = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(servicios, servicios.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var raiz = await renderer.RenderComponentAsync<CategoriaForm>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    ["Modelo"] = new CategoriaFormModel { Nombre = "Salud" },
                    ["TieneSubcategorias"] = true,
                    ["PadresDisponibles"] = new CategoriaDto[] { new(1, "Belleza", null) },
                }));
            return WebUtility.HtmlDecode(raiz.ToHtmlString());
        });
        Assert.Contains("id=\"categoria-padre\"", html);
        Assert.Contains("disabled", html);
        Assert.Contains("Esta categoría tiene subcategorías y debe conservarse como raíz.", html);
        Assert.Contains("Sin padre — categoría raíz", html);
    }
}
