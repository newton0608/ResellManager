using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

public sealed class FinanzasTests
{
    internal sealed class Acceso(bool admin = true, string? id = "admin-sintetico") : ICuentaOperativa
    {
        public Task<CuentaOperativa?> ObtenerAsync(CancellationToken ct = default) => Task.FromResult(id is null ? null : new CuentaOperativa(id, admin));
    }
    private sealed class Reloj : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 11, 22, 1, 0, 0, TimeSpan.Zero); }
    private static FinanzasService Crear(ResellManagerDbContext db, bool admin = true) => new(db, new Acceso(admin), new Reloj());
    private static FilaAperturaInput Fila(TestDatabase t, decimal importe = 1250) => new(t.Cliente.Id, t.Cliente.Telefono, t.Cliente.Nombres, null, importe);

    [Fact]
    public async Task Finanzas_AperturaGlobal_Abono_Ajuste_Reversion_ConservanSaldoYCorte()
    {
        await using var t = await TestDatabase.CreateAsync(); var f = Crear(t.Db);
        var p = await f.PrepararAsync([Fila(t)]); Assert.True(p.IsSuccess); Assert.True(p.Value!.PuedeConfirmar);
        Assert.Empty(await t.Db.MovimientosCuenta.ToListAsync());
        var c = await f.ConfirmarAsync(p.Value.Version, "abrir"); Assert.True(c.IsSuccess, c.ErrorMessage);
        Assert.Equal(new DateOnly(2026, 11, 21), c.Value!.Corte);
        var repetir = await f.ConfirmarAsync(p.Value.Version, "abrir"); Assert.True(repetir.IsSuccess);
        Assert.False((await f.ConfirmarAsync(p.Value.Version, "segunda")).IsSuccess);
        Assert.False((await f.PrepararAsync([Fila(t)])).IsSuccess);
        Assert.True((await new PagoService(t.Db).RegistrarAsync(new(t.Cliente.Id, new(2026,11,22),300,MetodoPago.Efectivo,null,null))).IsSuccess);
        Assert.Equal(950, (await new ClienteService(t.Db).ObtenerSaldoAsync(t.Cliente.Id)).Value);
        Assert.Equal(950, Assert.Single(await new ClienteService(t.Db).ListarAsync()).Saldo);
        var a = await f.AjustarAsync(new(t.Cliente.Id,150,"Corrección comprobada","ajuste")); Assert.True(a.IsSuccess);
        Assert.Equal(1100, a.Value!.SaldoResultante);
        Assert.True((await f.AjustarAsync(new(t.Cliente.Id,150,"Corrección comprobada","ajuste"))).IsSuccess);
        Assert.False((await f.AjustarAsync(new(t.Cliente.Id,151,"Otro contenido","ajuste"))).IsSuccess);
        Assert.False((await f.AjustarAsync(new(t.Cliente.Id,-1101,"Excede","exceso"))).IsSuccess);
        Assert.True((await f.RevertirAsync(a.Value.MovimientoId,"Reversión comprobada","revertir")).IsSuccess);
        Assert.True((await f.RevertirAsync(a.Value.MovimientoId,"Reversión comprobada","revertir")).IsSuccess);
        Assert.False((await f.RevertirAsync(a.Value.MovimientoId,"Dos veces","otra-reversion")).IsSuccess);
        var historial = await f.ConsultarAsync(new(t.Cliente.Id)); Assert.True(historial.IsSuccess, historial.ErrorMessage);
        Assert.Equal(950, historial.Value!.SaldoActual); Assert.Equal(950, historial.Value.Filas.Sum(x=>x.Importe));
        Assert.Equal(4, historial.Value.Filas.Count);
        Assert.Empty(await t.Db.Ventas.ToListAsync()); Assert.Empty(await t.Db.Pedidos.ToListAsync());
    }

    [Theory]
    [InlineData("sin-admin")]
    [InlineData(null)]
    public async Task Finanzas_PermisosSeVerificanEnServicios(string? id)
    {
        await using var t = await TestDatabase.CreateAsync(); var f = new FinanzasService(t.Db,new Acceso(false,id),TimeProvider.System);
        Assert.False((await f.PrepararAsync([Fila(t)])).IsSuccess);
        Assert.False((await f.PrepararCsvAsync("texto")).IsSuccess);
        Assert.False((await f.ConfirmarAsync(Guid.NewGuid(),"key")).IsSuccess);
        Assert.False((await f.AjustarAsync(new(t.Cliente.Id,1,"Motivo","key"))).IsSuccess);
        Assert.False((await f.RevertirAsync(Guid.NewGuid(),"Motivo","key")).IsSuccess);
        Assert.False((await f.ExportarAsync("apertura","xlsx",new())).IsSuccess);
        Assert.Empty(await t.Db.MovimientosCuenta.ToListAsync());
    }

    [Fact]
    public async Task Finanzas_Csv_ValidaFilas_OmiteCero_CreaClienteSoloAlConfirmar()
    {
        await using var t = await TestDatabase.CreateAsync(); var f = Crear(t.Db);
        var p = await f.PrepararCsvAsync("clienteId,telefono,nombre,apellido,deuda,observacion\r\n,55551234,Ana,Pérez,1250.00,\"Deuda, revisada\"\r\n,55559876,Otro,,0.00,\r\n");
        Assert.True(p.IsSuccess,p.ErrorMessage); Assert.True(p.Value!.PuedeConfirmar); Assert.Equal(1250,p.Value.Total);
        Assert.Equal("Deuda, revisada",p.Value.Filas[0].Observacion); Assert.True(p.Value.Filas[1].Omitida);
        Assert.Single(await t.Db.Clientes.ToListAsync());
        Assert.True((await f.ConfirmarAsync(p.Value.Version,"importar")).IsSuccess);
        Assert.Equal(2,await t.Db.Clientes.CountAsync()); Assert.Single(await t.Db.MovimientosCuenta.ToListAsync());
        Assert.False((await f.PrepararCsvAsync("clienteId,telefono,nombre,apellido,deuda,observacion\n,55551234,Ana,,1,000.00,")).IsSuccess);
    }

    [Fact]
    public async Task Finanzas_FilasDuplicadas_Invalidas_ActividadPrevia_BloqueanLoteCompleto()
    {
        await using var t = await TestDatabase.CreateAsync(); var f = Crear(t.Db);
        var p = await f.PrepararAsync([Fila(t),Fila(t,-1),new(null,"","Falta",null,10)]);
        Assert.False(p.Value!.PuedeConfirmar); Assert.False((await f.ConfirmarAsync(p.Value.Version,"invalido")).IsSuccess);
        Assert.Empty(await t.Db.MovimientosCuenta.ToListAsync());
        p = await f.PrepararAsync([Fila(t),Fila(t)]); Assert.False(p.Value!.PuedeConfirmar);
        p = await f.PrepararAsync([Fila(t)]);
        await t.CrearVentaCatalogoAsync("PED-PREVIA","VEN-PREVIA");
        Assert.False((await f.ConfirmarAsync(p.Value!.Version,"actividad")).IsSuccess);
        Assert.Empty(await t.Db.MovimientosCuenta.ToListAsync());
    }

    [Fact]
    public async Task Finanzas_CancelacionIncluyeAperturaYAjustes()
    {
        await using var t = await TestDatabase.CreateAsync(); var f = Crear(t.Db);
        var p = await f.PrepararAsync([Fila(t,200)]); Assert.True((await f.ConfirmarAsync(p.Value!.Version,"apertura")).IsSuccess);
        var venta=await t.CrearVentaCatalogoAsync("PED-CANCELAR","VEN-CANCELAR",100,40);
        Assert.True((await new PagoService(t.Db).RegistrarAsync(new(t.Cliente.Id,new(2026,11,22),150,MetodoPago.Efectivo,null,null))).IsSuccess);
        Assert.True((await new VentaService(t.Db).CancelarAsync(venta.Id)).IsSuccess);
        Assert.Equal(50,(await new ClienteService(t.Db).ObtenerSaldoAsync(t.Cliente.Id)).Value);
    }

    [Theory]
    [InlineData("xlsx")]
    [InlineData("pdf")]
    public async Task Finanzas_ExportaConjuntoFiltradoCompletoConFormatoReal(string formato)
    {
        await using var t = await TestDatabase.CreateAsync(); var f = Crear(t.Db);
        for(var i=0;i<35;i++) Assert.True((await f.AjustarAsync(new(t.Cliente.Id,1,"=SUM(A1:A9)","ajuste-"+i))).IsSuccess);
        var p=await f.ConsultarAsync(new(t.Cliente.Id,Tamano:10)); Assert.Equal(10,p.Value!.Filas.Count); Assert.True(p.Value.HayMas);
        var r=await f.ExportarAsync("cliente",formato,new(t.Cliente.Id,Tamano:10)); Assert.True(r.IsSuccess,r.ErrorMessage);
        if(formato=="pdf") Assert.StartsWith("%PDF-",Encoding.ASCII.GetString(r.Value!.Contenido[..5]));
        else
        {
            using var zip=new ZipArchive(new MemoryStream(r.Value!.Contenido));
            using var archivo=zip.GetEntry("xl/worksheets/sheet1.xml")!.Open(); var xml=XDocument.Load(archivo);
            XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            Assert.Equal(39,xml.Descendants(ns+"row").Count()); Assert.Empty(xml.Descendants(ns+"f"));
            Assert.Equal(35,xml.Descendants(ns+"t").Count(x=>x.Value=="=SUM(A1:A9)"));
        }
    }

    [Fact]
    public async Task Finanzas_MovimientosYComandosNoSeEditanNiBorran()
    {
        await using var t = await TestDatabase.CreateAsync(); var f=Crear(t.Db);
        var a=await f.AjustarAsync(new(t.Cliente.Id,10,"Motivo","inmutable"));
        var movimiento=await t.Db.MovimientosCuenta.SingleAsync(); movimiento.Importe=999;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>t.Db.SaveChangesAsync());
        t.Db.ChangeTracker.Clear(); Assert.Equal(10,(await t.Db.MovimientosCuenta.SingleAsync()).Importe);
    }

    [Fact]
    public async Task Finanzas_ConexionesIndependientes_DobleComandoNoDuplica()
    {
        var ruta=Path.Combine(Path.GetTempPath(),"finanzas-"+Guid.NewGuid()+".db");
        try
        {
            await using(var t=await TestDatabase.CreateAsync())
            {
                using var destino=new SqliteConnection("Data Source="+ruta); await destino.OpenAsync();
                ((SqliteConnection)t.Db.Database.GetDbConnection()).BackupDatabase(destino);
            }
            async Task<decimal> Aplicar()
            {
                await using var db=new ResellManagerDbContext(new DbContextOptionsBuilder<ResellManagerDbContext>().UseSqlite("Data Source="+ruta+";Default Timeout=10;Pooling=False").Options);
                var cliente=await db.Clientes.Select(x=>x.Id).SingleAsync();
                var r=await Crear(db).AjustarAsync(new(cliente,100,"Mismo comando","concurrente"));
                Assert.True(r.IsSuccess,r.ErrorMessage); return r.Value!.SaldoResultante;
            }
            var resultados=await Task.WhenAll(Task.Run(Aplicar),Task.Run(Aplicar)); Assert.All(resultados,x=>Assert.Equal(100,x));
            await using var final=new ResellManagerDbContext(new DbContextOptionsBuilder<ResellManagerDbContext>().UseSqlite("Data Source="+ruta+";Pooling=False").Options);
            Assert.Equal(1,await final.MovimientosCuenta.CountAsync());
        }
        finally { if(File.Exists(ruta))File.Delete(ruta); }
    }
}
