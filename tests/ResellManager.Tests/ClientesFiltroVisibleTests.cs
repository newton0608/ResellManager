using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.DTOs;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Pages;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed class ClientesFiltroVisibleTests
{
    [Fact]
    public void AccionesRapidas_ConservanGridConAlturaTactilDe80Px()
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz is not null && !File.Exists(Path.Combine(raiz.FullName, "ResellManager.sln"))) raiz = raiz.Parent;
        Assert.NotNull(raiz);
        var css = File.ReadAllText(Path.Combine(raiz.FullName, "src/ResellManager.Web/wwwroot/app.css"));
        Assert.Matches(@"\.dashboard-action-grid > a\s*\{[^}]*min-height:\s*5rem;", css);
        Assert.Matches(@"\.dashboard-action-grid\s*\{[^}]*grid-template-columns:\s*repeat\(2, minmax\(0, 1fr\)\)", css);
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
