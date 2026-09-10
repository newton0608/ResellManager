using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Productos;
using ResellManager.Web.Components.Shared;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed class BuscadoresContextualesTests
{
    [Fact]
    public async Task Producto_AltaSoloTrasBusquedaVacia_NoInicialNiConResultadosNiError()
    {
        await using var test = await TestDatabase.CreateAsync();
        using var services = new ServiceCollection().AddSingleton<IProductoService>(new ProductoService(test.Db)).BuildServiceProvider();
        using var buscador = new ProductoBuscador();
        Set(buscador, "ScopeFactory", services.GetRequiredService<IServiceScopeFactory>());
        Set(buscador, "Logger", NullLogger<ProductoBuscador>.Instance);
        var altas = 0;
        Set(buscador, "OnRegistrar", EventCallback.Factory.Create(new object(), () => altas++));
        Assert.DoesNotContain("Agregar producto", TextoRenderizado(buscador));
        await CallAsync(buscador, "RegistrarAsync");
        Assert.Equal(0, altas);
        await CallAsync(buscador, "CambiarTextoAsync", new ChangeEventArgs { Value = "Producto" });
        Assert.DoesNotContain("Agregar producto", TextoRenderizado(buscador));
        await CallAsync(buscador, "RegistrarAsync");
        Assert.Equal(0, altas);
        await CallAsync(buscador, "CambiarTextoAsync", new ChangeEventArgs { Value = "inexistente" });
        Assert.Contains("Agregar producto", TextoRenderizado(buscador));
        Set(buscador, "Error", "Error controlado");
        Assert.DoesNotContain("Agregar producto", TextoRenderizado(buscador));
        await CallAsync(buscador, "RegistrarAsync");
        Assert.Equal(0, altas);
        Set(buscador, "Error", null);
        await CallAsync(buscador, "RegistrarAsync");
        Assert.Equal(1, altas);
        Assert.DoesNotContain("Agregar producto", TextoRenderizado(buscador));
    }

    [Fact]
    public async Task BuscadorCompartido_DebounceCancelaAnterior_AltaContextualYSeleccion()
    {
        using var buscador = new BuscadorEntidad<ProveedorDto>();
        Set(buscador, "Logger", NullLogger<BuscadorEntidad<ProveedorDto>>.Instance);
        Set(buscador, "Resultado", (RenderFragment<ProveedorDto>)(p => b => b.AddContent(0, p.Nombre)));
        Set(buscador, "TextoAgregar", "Agregar proveedor");
        var consultas = new List<string>();
        Set(buscador, "Buscar", (Func<string, CancellationToken, Task<IReadOnlyList<ProveedorDto>>>)((texto, _) =>
        {
            consultas.Add(texto);
            return Task.FromResult<IReadOnlyList<ProveedorDto>>([]);
        }));
        var altas = 0;
        ProveedorDto? seleccionado = null;
        Set(buscador, "OnAgregar", EventCallback.Factory.Create(new object(), () => altas++));
        Set(buscador, "OnSeleccionado", EventCallback.Factory.Create<ProveedorDto?>(new object(), p => seleccionado = p));
        Assert.DoesNotContain("Agregar proveedor", TextoRenderizado(buscador));
        await CallAsync(buscador, "AgregarAsync");
        await CallAsync(buscador, "CambiarTextoAsync", new ChangeEventArgs { Value = "n" });
        Assert.Empty(consultas);
        var anterior = CallAsync(buscador, "CambiarTextoAsync", new ChangeEventArgs { Value = "nu" });
        var actual = CallAsync(buscador, "CambiarTextoAsync", new ChangeEventArgs { Value = "nuevo" });
        await Task.WhenAll(anterior, actual);
        Assert.Equal("nuevo", Assert.Single(consultas));
        Assert.Contains("Agregar proveedor", TextoRenderizado(buscador));
        await CallAsync(buscador, "AgregarAsync");
        Assert.Equal(1, altas);
        Assert.DoesNotContain("Agregar proveedor", TextoRenderizado(buscador));
        var proveedor = new ProveedorDto(1, "Nuevo", "555", null, null);
        await CallAsync(buscador, "SeleccionarAsync", proveedor);
        Assert.Same(proveedor, seleccionado);
        Set(buscador, "Seleccionado", proveedor);
        Set(buscador, "TextoSeleccionado", proveedor.Nombre);
        Call(buscador, "OnParametersSet");
        Assert.Contains("Seleccionado: Nuevo", TextoRenderizado(buscador));
    }

    internal static string TextoRenderizado(object componente)
    {
        using var tree = new RenderTreeBuilder();
        componente.GetType().GetMethod("BuildRenderTree", Flags)!.Invoke(componente, [tree]);
        return Texto(tree);
    }

    private static string Texto(RenderTreeBuilder tree)
    {
        var texto = new StringBuilder();
#pragma warning disable BL0006 // Misma inspección del árbol Razor que las regresiones UI existentes.
        var frames = tree.GetFrames();
        foreach (var frame in frames.Array.Take(frames.Count))
        {
            if (frame.FrameType == RenderTreeFrameType.Text) texto.Append(frame.TextContent);
            else if (frame.FrameType == RenderTreeFrameType.Markup) texto.Append(frame.MarkupContent);
            else if (frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeValue is RenderFragment fragment)
            {
                using var child = new RenderTreeBuilder();
                fragment(child);
                texto.Append(Texto(child));
            }
        }
#pragma warning restore BL0006
        return texto.ToString();
    }
}
