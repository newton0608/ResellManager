namespace ResellManager.Tests;

// xUnit crea una instancia por caso: ningún método comparte SQLite ni comprobantes.
public abstract class PruebaWebAislada : IDisposable
{
    protected readonly AplicacionAutenticacionFactory factory = new();

    public void Dispose() => factory.Dispose();
}
