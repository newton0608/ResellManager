using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Pages;
using ResellManager.Web.Components.Pagos;
using static ResellManager.Tests.RevisionOperacionesTests;
using static ResellManager.Tests.BuscadoresContextualesTests;

namespace ResellManager.Tests;

public sealed class PagoBusquedaRevisionTests
{
    [Theory]
    [InlineData("ana lopez")]
    [InlineData("ANA LOPEZ")]
    [InlineData("5551234")]
    public async Task Cliente_BuscaNombreCompletoYTelefono(string termino)
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new ClienteService(test.Db);
        var cliente = await servicio.CrearAsync(new("Ana", "Lopez", "5551234", null, null));
        Assert.Equal(cliente.Value!.Id, Assert.Single(await servicio.BuscarAsync(termino, limite: 12)).Id);
    }

    [Fact]
    public async Task Cliente_LimitaAntesDeCalcularSaldos_PriorizaTelefonoExacto_NoTruncaListado()
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new ClienteService(test.Db);
        for (var i = 0; i < 15; i++) await servicio.CrearAsync(new($"AAA 555 {i}", null, "100", null, null));
        var exacto = await servicio.CrearAsync(new("ZZZ", null, "555", null, null));
        var resultados = await servicio.BuscarAsync("555", limite: 12);
        Assert.Equal(12, resultados.Count);
        Assert.Equal(exacto.Value!.Id, resultados[0].Id);
        Assert.Equal(17, (await servicio.BuscarAsync("555")).Count); // Incluye el cliente inicial.
        Assert.Empty(await servicio.BuscarAsync("inexistente", limite: 12));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("999999")]
    [InlineData("invalido")]
    public async Task Pago_SinQueryOClienteInexistentePermiteBuscar(string? query)
    {
        await using var test = await TestDatabase.CreateAsync();
        var pagina = CrearPagina(test, new PagoService(test.Db));
        pagina.ClienteDesdeQuery = query;
        await CallAsync(pagina, "CargarClientesAsync");
        Assert.Null(Get<ClienteDto?>(pagina, "ClienteSeleccionado"));
        Assert.False(Get<bool>(pagina, "CargandoClientes"));
        Assert.Null(Get<string?>(pagina, "ErrorCarga"));
        if (query is not null) Assert.Contains("no está disponible", Get<string>(pagina, "ErrorOperacion"));
    }

    [Fact]
    public async Task Pago_QueryCargaSaldoHistorial_RevisionEditarYConfirmarUnaVez()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearVentaCatalogoAsync("PED-REV-PAGO", "VEN-REV-PAGO", 850m);
        var pagos = new PagoContado(new PagoService(test.Db));
        var pagina = CrearPagina(test, pagos);
        pagina.ClienteDesdeQuery = test.Cliente.Id.ToString();
        await CallAsync(pagina, "CargarClientesAsync");
        Assert.Equal(850m, Get<decimal?>(pagina, "SaldoActual"));
        Assert.Empty(Get<IReadOnlyList<PagoDto>>(pagina, "Historial"));
        var modelo = Get<PagoFormModel>(pagina, "Modelo");
        modelo.Monto = 300m;
        modelo.MetodoPago = MetodoPago.Transferencia;
        modelo.Referencia = "ABONO-PRUEBA";
        Call(pagina, "RevisarPago");
        Assert.True(Get<bool>(pagina, "RevisandoPago"));
        Assert.Equal(0, pagos.Llamadas);
        var resumen = TextoRenderizado(pagina);
        Assert.Contains("Q 850.00", resumen);
        Assert.Contains("Q 300.00", resumen);
        Assert.Contains("Q 550.00", resumen);
        Assert.Contains("ABONO-PRUEBA", resumen);
        Call(pagina, "EditarPago");
        await CallAsync(pagina, "ConfirmarPagoAsync");
        Assert.Same(modelo, Get<PagoFormModel>(pagina, "Modelo"));
        Assert.Empty(await test.Db.Pagos.ToListAsync());
        Call(pagina, "RevisarPago");
        var primera = CallAsync(pagina, "ConfirmarPagoAsync");
        await CallAsync(pagina, "ConfirmarPagoAsync");
        Assert.Equal(1, pagos.Llamadas);
        pagos.Continuar.SetResult();
        await primera;
        await CallAsync(pagina, "ConfirmarPagoAsync");
        Assert.Single(await test.Db.Pagos.ToListAsync());
        Assert.Equal(550m, Get<decimal?>(pagina, "SaldoActual"));
        Assert.Single(Get<IReadOnlyList<PagoDto>>(pagina, "Historial"));
    }

    [Fact]
    public async Task Pago_MontoMayorAlSaldoNoAbreRevisionNiPersiste()
    {
        await using var test = await TestDatabase.CreateAsync();
        var pagina = CrearPagina(test, new PagoService(test.Db));
        var cliente = (await new ClienteService(test.Db).ObtenerPorIdAsync(test.Cliente.Id)).Value!;
        await CallAsync(pagina, "SeleccionarClienteAsync", cliente);
        Get<PagoFormModel>(pagina, "Modelo").Monto = 10m;
        Call(pagina, "RevisarPago");
        Assert.False(Get<bool>(pagina, "RevisandoPago"));
        Assert.Contains("no puede superar", Get<string>(pagina, "ErrorOperacion"));
        Assert.Empty(await test.Db.Pagos.ToListAsync());
    }

    private static Pagos CrearPagina(TestDatabase test, IPagoService servicio)
    {
        var pagina = new Pagos();
        Set(pagina, "PagoService", servicio);
        Set(pagina, "ClienteService", new ClienteService(test.Db));
        Set(pagina, "Logger", NullLogger<Pagos>.Instance);
        return pagina;
    }

    private sealed class PagoContado(IPagoService servicio) : IPagoService
    {
        public int Llamadas;
        public TaskCompletionSource Continuar { get; } = new();
        public async Task<ServiceResult<PagoDto>> RegistrarAsync(PagoInput input, CancellationToken ct = default)
        {
            Llamadas++;
            await Continuar.Task;
            return await servicio.RegistrarAsync(input, ct);
        }
        public Task<IReadOnlyList<PagoDto>> ListarPorClienteAsync(int clienteId, CancellationToken ct = default) => servicio.ListarPorClienteAsync(clienteId, ct);
        public Task<ServiceResult<PagoDto>> ObtenerPorIdAsync(int id, CancellationToken ct = default) => servicio.ObtenerPorIdAsync(id, ct);
    }
}
