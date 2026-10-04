namespace ResellManager.Web.Catalogo;

// Coordinates the public header search with the catalog page in the same Blazor circuit.
public sealed class BusquedaTiendaEstado
{
    private object? propietario;
    private Func<string, Task>? buscar;
    private Func<Task>? enviar;

    public string Termino { get; private set; } = string.Empty;
    public bool TieneCatalogo => buscar is not null;
    public event Action? Cambio;

    public void Registrar(object pagina, Func<string, Task> alBuscar, Func<Task> alEnviar)
    {
        propietario = pagina;
        buscar = alBuscar;
        enviar = alEnviar;
    }

    public void Liberar(object pagina)
    {
        if (!ReferenceEquals(propietario, pagina)) return;
        propietario = null;
        buscar = null;
        enviar = null;
    }

    public void FijarTermino(string? termino)
    {
        var nuevo = termino ?? string.Empty;
        if (Termino == nuevo) return;
        Termino = nuevo;
        Cambio?.Invoke();
    }

    public Task BuscarAsync(string termino)
    {
        FijarTermino(termino);
        return buscar?.Invoke(termino) ?? Task.CompletedTask;
    }

    public Task EnviarAsync() => enviar?.Invoke() ?? Task.CompletedTask;
}