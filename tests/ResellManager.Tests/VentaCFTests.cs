using Microsoft.EntityFrameworkCore;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;
public sealed class VentaCFTests
{
    private sealed class Reloj : TimeProvider { public override DateTimeOffset GetUtcNow()=>new(2026,11,22,18,0,0,TimeSpan.Zero); }
    private static VentaDirectaService Servicio(TestDatabase t,bool admin=true)=>new(t.Db,new FinanzasTests.Acceso(admin),new Reloj());
    private static VentaDirectaInput Input(int unidad,decimal precio=500,string clave="venta-cf")=>new(clave,new(2026,11,22),null,"Ana",null,
        [new DetalleVentaInput(unidad,null,null,precio,null)],[new(MetodoPago.Efectivo,300,400),new(MetodoPago.Transferencia,200,200)]);
    [Fact]
    public async Task CF_Mixto_Cambio_Atomico_Idempotente_SinClienteNiCartera()
    {
        await using var t=await TestDatabase.CreateAsync();var u=await t.CrearUnidadDisponibleAsync("CF-MIXTA");var s=Servicio(t);var input=Input(u.Id);
        var revision=await s.RevisarAsync(input);Assert.True(revision.IsSuccess,revision.ErrorMessage);Assert.Equal(100,revision.Value!.Cambio);
        var r=await s.RegistrarAsync(input);Assert.True(r.IsSuccess,r.ErrorMessage);Assert.True(r.Value!.ConsumidorFinal);Assert.Null(r.Value.ClienteId);Assert.Equal("Ana",r.Value.Cliente);
        Assert.True((await s.RegistrarAsync(input)).IsSuccess);Assert.Equal(1,await t.Db.Ventas.CountAsync());Assert.Equal(1,await t.Db.Pedidos.CountAsync());Assert.Equal(2,await t.Db.CobrosVenta.CountAsync());
        Assert.Single(await t.Db.Clientes.ToListAsync());Assert.Equal(0,(await new DashboardService(t.Db).ObtenerAsync()).TotalAdeudado);
        Assert.False((await new VentaService(t.Db).CancelarAsync(r.Value.Id)).IsSuccess);
        Assert.False((await s.RegistrarAsync(input with{Nombre="Contenido cambiado"})).IsSuccess);
    }
    [Theory]
    [InlineData(499,499,MetodoPago.Efectivo)]
    [InlineData(501,501,MetodoPago.Efectivo)]
    [InlineData(500,600,MetodoPago.Transferencia)]
    [InlineData(-500,-500,MetodoPago.Efectivo)]
    public async Task CF_CobrosInvalidos_NoDejanPedidoVentaNiStockModificado(decimal aplicado,decimal entregado,MetodoPago metodo)
    {
        await using var t=await TestDatabase.CreateAsync();var u=await t.CrearUnidadDisponibleAsync("CF-INVALIDA");
        var r=await Servicio(t).RegistrarAsync(Input(u.Id) with{Cobros=[new(metodo,aplicado,entregado)]});Assert.False(r.IsSuccess);
        Assert.Empty(await t.Db.Pedidos.ToListAsync());Assert.Empty(await t.Db.Ventas.ToListAsync());Assert.Empty(await t.Db.CobrosVenta.ToListAsync());
        Assert.Equal(EstadoUnidadInventario.Disponible,(await t.Db.UnidadesInventario.SingleAsync()).Estado);
    }
    [Fact]
    public async Task CF_Anulacion_AprobacionNoPresumeDevolucion_ClasificaInventario()
    {
        await using var t=await TestDatabase.CreateAsync();var u=await t.CrearUnidadDisponibleAsync("CF-ANULA");var s=Servicio(t);var venta=(await s.RegistrarAsync(Input(u.Id))).Value!;
        var vendedor=Servicio(t,false);var solicitud=await vendedor.SolicitarAnulacionAsync(venta.Id,"Devolución aceptada","solicitud");Assert.True(solicitud.IsSuccess);
        var id=solicitud.Value!.Id;Assert.Equal(EstadoVenta.Registrada,(await t.Db.Ventas.SingleAsync()).Estado);
        Assert.False((await vendedor.DecidirAsync(id,true,"Aprobada","sin-permiso")).IsSuccess);
        Assert.True((await s.DecidirAsync(id,true,"Aprobada por negocio","aprobar")).IsSuccess);
        Assert.False((await s.CompletarAsync(id,"prematuro")).IsSuccess);
        Assert.True((await s.RegistrarDevolucionAsync(new(id,"reembolso",500,MetodoPago.Deposito,new(2026,11,22),"Constancia sintética"))).IsSuccess);
        Assert.True((await s.RegistrarDevolucionAsync(new(id,"reembolso",500,MetodoPago.Deposito,new(2026,11,22),"Constancia sintética"))).IsSuccess);
        Assert.False((await s.RegistrarDevolucionAsync(new(id,"otro-reembolso",500,MetodoPago.Efectivo,new(2026,11,22),"Duplicado"))).IsSuccess);
        Assert.True((await s.RecibirAsync(id,u.Id,false,"Dañada, no apta para reventa","recepcion")).IsSuccess);
        Assert.Equal(EstadoUnidadInventario.DevueltaEnRevision,(await t.Db.UnidadesInventario.SingleAsync()).Estado);
        Assert.True((await s.CompletarAsync(id,"completar")).IsSuccess);Assert.True((await s.CompletarAsync(id,"completar")).IsSuccess);
        Assert.Equal(EstadoUnidadInventario.NoApta,(await t.Db.UnidadesInventario.SingleAsync()).Estado);
        Assert.Equal(EstadoVenta.Cancelada,(await t.Db.Ventas.SingleAsync()).Estado);Assert.Equal(1,await t.Db.DevolucionesDineroCF.CountAsync());
        Assert.Equal(2,await t.Db.CobrosVenta.CountAsync());
    }
    [Fact]
    public async Task CF_RechazoConservaVentaYCobros_SinEfectos()
    {
        await using var t=await TestDatabase.CreateAsync();var u=await t.CrearUnidadDisponibleAsync("CF-RECHAZA");var s=Servicio(t);var venta=(await s.RegistrarAsync(Input(u.Id))).Value!;
        var solicitud=(await s.SolicitarAnulacionAsync(venta.Id,"Solicitud","pedir")).Value!;
        Assert.True((await s.DecidirAsync(solicitud.Id,false,"Rechazada por negocio","rechazar")).IsSuccess);
        Assert.Equal(EstadoVenta.Registrada,(await t.Db.Ventas.SingleAsync()).Estado);Assert.Equal(EstadoUnidadInventario.Vendida,(await t.Db.UnidadesInventario.SingleAsync()).Estado);
        Assert.Empty(await t.Db.DevolucionesDineroCF.ToListAsync());
    }
}
