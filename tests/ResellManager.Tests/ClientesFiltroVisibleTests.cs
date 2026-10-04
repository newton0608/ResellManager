using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.DTOs;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Pages;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed class ClientesFiltroVisibleTests
{
    [Fact]
    public void AccionesRapidas_ConservanGridResponsiveConAlturaTactilDe96Px()
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz is not null && !File.Exists(Path.Combine(raiz.FullName, "ResellManager.sln"))) raiz = raiz.Parent;
        Assert.NotNull(raiz);
        var home = File.ReadAllText(Path.Combine(raiz.FullName, "src/ResellManager.Web/Components/Pages/Home.razor"));
        var acciones = System.Text.RegularExpressions.Regex.Match(home,
            "<section aria-labelledby=\"titulo-acciones-rapidas\">[\\s\\S]*?</section>");
        Assert.True(acciones.Success);
        Assert.Contains("grid-cols-2", acciones.Value, StringComparison.Ordinal);
        Assert.Contains("xl:grid-cols-4", acciones.Value, StringComparison.Ordinal);
        var enlaces = System.Text.RegularExpressions.Regex.Matches(acciones.Value,
            "<a\\s+class=\"([^\"]+)\"");
        Assert.Equal(4, enlaces.Count);
        foreach (System.Text.RegularExpressions.Match enlace in enlaces)
        {
            Assert.Contains("ui-button", enlace.Groups[1].Value, StringComparison.Ordinal);
            Assert.Contains("min-h-24", enlace.Groups[1].Value, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task TodosConDeuda_CombinanBusquedaYConservanCriterioEnUrlYAlVolver()
    {
        await using var test = await TestDatabase.CreateAsync();
        test.Cliente.Nombres = "Ana";
        await test.Db.SaveChangesAsync();
        await test.CrearVentaCatalogoAsync("PED-FILTRO", "VEN-FILTRO", 100m);
        var servicio = new ClienteService(test.Db);
        await servicio.CrearAsync(new("Ana", "Sin deuda", "222", null, null));
        await servicio.CrearAsync(new("Otra", null, "333", null, null));
        var pagina = new Clientes();
        Set(pagina, "ClienteService", servicio);
        Set(pagina, "Logger", NullLogger<Clientes>.Instance);
        await CallAsync(pagina, "OnParametersSetAsync");
        Assert.Equal(3, Get<IReadOnlyList<ClienteDto>>(pagina, "ClientesEncontrados").Count);
        Assert.Equal("/clientes", Call(pagina, "UrlFiltro", false));
        Assert.Equal("/clientes?saldo=pendiente", Call(pagina, "UrlFiltro", true));
        pagina.BusquedaQuery = "Ana";
        pagina.SaldoFiltro = "pendiente";
        await CallAsync(pagina, "OnParametersSetAsync");
        Assert.Equal(test.Cliente.Id, Assert.Single(Get<IReadOnlyList<ClienteDto>>(pagina, "ClientesEncontrados")).Id);
        Assert.Equal("/clientes?buscar=Ana", Call(pagina, "UrlFiltro", false));
        Assert.Equal("/clientes?buscar=Ana&saldo=pendiente", Call(pagina, "UrlFiltro", true));
        pagina.SaldoFiltro = null;
        await CallAsync(pagina, "OnParametersSetAsync");
        Assert.Equal("Ana", Get<string>(pagina, "Termino"));
        Assert.Equal(2, Get<IReadOnlyList<ClienteDto>>(pagina, "ClientesEncontrados").Count);
        Set(pagina, "Termino", "Ana & López");
        Assert.Contains("Ana%20%26%20L%C3%B3pez", (string)Call(pagina, "UrlFiltro", true)!);
    }
}
