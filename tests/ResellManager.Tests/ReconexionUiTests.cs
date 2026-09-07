using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ResellManager.Web.Components.Layout;

namespace ResellManager.Tests;

[Collection("Integración web")]
public sealed class ReconexionUiTests
{
    [Fact]
    public async Task Modal_RenderizaEstadosEnEspanolConIdsOficialesUnicos()
    {
        await using var servicios = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(servicios, servicios.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var componente = await renderer.RenderComponentAsync<ReconnectModal>(ParameterView.Empty);
            return WebUtility.HtmlDecode(componente.ToHtmlString());
        });

        foreach (var id in new[] { "components-reconnect-modal", "components-reconnect-current-attempt",
                     "components-reconnect-max-retries", "reconnect-retry", "reconnect-reload" })
            Assert.Single(Regex.Matches(html, $"id=\"{id}\""));

        Assert.Contains("class=\"components-reconnect-hide\"", html);
        Assert.Contains("data-permanent", html);
        Assert.Contains("role=\"status\"", html);
        Assert.Contains("role=\"alert\"", html);
        Assert.Contains("aria-live=\"polite\"", html);
        Assert.Contains("aria-live=\"assertive\"", html);
        Assert.Contains("aria-busy=\"false\"", html);
        var textos = Regex.Replace(html, "<[^>]+>", " ");
        foreach (var texto in new[] { "Reconectando…", "Intentando conectar con el servidor.",
                     "Intento", "No se pudo restablecer la conexión",
                     "Revisa tu conexión a Internet e intenta nuevamente.", "Reintentar",
                     "La sesión ya no está disponible", "Recarga la página para continuar.", "Recargar página" })
            Assert.Contains(texto, textos);
        foreach (var ingles in new[] { "Attempting", "Reconnect", "Connection", "Retry", "Reload", "Refresh", "Server", "Failed" })
            Assert.DoesNotContain(ingles, textos, StringComparison.OrdinalIgnoreCase);
        Assert.Matches("id=\"components-reconnect-current-attempt\">\\s*</span>", html);
        Assert.Matches("id=\"components-reconnect-max-retries\">\\s*</span>", html);
    }

    [Fact]
    public async Task App_IncluyeUnSoloModalYScriptClienteAntesDeBlazorSinAlterarArranque()
    {
        using var factory = new AplicacionAutenticacionFactory();
        using var cliente = factory.CreateClient();
        var html = await cliente.GetStringAsync("/login");
        Assert.Single(Regex.Matches(html, "id=\"components-reconnect-modal\""));
        Assert.Single(Regex.Matches(html, "src=\"reconnect.js\""));
        Assert.True(html.IndexOf("src=\"reconnect.js\"", StringComparison.Ordinal)
            < html.IndexOf("src=\"_framework/blazor.web.js\"", StringComparison.Ordinal));
        Assert.DoesNotContain("autostart=\"false\"", html);

        var script = await cliente.GetStringAsync("/reconnect.js");
        Assert.Contains("await window.Blazor.reconnect()", script);
        Assert.Contains("window.location.reload()", script);
        Assert.DoesNotContain("Blazor.start", script);
        Assert.DoesNotContain("setInterval", script);
        Assert.DoesNotContain("setTimeout", script);
        Assert.DoesNotContain("maxRetries", script);
    }
}
