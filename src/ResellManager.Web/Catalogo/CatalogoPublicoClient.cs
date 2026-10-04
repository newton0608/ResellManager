using Microsoft.JSInterop;
using ResellManager.Application.DTOs;

namespace ResellManager.Web.Catalogo;

// HTTP del mismo origen desde el navegador, después del primer render interactivo.
public sealed class CatalogoPublicoClient(IJSRuntime js) : ICatalogoPublicoClient, IAsyncDisposable
{
    private Task<IJSObjectReference>? modulo;

    private async Task<IJSObjectReference> ModuloAsync(CancellationToken ct)
    {
        var actual = modulo ??= js.InvokeAsync<IJSObjectReference>("import", "./catalogo-publico.js").AsTask();
        try { return await actual.WaitAsync(ct); }
        catch
        {
            if (actual.IsFaulted && ReferenceEquals(modulo, actual)) modulo = null;
            throw;
        }
    }

    public async Task<IReadOnlyList<ProductoCatalogoDto>> ListarAsync(
        string? termino = null, int? categoriaId = null, CancellationToken ct = default) =>
        await (await ModuloAsync(ct)).InvokeAsync<ProductoCatalogoDto[]>("listar", ct, termino, categoriaId);

    public async Task<ProductoCatalogoDetalleDto?> ObtenerDetalleAsync(int productoId, CancellationToken ct = default) =>
        await (await ModuloAsync(ct)).InvokeAsync<ProductoCatalogoDetalleDto?>("detalle", ct, productoId);

    public async ValueTask DisposeAsync()
    {
        if (modulo is null) return;
        try { await (await modulo).DisposeAsync(); }
        catch (JSException) { }
        catch (OperationCanceledException) { }
    }
}
