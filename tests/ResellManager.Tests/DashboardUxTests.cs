using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Web.Components.Pages;

namespace ResellManager.Tests;

public sealed class DashboardUxTests
{
    [Fact]
    public async Task Ganancia_EditarFechasNoReetiquetaImporteHastaConsultarDeNuevo()
    {
        var servicio = new DashboardControlado((_, _, intento) =>
            Task.FromResult(ServiceResult<decimal>.Ok(intento == 1 ? 125.50m : 80m)));
        var pagina = CrearPagina(servicio);
        Establecer(pagina, "Desde", new DateOnly(2026, 9, 1));
        Establecer(pagina, "Hasta", new DateOnly(2026, 9, 5));

        await ConsultarAsync(pagina);
        Establecer(pagina, "Desde", new DateOnly(2026, 8, 1));
        Establecer(pagina, "Hasta", new DateOnly(2026, 8, 31));

        var anterior = RenderizarTexto(pagina);
        Assert.Contains("Ganancia del 01/09/2026 al 05/09/2026", anterior);
        Assert.Contains("Q 125.50", anterior);
        Assert.DoesNotContain("Ganancia del 01/08/2026", anterior);
        Assert.Single(servicio.Consultas);

        await ConsultarAsync(pagina);

        var actualizado = RenderizarTexto(pagina);
        Assert.Contains("Ganancia del 01/08/2026 al 31/08/2026", actualizado);
        Assert.Contains("Q 80.00", actualizado);
        Assert.DoesNotContain("Q 125.50", actualizado);
        Assert.Equal(2, servicio.Consultas.Count);
    }

    [Fact]
    public async Task Ganancia_ConsultaPendienteNoSeDuplicaYConservaElRangoSolicitado()
    {
        var respuesta = new TaskCompletionSource<ServiceResult<decimal>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var servicio = new DashboardControlado((_, _, _) => respuesta.Task);
        var pagina = CrearPagina(servicio);
        var desde = new DateOnly(2026, 9, 1);
        var hasta = new DateOnly(2026, 9, 5);
        Establecer(pagina, "Desde", desde);
        Establecer(pagina, "Hasta", hasta);

        var primera = ConsultarAsync(pagina);
        Assert.True(Obtener<bool>(pagina, "ConsultandoUtilidad"));
        await ConsultarAsync(pagina);
        Assert.Equal((desde, hasta), Assert.Single(servicio.Consultas));
        Establecer(pagina, "Hasta", new DateOnly(2026, 9, 30));
        respuesta.SetResult(ServiceResult<decimal>.Ok(50m));
        await primera;

        Assert.False(Obtener<bool>(pagina, "ConsultandoUtilidad"));
        var contenido = RenderizarTexto(pagina);
        Assert.Contains("Ganancia del 01/09/2026 al 05/09/2026", contenido);
        Assert.Contains("Q 50.00", contenido);
        Assert.DoesNotContain("Ganancia del 01/09/2026 al 30/09/2026", contenido);
    }

    private static Home CrearPagina(IDashboardService servicio)
    {
        var pagina = new Home();
        Establecer(pagina, "DashboardService", servicio);
        Establecer(pagina, "Logger", NullLogger<Home>.Instance);
        return pagina;
    }

    private const BindingFlags Miembros = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static void Establecer(Home pagina, string propiedad, object valor) =>
        typeof(Home).GetProperty(propiedad, Miembros)!.SetValue(pagina, valor);

    private static T Obtener<T>(Home pagina, string propiedad) =>
        (T)typeof(Home).GetProperty(propiedad, Miembros)!.GetValue(pagina)!;

    private static Task ConsultarAsync(Home pagina) =>
        (Task)typeof(Home).GetMethod("ConsultarUtilidadAsync", Miembros)!.Invoke(pagina, null)!;

    private static string RenderizarTexto(Home pagina)
    {
        using var render = new RenderTreeBuilder();
        typeof(Home).GetMethod("BuildRenderTree", Miembros)!.Invoke(pagina, [render]);
        var texto = new StringBuilder();
        // Se inspecciona el árbol Razor generado, como en las regresiones UI existentes.
#pragma warning disable BL0006
        var frames = render.GetFrames();
        foreach (var frame in frames.Array.Take(frames.Count))
        {
            if (frame.FrameType == Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrameType.Text)
                texto.Append(frame.TextContent);
            else if (frame.FrameType == Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrameType.Markup)
                texto.Append(frame.MarkupContent);
        }
#pragma warning restore BL0006
        return texto.ToString();
    }

    private sealed class DashboardControlado(
        Func<DateOnly, DateOnly, int, Task<ServiceResult<decimal>>> consultar) : IDashboardService
    {
        public List<(DateOnly Desde, DateOnly Hasta)> Consultas { get; } = [];

        public Task<DashboardDto> ObtenerAsync(int cantidadRecientes = 5, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<ServiceResult<decimal>> ObtenerUtilidadAsync(
            DateOnly desde, DateOnly hasta, CancellationToken ct = default)
        {
            Consultas.Add((desde, hasta));
            return consultar(desde, hasta, Consultas.Count);
        }
    }
}
