using Microsoft.EntityFrameworkCore;
using ResellManager.Application.Interfaces;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;
namespace ResellManager.Tests;
public sealed class ConcurrenciaOperacionTests
{
    [Fact]
    public async Task ReservaConLecturaObsoleta_NoPuedeReservarUnaUnidadVendidaPorCF()
    {
        await using var t=await TestDatabase.CreateAsync();var unidad=await t.CrearUnidadDisponibleAsync("CONCURRENCIA-RESERVA");var pedido=await t.CrearPedidoAsync(TipoPedido.Apartado,"PED-RESERVA-CONCURRENT");
        await using var antigua=new ResellManagerDbContext(new DbContextOptionsBuilder<ResellManagerDbContext>().UseSqlite(t.Db.Database.GetDbConnection()).Options);
        var obsoleta=await antigua.UnidadesInventario.SingleAsync();
        var r=await new VentaDirectaService(t.Db,new FinanzasTests.Acceso(),TimeProvider.System).RegistrarAsync(new("venta-concurrente",new(2026,11,22),null,null,null,[new(unidad.Id,null,null,100,null)],[new(MetodoPago.Efectivo,100,100)]));Assert.True(r.IsSuccess,r.ErrorMessage);
        obsoleta.DetallePedidoReservaId=pedido.Detalles.Single().Id;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>antigua.SaveChangesAsync());
        await t.Db.Entry(unidad).ReloadAsync();Assert.Equal(EstadoUnidadInventario.Vendida,unidad.Estado);Assert.Null(unidad.DetallePedidoReservaId);
    }

    [Fact]
    public async Task BorradorObsoleto_NoPuedeCambiarUnaAperturaConfirmadaPorOtraSesion()
    {
        await using var t=await TestDatabase.CreateAsync();
        var acceso=new FinanzasTests.Acceso();
        var servicio=new FinanzasService(t.Db,acceso,TimeProvider.System);
        var filas=new FilaAperturaInput[]{new(t.Cliente.Id,t.Cliente.Telefono,t.Cliente.Nombres,null,1250)};
        var preparada=await servicio.PrepararAsync(filas);
        await using var antigua=new ResellManagerDbContext(new DbContextOptionsBuilder<ResellManagerDbContext>().UseSqlite(t.Db.Database.GetDbConnection()).Options);
        await antigua.AperturasCartera.SingleAsync();
        Assert.True((await servicio.ConfirmarAsync(preparada.Value!.Version,"confirmar-otra-sesion")).IsSuccess);
        var obsoleta=new FinanzasService(antigua,acceso,TimeProvider.System);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>obsoleta.PrepararAsync([filas[0] with{Deuda=900}]));
        t.Db.ChangeTracker.Clear();
        var final=await t.Db.AperturasCartera.SingleAsync();
        Assert.NotNull(final.ConfirmadaUtc);Assert.Equal(1250,final.Total);
        Assert.Equal(1250,(await new ClienteService(t.Db).ObtenerSaldoAsync(t.Cliente.Id)).Value);
    }

    [Fact]
    public async Task PrecioConSesionLarga_RevalidaElPrecioActualDeOtraConexion()
    {
        await using var t=await TestDatabase.CreateAsync();
        await t.Db.Productos.SingleAsync();
        await using var otra=new ResellManagerDbContext(new DbContextOptionsBuilder<ResellManagerDbContext>().UseSqlite(t.Db.Database.GetDbConnection()).Options);
        var producto=await otra.Productos.SingleAsync();var anterior=producto.PrecioSugerido;
        producto.PrecioSugerido=anterior+50;await otra.SaveChangesAsync();
        var s=new ProductoOperacionesService(t.Db,new FinanzasTests.Acceso(),TimeProvider.System);
        var r=await s.PrecioAsync(producto.Id,anterior+100,anterior,"precio-sesion-obsoleta");
        Assert.False(r.IsSuccess);Assert.Empty(await t.Db.CambiosPrecioProducto.ToListAsync());
        Assert.Equal(anterior+50,(await otra.Productos.AsNoTracking().SingleAsync()).PrecioSugerido);
    }
}
