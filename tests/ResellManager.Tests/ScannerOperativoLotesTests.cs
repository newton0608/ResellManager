using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ResellManager.Web.Components.Ventas;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed partial class ScannerOperativoTests
{
    [Fact]
    public async Task Lotes_AgrupaPorAccion_NoFusionaProductoOPrecio_QuitarConservaIdentidades()
    {
        await using var test = await DatosAsync(7);
        await using var vista = await Vista.CrearAsync(test);
        await AgregarLoteAsync(vista, 5);
        await AgregarLoteAsync(vista, 1);
        var unidadManual = (await new ResellManager.Infrastructure.Services.SeleccionOperativaService(test.Db)
            .ListarUnidadesDirectasAsync(test.Producto.Id, vista.Articulos.Select(x => x.Unidad.Id).ToArray())).Single();
        await vista.EjecutarAsync("AgregarUnidadAsync", unidadManual);
        var lotes = Get<IReadOnlyList<LoteVentaDirectaFormModel>>(vista.Componente, "Lotes");
        Assert.Equal(new[] { 5, 1, 1 }, lotes.Select(x => x.Cantidad));
        Assert.Equal(3, lotes.Select(x => x.Id).Distinct().Count());
        Assert.All(lotes, lote => Assert.Equal(test.Producto.PrecioSugerido, lote.PrecioFinal));
        lotes[0].PrecioFinal = 75m;
        lotes[1].PrecioFinal = 65m;
        Assert.All(lotes[0].Unidades, x => Assert.Equal(75m, x.ToVentaInput().PrecioFinal));
        Assert.Equal(65m, lotes[1].Unidades.Single().PrecioFinal);
        Assert.Equal(120m, lotes[2].PrecioFinal);
        var idLote = lotes[0].Id;
        var restantes = lotes[0].Unidades.Skip(1).Select(x => x.Unidad.Id).ToArray();
        await vista.EjecutarAsync("QuitarUnidad", lotes[0].Unidades[0]);
        var actual = Get<IReadOnlyList<LoteVentaDirectaFormModel>>(vista.Componente, "Lotes")[0];
        Assert.Equal(idLote, actual.Id);
        Assert.Equal(4, actual.Cantidad);
        Assert.Equal(restantes, actual.Unidades.Select(x => x.Unidad.Id));
        Assert.Equal(300m, actual.Subtotal);
        var html = await vista.HtmlAsync();
        Assert.Equal(3, Regex.Matches(html, "id=.precio-directo-").Count);
        Assert.Equal(3, Regex.Matches(html, "<article").Count);
        Assert.Contains("×4", html);
        foreach (var unidad in actual.Unidades) await vista.EjecutarAsync("QuitarUnidad", unidad);
        Assert.Equal(2, Get<IReadOnlyList<LoteVentaDirectaFormModel>>(vista.Componente, "Lotes").Count);
        Assert.DoesNotContain(vista.Articulos, x => x.LoteId == idLote);
        await vista.EjecutarAsync("RevisarVentaDirectaAsync");
        Assert.Equal(2, Regex.Matches(await vista.HtmlAsync(), "rm-sale-review-lot ").Count);
        Assert.Equal(lotes.Skip(1).Select(x => x.Id), Get<IReadOnlyList<LoteVentaDirectaFormModel>>(vista.Componente, "LotesSeleccionados").Select(x => x.Id));
    }

    [Fact]
    public async Task Lotes_PrecioComunYPrecioIndependiente_PersistenSeisUnidadesFisicas()
    {
        await using var test = await DatosAsync(6);
        await using var vista = await Vista.CrearAsync(test);
        await AgregarLoteAsync(vista, 5);
        Get<IReadOnlyList<LoteVentaDirectaFormModel>>(vista.Componente, "Lotes")[0].PrecioFinal = 75m;
        await AgregarLoteAsync(vista, 1);
        Get<IReadOnlyList<LoteVentaDirectaFormModel>>(vista.Componente, "Lotes")[1].PrecioFinal = 65m;
        var ids = vista.Articulos.Select(x => x.Unidad.Id).ToArray();
        await vista.EjecutarAsync("RevisarVentaDirectaAsync");
        var html = await vista.HtmlAsync();
        Assert.Equal(2, Regex.Matches(html, "rm-sale-review-lot ").Count);
        Assert.Contains("5 × Q 75.00 = Q 375.00", html);
        Assert.Contains("1 × Q 65.00 = Q 65.00", html);
        await vista.EjecutarAsync("ConfirmarVentaAsync");
        var venta = Assert.Single(await test.Db.Ventas.Include(x => x.Detalles).ToListAsync());
        Assert.Equal(6, venta.Detalles.Count);
        Assert.Equal(ids.Order(), venta.Detalles.Select(x => x.UnidadInventarioId!.Value).Order());
        Assert.Equal(5, venta.Detalles.Count(x => x.PrecioFinal == 75m));
        Assert.Equal(1, venta.Detalles.Count(x => x.PrecioFinal == 65m));
        Assert.Equal(440m, venta.Detalles.Sum(x => x.PrecioFinal));
    }

    [Fact]
    public async Task MarkupOperativoLotes_MismoProductoPreciosIgualesODistintosYRevision()
    {
        await using var test = await DatosAsync(6);
        await using var vista = await Vista.CrearAsync(test);
        await AgregarLoteAsync(vista, 5);
        await AgregarLoteAsync(vista, 1);
        await vista.GuardarHtmlAsync("venta-lotes-iguales");
        var lotes = Get<IReadOnlyList<LoteVentaDirectaFormModel>>(vista.Componente, "Lotes");
        lotes[0].PrecioFinal = 75m; lotes[1].PrecioFinal = 65m;
        await vista.EjecutarAsync("StateHasChanged");
        await vista.GuardarHtmlAsync("venta-lotes-precios");
        await vista.EjecutarAsync("RevisarVentaDirectaAsync");
        await vista.GuardarHtmlAsync("venta-lotes-revision");
    }

    private static async Task AgregarLoteAsync(Vista vista, int cantidad)
    {
        await vista.DetectarAsync(Codigo);
        Set(vista.Componente, "CantidadEscaneada", cantidad.ToString());
        await vista.EjecutarAsync("AgregarEscaneadasAsync");
    }
}
