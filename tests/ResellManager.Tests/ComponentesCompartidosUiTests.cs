using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using ResellManager.Application.DTOs;
using ResellManager.Web.Components.Productos;
using ResellManager.Web.Components.Shared;

namespace ResellManager.Tests;

public sealed class ComponentesCompartidosUiTests
{
    [Fact]
    public async Task Confirmacion_Bloqueada_ConservaDialogTituloErrorYAmbasAccionesDeshabilitadas()
    {
        var html = await Render<ConfirmacionOperacion>(new()
        {
            [nameof(ConfirmacionOperacion.Titulo)] = "Revisar operación",
            [nameof(ConfirmacionOperacion.Ocupado)] = true,
            [nameof(ConfirmacionOperacion.Error)] = "No se pudo confirmar.",
            [nameof(ConfirmacionOperacion.ChildContent)] = (RenderFragment)(b => b.AddContent(0, "Resumen conservado")),
        });

        var dialogo = Regex.Match(html, "<dialog\\b[^>]*>");
        Assert.True(dialogo.Success);
        Assert.Contains("aria-busy=\"true\"", dialogo.Value);
        Assert.Contains("data-ocupado=\"true\"", dialogo.Value);
        var tituloId = Regex.Match(dialogo.Value, "aria-labelledby=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(tituloId);
        Assert.Matches("<h2\\b[^>]*id=\"" + Regex.Escape(tituloId) + "\"", html);
        Assert.Contains("Resumen conservado", html);
        Assert.Matches("<p\\b[^>]*role=\"alert\"[^>]*>No se pudo confirmar\\.</p>", html);
        var acciones = Regex.Matches(html, "<button\\b[^>]*>");
        Assert.Equal(2, acciones.Count);
        Assert.All(acciones.Cast<Match>(), boton => Assert.Matches("\\bdisabled\\b", boton.Value));
        Assert.Contains("Editar", html);
        Assert.Contains("Procesando...", html);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Filtros_ConservanBusquedaOpcionalFechasContenidoYDisabled(bool buscar)
    {
        var html = await Render<FiltrosHistorial>(new()
        {
            [nameof(FiltrosHistorial.Id)] = "qa-historial",
            [nameof(FiltrosHistorial.Modelo)] = new FiltroHistorialModelo
            {
                Termino = "PED-001",
                Desde = new DateOnly(2026, 1, 2),
                Hasta = new DateOnly(2026, 2, 3),
            },
            [nameof(FiltrosHistorial.MostrarBusqueda)] = buscar,
            [nameof(FiltrosHistorial.Ocupado)] = true,
            [nameof(FiltrosHistorial.ChildContent)] = (RenderFragment)(b => b.AddContent(0, "Filtro contextual")),
        });

        Assert.Contains("aria-label=\"Filtrar historial\"", html);
        Assert.Contains("Filtro contextual", html);
        Assert.Equal(buscar, html.Contains("id=\"qa-historial-buscar\"", StringComparison.Ordinal));
        foreach (var campo in new[] { "desde", "hasta" })
        {
            Assert.Contains("for=\"qa-historial-" + campo + "\"", html);
            var input = Regex.Match(html, "<input\\b[^>]*id=\"qa-historial-" + campo + "\"[^>]*>");
            Assert.True(input.Success);
            Assert.Contains("type=\"date\"", input.Value);
            Assert.Matches("\\bdisabled\\b", input.Value);
        }
        Assert.Contains("2026-01-02", html);
        Assert.Contains("2026-02-03", html);
        Assert.Matches("<button\\b[^>]*type=\"submit\"[^>]*disabled", html);
        Assert.Matches("<button\\b[^>]*type=\"button\"[^>]*disabled", html);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Buscadores_ConservanLabelEstadoAccesibleYDisabled(bool producto)
    {
        var html = producto
            ? await Render<ProductoBuscador>(new()
            {
                [nameof(ProductoBuscador.Id)] = "qa-selector",
                [nameof(ProductoBuscador.Deshabilitado)] = true,
            })
            : await Render<BuscadorEntidad<ProveedorDto>>(new()
            {
                [nameof(BuscadorEntidad<ProveedorDto>.Id)] = "qa-selector",
                [nameof(BuscadorEntidad<ProveedorDto>.Etiqueta)] = "Proveedor",
                [nameof(BuscadorEntidad<ProveedorDto>.Deshabilitado)] = true,
                [nameof(BuscadorEntidad<ProveedorDto>.Buscar)] =
                    (Func<string, CancellationToken, Task<IReadOnlyList<ProveedorDto>>>)((_, _) => Task.FromResult<IReadOnlyList<ProveedorDto>>([])),
                [nameof(BuscadorEntidad<ProveedorDto>.Resultado)] =
                    (RenderFragment<ProveedorDto>)(proveedor => b => b.AddContent(0, proveedor.Nombre)),
            });

        Assert.Contains("for=\"qa-selector\"", html);
        var input = Regex.Match(html, "<input\\b[^>]*id=\"qa-selector\"[^>]*>");
        Assert.True(input.Success);
        Assert.Contains("type=\"search\"", input.Value);
        Assert.Contains("autocomplete=\"off\"", input.Value);
        Assert.Contains("aria-describedby=\"qa-selector-estado\"", input.Value);
        Assert.Matches("\\bdisabled\\b", input.Value);
        Assert.Matches("<(?:p|div)\\b[^>]*id=\"qa-selector-estado\"[^>]*role=\"status\"[^>]*aria-live=\"polite\"", html);
        Assert.Contains("Escribe al menos 2 caracteres.", html);
        Assert.DoesNotContain("Agregar producto</button>", html);
        if (producto) Assert.Contains("aria-required=\"true\"", input.Value);
    }

    private static async Task<string> Render<T>(Dictionary<string, object?> parametros) where T : IComponent
    {
        using var servicios = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IJSRuntime>(new JsSinNavegador())
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(servicios, servicios.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
            WebUtility.HtmlDecode((await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parametros))).ToHtmlString()));
    }

    private sealed class JsSinNavegador : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => throw new InvalidOperationException("El render estático no debe ejecutar JavaScript.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => throw new InvalidOperationException("El render estático no debe ejecutar JavaScript.");
    }
}
