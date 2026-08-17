using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

public sealed class CasosUsoAdicionalesTests
{
    [Fact]
    public async Task Compra_PermiteConsultarComprobante()
    {
        await using var test = await TestDatabase.CreateAsync();
        var input = test.Compra(OrigenCompra.CompraLocal, "CL-COMP", new DateOnly(2026, 1, 12)) with
        {
            Comprobante = new ComprobanteCompraInput(
                "FAC-1",
                new DateOnly(2026, 1, 10),
                "comprobantes/fac-1.pdf",
                null
            ),
        };
        var compra = await new CompraService(test.Db).RegistrarAsync(input);

        var comprobante = await new CompraService(test.Db).ObtenerComprobanteAsync(
            compra.Value!.Id
        );

        Assert.True(comprobante.IsSuccess, comprobante.ErrorMessage);
        Assert.Equal("FAC-1", comprobante.Value!.NumeroDocumento);
    }

    [Fact]
    public async Task HistorialYDashboard_UsanVentasRegistradasMenosPagos()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearVentaCatalogoAsync("PED-HIST", "VEN-HIST", 100m, 40m);
        var pago = await new PagoService(test.Db).RegistrarAsync(
            new PagoInput(
                test.Cliente.Id,
                new DateOnly(2026, 2, 3),
                40m,
                MetodoPago.Efectivo,
                null,
                null
            )
        );
        Assert.True(pago.IsSuccess, pago.ErrorMessage);

        var historial = await new ClienteService(test.Db).ObtenerHistorialAsync(test.Cliente.Id);
        var dashboard = await new DashboardService(test.Db).ObtenerAsync();

        Assert.True(historial.IsSuccess, historial.ErrorMessage);
        Assert.Single(historial.Value!.Ventas);
        Assert.Single(historial.Value.Pagos);
        Assert.Equal(60m, historial.Value.Cliente.Saldo);
        Assert.Equal(60m, dashboard.TotalAdeudado);
    }
}
