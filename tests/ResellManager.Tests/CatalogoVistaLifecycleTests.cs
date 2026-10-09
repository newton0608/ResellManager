using System.Reflection;
using Microsoft.JSInterop;
using ResellManager.Web.Components.Catalogo;

namespace ResellManager.Tests;

public sealed class CatalogoVistaLifecycleTests
{
    [Fact]
    public async Task ImportPendiente_AlDesmontarNoCreaReferenciasNiObserversYLiberaModulo()
    {
        var js = new Js(); var vista = Vista(js);
        var render = Render(vista);
        await vista.DisposeAsync();
        js.Import.SetResult(js.Modulo);
        await render;
        Assert.Equal(1, js.Importaciones); Assert.Equal(1, js.Modulo.Descartes);
        Assert.Empty(js.Modulo.Llamadas);
        Assert.Null(typeof(CatalogoVista).GetField("Referencia", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(vista));
    }
    [Fact]
    public async Task RendersDuranteImport_NoDuplicanReferenciasNiInicializacion()
    {
        var js = new Js(); var vista = Vista(js);
        var primero = Render(vista); await Render(vista);
        Assert.Equal(1, js.Importaciones);
        js.Import.SetResult(js.Modulo); await primero;
        Assert.Equal(new[] { "iniciar", "actualizar" }, js.Modulo.Llamadas);
        await vista.DisposeAsync(); Assert.Equal(1, js.Modulo.Descartes);
        Assert.Equal("destruir", js.Modulo.Llamadas.Last());
        await Render(vista); Assert.Equal(1, js.Importaciones);
    }
    private static CatalogoVista Vista(Js js)
    {
        var vista = new CatalogoVista();
        typeof(CatalogoVista).GetProperty("JS", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(vista, js);
        return vista;
    }
    private static Task Render(CatalogoVista vista) => (Task)typeof(CatalogoVista).GetMethod("OnAfterRenderAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vista, [true])!;
    private sealed class Js : IJSRuntime
    {
        public int Importaciones;
        public readonly Modulo Modulo = new();
        public readonly TaskCompletionSource<IJSObjectReference> Import = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => InvokeAsync<T>(identifier, default, args);
        public async ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args)
        { Assert.Equal("import", identifier); Importaciones++; return (T)(object)await Import.Task; }
    }
    private sealed class Modulo : IJSObjectReference
    {
        public int Descartes; public List<string> Llamadas = [];
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => InvokeAsync<T>(identifier, default, args);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args)
        { Llamadas.Add(identifier); return ValueTask.FromResult(default(T)!); }
        public ValueTask DisposeAsync() { Descartes++; return ValueTask.CompletedTask; }
    }
}
