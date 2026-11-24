using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Infrastructure.Storage;
namespace ResellManager.Tests;
public sealed class CostosTests
{
    [Fact]
    public async Task Costos_DesconocidoNoEsGanancia_CorreccionesPreservanOriginalYFechasDeCorte()
    {
        await using var t=await TestDatabase.CreateAsync();await t.CrearVentaCatalogoAsync("PED-CONOCIDO","VEN-CONOCIDA",250,100);
        var compra=await new CompraService(t.Db).RegistrarAsync(t.Compra(OrigenCompra.CompraLocal,"COM-DESCONOCIDO",new(2026,11,22)) with{Detalles=[new(t.Producto.Id,1,0,false)]});Assert.True(compra.IsSuccess);
        var u=await t.Db.UnidadesInventario.SingleAsync();Assert.False(u.CostoConocido);
        var v=await new VentaDirectaService(t.Db,new FinanzasTests.Acceso(),TimeProvider.System).RegistrarAsync(new("vender-sin-costo",new(2026,11,22),null,null,null,[new(u.Id,null,null,395,null)],[new(MetodoPago.Efectivo,395,395)]));Assert.True(v.IsSuccess,v.ErrorMessage);
        Assert.False(v.Value!.Detalles.Single().CostoConocido);
        var s=new CostosService(t.Db,new FinanzasTests.Acceso(),TimeProvider.System);
        var original=await s.UtilidadAsync(new(2026,1,1),new(2026,12,31));Assert.True(original.IsSuccess);Assert.Equal(150,original.Value!.UtilidadCalculable);Assert.Equal(1,original.Value.LineasSinCosto);Assert.Equal(395,original.Value.VentasSinCosto);
        var n=await s.CorregirAsync(new(u.Id,125,new(2026,11,22),"Factura comprobada","costo-real"));Assert.True(n.IsSuccess,n.ErrorMessage);Assert.Null(n.Value!.Anterior);
        Assert.True((await s.CorregirAsync(new(u.Id,125,new(2026,11,22),"Factura comprobada","costo-real"))).IsSuccess);
        var revisada=(await s.UtilidadAsync(new(2026,1,1),new(2026,12,31))).Value!;Assert.Equal(420,revisada.UtilidadCalculable);Assert.Equal(0,revisada.LineasSinCosto);Assert.Equal(1,revisada.Correcciones);
        var anterior=(await s.UtilidadAsync(new(2026,1,1),new(2026,12,31),original.Value.CorteUtc)).Value!;Assert.Equal(150,anterior.UtilidadCalculable);Assert.Equal(1,anterior.LineasSinCosto);
        Assert.Equal(150,(await s.UtilidadAsync(new(2026,1,1),new(2026,12,31),original:true)).Value!.UtilidadCalculable);
        Assert.True((await s.CorregirAsync(new(u.Id,150,new(2026,11,22),"Segunda factura comprobada","costo-real-2"))).IsSuccess);
        Assert.Equal(420,(await s.UtilidadAsync(new(2026,1,1),new(2026,12,31),n.Value.RegistradoUtc)).Value!.UtilidadCalculable);
        Assert.Equal(2,(await s.HistorialAsync(u.Id)).Value!.Count);
        Assert.False((await t.Db.DetallesVenta.SingleAsync(x=>x.VentaId==v.Value.Id)).CostoConocido);
        Assert.Equal(0,(await t.Db.DetallesVenta.SingleAsync(x=>x.VentaId==v.Value.Id)).CostoUnitario);
    }
    [Fact]
    public async Task Costos_Autorizacion_Estimaciones_MigracionAcotadaConRespaldoEIdempotencia()
    {
        var carpeta=Path.Combine(Path.GetTempPath(),"costos-prueba-"+Guid.NewGuid());
        try
        {
            await using var t=await TestDatabase.CreateAsync();var r=await new CompraService(t.Db).RegistrarAsync(t.Compra(OrigenCompra.CompraLocal,"HISTORICA-Q0",new(2026,11,22)) with{Detalles=[new(t.Producto.Id,1,0)]});Assert.True(r.IsSuccess);
            var u=await t.Db.UnidadesInventario.SingleAsync();var denegado=new CostosService(t.Db,new FinanzasTests.Acceso(false),TimeProvider.System);
            Assert.False((await denegado.CorregirAsync(new(u.Id,10,new(2026,11,22),"Sin acceso","denegar"))).IsSuccess);
            Assert.False((await denegado.MigrarHistoricosAsync([u.Id],"Migración inicial","sin-acceso")).IsSuccess);
            var s=new CostosService(t.Db,new FinanzasTests.Acceso(),TimeProvider.System,Options.Create(new AlmacenamientoComprobantesOptions{DirectorioBase=carpeta}));
            var m=await s.MigrarHistoricosAsync([u.Id],"Q0 confirmado desconocido por responsable","migracion");Assert.True(m.IsSuccess,m.ErrorMessage);
            Assert.True((await s.MigrarHistoricosAsync([u.Id],"Q0 confirmado desconocido por responsable","migracion")).IsSuccess);
            Assert.False(u.CostoConocido);Assert.False((await t.Db.DetallesCompra.SingleAsync()).CostoConocido);
            var registro=await t.Db.MigracionesCostosHistoricos.SingleAsync();Assert.True(File.Exists(registro.Respaldo));Assert.Contains(u.Id.ToString(),registro.AlcanceJson);
            Assert.False((await s.MigrarHistoricosAsync([u.Id],"Otra migración","otra")).IsSuccess);
            Assert.Single(await t.Db.MigracionesCostosHistoricos.ToListAsync());
        }
        finally{if(Directory.Exists(carpeta))Directory.Delete(carpeta,true);}
    }
    [Fact]
    public async Task Costos_CompraDesconocidaConImporteEstimadoSeRechaza()
    {
        await using var t=await TestDatabase.CreateAsync();var r=await new CompraService(t.Db).RegistrarAsync(t.Compra(OrigenCompra.CompraLocal,"ESTIMADA",new(2026,11,22)) with{Detalles=[new(t.Producto.Id,1,100,false)]});Assert.False(r.IsSuccess);Assert.Empty(await t.Db.Compras.ToListAsync());
    }
}
