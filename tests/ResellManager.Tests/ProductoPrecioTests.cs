using ResellManager.Application.DTOs;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

public sealed class ProductoPrecioTests
{
    [Fact]
    public async Task Producto_GuardaPrecioSugerido()
    {
        await using var test = await TestDatabase.CreateAsync();
        var input = CrearInput(test, 275m, "PROD-PRECIO");

        var result = await new ProductoService(test.Db).CrearAsync(input);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(275m, result.Value!.PrecioSugerido);
    }

    [Fact]
    public async Task Producto_RechazaPrecioSugeridoNegativo()
    {
        await using var test = await TestDatabase.CreateAsync();
        var input = CrearInput(test, -1m, "PROD-NEGATIVO");

        var result = await new ProductoService(test.Db).CrearAsync(input);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            "precio sugerido",
            result.ErrorMessage!,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static ProductoInput CrearInput(TestDatabase test, decimal precio, string codigo) =>
        new(
            codigo,
            null,
            "Producto con precio",
            null,
            null,
            null,
            null,
            null,
            precio,
            test.Categoria.Id
        );
}
