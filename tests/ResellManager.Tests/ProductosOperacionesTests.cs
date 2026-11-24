using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Infrastructure.Storage;
namespace ResellManager.Tests;
public sealed class ProductosOperacionesTests
{
    private static ProductoOperacionesService Ops(TestDatabase t)=>new(t.Db,new FinanzasTests.Acceso(false),TimeProvider.System);
    private static CatalogoPublicoService Publico(TestDatabase t)=>new(t.Db,new AlmacenamientoImagenesProductoLocal(Options.Create(new AlmacenamientoImagenesProductoOptions{DirectorioBase=Path.Combine(Path.GetTempPath(),"catalogo-nuevo-pruebas")}),NullLogger<AlmacenamientoImagenesProductoLocal>.Instance));
    [Fact]
    public async Task Productos_FiltrosGlobalesAntesDePaginar_PrecioActualizaYConservaCostos()
    {
        await using var t=await TestDatabase.CreateAsync();for(var i=0;i<45;i++){var p=await t.CrearProductoAsync("PRO-FILTRO-"+i,0);p.Marca="Marca";p.Modelo="Modelo";p.ContenidoMl=250;}
        await t.Db.SaveChangesAsync();var s=Ops(t);var filtro=new ProductoFiltros(true,"Marca","Modelo",200,300);
        var p1=await s.ListarAsync(filtro);var p2=await s.ListarAsync(filtro,cursor:p1.SiguienteCursor);var p3=await s.ListarAsync(filtro,cursor:p2.SiguienteCursor);
        Assert.Equal(45,p1.Items.Concat(p2.Items).Concat(p3.Items).Select(x=>x.Id).Distinct().Count());
        var id=p1.Items[0].Id;Assert.True((await s.PrecioAsync(id,75,0,"precio")).IsSuccess);Assert.True((await s.PrecioAsync(id,75,0,"precio")).IsSuccess);
        Assert.False((await s.PrecioAsync(id,80,0,"precio-concurrente")).IsSuccess);
        Assert.DoesNotContain((await s.ListarAsync(filtro)).Items,x=>x.Id==id);Assert.Single(await t.Db.CambiosPrecioProducto.ToListAsync());
        Assert.Empty(await t.Db.UnidadesInventario.ToListAsync());
        await Assert.ThrowsAsync<ArgumentException>(()=>s.ListarAsync(new ProductoFiltros(true,"Otra"),cursor:p1.SiguienteCursor));
    }
    [Fact]
    public async Task Productos_VariantesIndependientes_AgotadoPorEnlace_SinResultadosPublicos()
    {
        await using var t=await TestDatabase.CreateAsync();var negro=t.Producto;negro.Color="Negro";var beige=await t.CrearProductoAsync("PRO-BEIGE",0);beige.Color="Beige";await t.Db.SaveChangesAsync();
        await t.CrearUnidadDisponibleAsync("COM-NEGRO");var compra=await new CompraService(t.Db).RegistrarAsync(t.Compra(OrigenCompra.CompraLocal,"COM-BEIGE",new(2026,11,22),producto:beige));Assert.True(compra.IsSuccess);
        var u=await t.Db.UnidadesInventario.SingleAsync(x=>x.ProductoId==beige.Id);u.Estado=EstadoUnidadInventario.Vendida;await t.Db.SaveChangesAsync();
        var s=Ops(t);var grupo=await s.VincularAsync(negro.Id,null,"Modelo",new Dictionary<string,string>());Assert.True(grupo.IsSuccess,grupo.ErrorMessage);
        Assert.True((await s.VincularAsync(beige.Id,grupo.Value!.Id,"",new Dictionary<string,string>())).IsSuccess);
        var publico=Publico(t);Assert.Equal(negro.Id,Assert.Single(await publico.ListarAsync()).Id);
        var detalle=await publico.ObtenerPorIdAsync(beige.Id);Assert.True(detalle.IsSuccess,detalle.ErrorMessage);Assert.False(detalle.Value!.Disponible);Assert.Equal(2,detalle.Value.Variantes!.Count);
        Assert.True(detalle.Value.Variantes.Single(x=>x.ProductoId==negro.Id).Disponible);
        var privado=await t.CrearProductoAsync("PRO-MAESTRO-NO-RECIBIDO",50);privado.Color="Rojo";await t.Db.SaveChangesAsync();
        Assert.True((await s.VincularAsync(privado.Id,grupo.Value.Id,"",new Dictionary<string,string>())).IsSuccess);
        var fichaPublica=(await publico.ObtenerPorIdAsync(beige.Id)).Value!;
        Assert.DoesNotContain(fichaPublica.Variantes!,x=>x.ProductoId==privado.Id);
        Assert.False((await publico.ObtenerPorIdAsync(privado.Id)).IsSuccess);
        Assert.False((await s.VincularAsync(beige.Id,null,"Otro grupo",new Dictionary<string,string>())).IsSuccess);
        Assert.True((await s.DesvincularAsync(beige.Id)).IsSuccess);Assert.Equal(3,await t.Db.Productos.CountAsync());Assert.Equal(2,await t.Db.UnidadesInventario.CountAsync());
    }
    [Fact]
    public async Task Productos_CodigoInternoYExterno_ColisionSeRechaza()
    {
        await using var t=await TestDatabase.CreateAsync();var consulta=new ProductoService(t.Db);
        Assert.Equal(t.Producto.Id,(await consulta.ObtenerPorCodigoBarrasAsync(t.Producto.CodigoInterno))!.Id);
        var p=await t.CrearProductoAsync("OTRO");p.CodigoBarras=t.Producto.CodigoInterno;await t.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>consulta.ObtenerPorCodigoBarrasAsync(t.Producto.CodigoInterno));
    }
    [Fact]
    public async Task Productos_EtiquetasCopiasXlsxPdf_NoMarcanImpresionNiCambianStock()
    {
        await using var t=await TestDatabase.CreateAsync();t.Producto.CodigoInterno="PRO-001";var dos=await t.CrearProductoAsync("PRO-002",75);var tres=await t.CrearProductoAsync("PRO-003",0);await t.Db.SaveChangesAsync();
        var s=new EtiquetasService(t.Db,new FinanzasTests.Acceso(false),TimeProvider.System);
        var lote=await s.PrepararAsync([new(t.Producto.Id,2),new(dos.Id,1),new(tres.Id,4)],new(),"etiquetas");Assert.True(lote.IsSuccess,lote.ErrorMessage);Assert.Equal(7,lote.Value!.Total);
        var excel=await s.ExportarAsync(lote.Value.Id,"xlsx");Assert.True(excel.IsSuccess,excel.ErrorMessage);
        using var zip=new ZipArchive(new MemoryStream(excel.Value!.Contenido));using var sheet=zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();var xml=XDocument.Load(sheet);XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Assert.Equal(9,xml.Descendants(ns+"row").Count());Assert.Empty(xml.Descendants(ns+"f"));
        var pdf=await s.ExportarAsync(lote.Value.Id,"pdf");Assert.True(pdf.IsSuccess,pdf.ErrorMessage);Assert.Equal("%PDF-",System.Text.Encoding.ASCII.GetString(pdf.Value!.Contenido[..5]));
        Assert.Null((await s.ObtenerAsync(lote.Value.Id)).Value!.ImpresoUtc);Assert.Empty(await t.Db.UnidadesInventario.ToListAsync());
        Assert.True((await s.ConfirmarImpresionAsync(lote.Value.Id)).IsSuccess);Assert.NotNull((await s.ObtenerAsync(lote.Value.Id)).Value!.ImpresoUtc);
        Assert.True((await Ops(t).PrecioAsync(dos.Id,80,75,"otro-precio")).IsSuccess);Assert.True((await s.ObtenerAsync(lote.Value.Id)).Value!.Desactualizado);Assert.False((await s.ExportarAsync(lote.Value.Id,"pdf")).IsSuccess);
    }
    [Fact]
    public async Task Productos_EtiquetaGuidNoSeComprimeParaEncajarEn50Mm()
    {
        await using var t=await TestDatabase.CreateAsync();t.Producto.CodigoInterno="PRO-"+Guid.NewGuid().ToString("N").ToUpperInvariant();await t.Db.SaveChangesAsync();
        var s=new EtiquetasService(t.Db,new FinanzasTests.Acceso(false),TimeProvider.System);
        var l=(await s.PrepararAsync([new(t.Producto.Id,1)],new(),"guid")).Value!;
        var r=await s.ExportarAsync(l.Id,"pdf");Assert.False(r.IsSuccess);Assert.Contains("requiere al menos",r.ErrorMessage);
        var ancho=(await s.PrepararAsync([new(t.Producto.Id,1)],new(120,40),"guid-ancho")).Value!;Assert.True((await s.ExportarAsync(ancho.Id,"pdf")).IsSuccess);
    }

    [Fact]
    public async Task Etiquetas_PerfilSinAlturaOCodigoNoCompatible_DevuelveValidacionSinMarcarImpresion()
    {
        await using var t=await TestDatabase.CreateAsync();
        var s=new EtiquetasService(t.Db,new FinanzasTests.Acceso(),TimeProvider.System);
        var corto=await s.PrepararAsync([new(t.Producto.Id,1)],new(120,25,5),"perfil-bajo");
        var pdf=await s.ExportarAsync(corto.Value!.Id,"pdf");Assert.False(pdf.IsSuccess);Assert.Contains("alto",pdf.ErrorMessage);
        t.Producto.CodigoInterno="PRO-Á";await t.Db.SaveChangesAsync();
        var legado=await s.PrepararAsync([new(t.Producto.Id,1)],new(120,40),"codigo-no-compatible");
        pdf=await s.ExportarAsync(legado.Value!.Id,"pdf");Assert.False(pdf.IsSuccess);Assert.Contains("Code 128",pdf.ErrorMessage);
        Assert.All(await t.Db.LotesEtiquetas.ToListAsync(),x=>Assert.Null(x.ImpresoUtc));
    }
}
