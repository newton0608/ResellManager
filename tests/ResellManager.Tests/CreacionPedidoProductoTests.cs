using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Pedidos;
using ResellManager.Web.Components.Productos;

namespace ResellManager.Tests;

public sealed class CreacionPedidoProductoTests
{
    [Fact]
    public void TiposManuales_CompartenListaSinCambiarEnumPersistido()
    {
        Assert.Equal(new[] { TipoPedido.Importacion, TipoPedido.Catalogo, TipoPedido.Apartado },
            TiposPedidoManual.Permitidos);
        Assert.Same(TiposPedidoManual.Permitidos, PedidoFormModel.TiposManuales);
        Assert.Equal(new[] { 0, 1, 2, 3 }, Enum.GetValues<TipoPedido>().Select(x => (int)x));
        Assert.Contains(TipoPedido.VentaDirecta, Enum.GetValues<TipoPedido>());
        Assert.DoesNotContain(TipoPedido.VentaDirecta, PedidoFormModel.TiposManuales);
    }

    [Theory]
    [InlineData(TipoPedido.Importacion, true)]
    [InlineData(TipoPedido.Catalogo, true)]
    [InlineData(TipoPedido.Apartado, true)]
    [InlineData(TipoPedido.VentaDirecta, false)]
    [InlineData((TipoPedido)99, false)]
    public async Task PedidoManual_ValidaEnModeloYBackendInclusoSinFormulario(TipoPedido tipo, bool permitido)
    {
        var modelo = new PedidoFormModel { ClienteId = 1, TipoPedido = tipo };
        var errores = new List<ValidationResult>();
        Assert.Equal(permitido, Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, true));
        if (!permitido)
            Assert.Contains(errores, x => x.MemberNames.Contains(nameof(PedidoFormModel.TipoPedido)));

        await using var test = await TestDatabase.CreateAsync();
        // Invocación directa al contrato manual: no depende del selector ni de validar el modelo.
        var resultado = await new PedidoService(test.Db).CrearManualAsync(new PedidoInput(
            CodigosInternos.CrearCodigoPedido(), new DateOnly(2026, 9, 7), tipo,
            CanalVenta.Presencial, test.Cliente.Id, null,
            [new DetallePedidoInput(test.Producto.Id, 1, 100m, null)]));
        Assert.Equal(permitido, resultado.IsSuccess);
        if (permitido)
        {
            Assert.Equal(tipo, resultado.Value!.TipoPedido);
            Assert.Equal(1, await test.Db.Pedidos.CountAsync());
        }
        else
        {
            Assert.Equal(TiposPedidoManual.Validar(tipo), resultado.ErrorMessage);
            Assert.Empty(await test.Db.Pedidos.ToListAsync());
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("7401234567890")]
    public async Task ProductoNuevo_GeneraCodigoUnicoEnBackendYConservaBusquedas(string? codigoBarras)
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new ProductoService(test.Db);
        var input = Input(test.Categoria.Id, codigoBarras);
        var primero = await servicio.CrearAsync(input);
        var segundo = await servicio.CrearAsync(input);
        Assert.True(primero.IsSuccess, primero.ErrorMessage);
        Assert.True(segundo.IsSuccess, segundo.ErrorMessage);
        Assert.Null(typeof(ProductoInput).GetProperty("CodigoInterno"));
        Assert.Matches("^PRO-[A-F0-9]{32}$", primero.Value!.CodigoInterno);
        Assert.Matches("^PRO-[A-F0-9]{32}$", segundo.Value!.CodigoInterno);
        Assert.NotEqual(primero.Value.CodigoInterno, segundo.Value.CodigoInterno);
        Assert.Equal(codigoBarras, primero.Value.CodigoBarras);
        test.Db.ChangeTracker.Clear();
        Assert.Equal(primero.Value.CodigoInterno,
            (await test.Db.Productos.SingleAsync(x => x.Id == primero.Value.Id)).CodigoInterno);
        Assert.Equal(primero.Value.Id, Assert.Single(await servicio.BuscarAsync(primero.Value.CodigoInterno)).Id);
        Assert.Equal(2, (await servicio.BuscarAsync(input.Nombre)).Count);
        if (codigoBarras is not null)
            Assert.Equal(2, (await servicio.BuscarAsync(codigoBarras)).Count);

        var editado = await servicio.EditarAsync(primero.Value.Id, input with { Nombre = "Nombre actualizado" });
        Assert.True(editado.IsSuccess, editado.ErrorMessage);
        Assert.Equal(primero.Value.CodigoInterno, editado.Value!.CodigoInterno);
        test.Db.ChangeTracker.Clear();
        Assert.Equal(primero.Value.CodigoInterno,
            (await test.Db.Productos.SingleAsync(x => x.Id == primero.Value.Id)).CodigoInterno);
    }

    [Theory]
    [InlineData("CAM-001")]
    [InlineData("cam-001 ")]
    public async Task EditarHistorico_PreservaCodigoExactoSinNormalizar(string codigo)
    {
        await using var test = await TestDatabase.CreateAsync();
        test.Producto.CodigoInterno = codigo;
        await test.Db.SaveChangesAsync();
        var servicio = new ProductoService(test.Db);
        var anterior = await servicio.ObtenerPorIdAsync(test.Producto.Id);
        var modelo = ProductoFormModel.FromDto(anterior.Value!);
        Assert.Null(typeof(ProductoFormModel).GetProperty("CodigoInterno"));
        modelo.Nombre = "Producto histórico actualizado";
        modelo.CodigoBarras = "7409876543210";
        var resultado = await servicio.EditarAsync(test.Producto.Id, modelo.ToInput());
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal(codigo, resultado.Value!.CodigoInterno);
        Assert.Equal(modelo.CodigoBarras, resultado.Value.CodigoBarras);
        test.Db.ChangeTracker.Clear();
        Assert.Equal(codigo, (await test.Db.Productos.SingleAsync()).CodigoInterno);
        Assert.Equal(test.Producto.Id, Assert.Single(await servicio.BuscarAsync(codigo)).Id);
    }

    [Fact]
    public async Task Producto_ConservaColumnaRequeridaEIndiceUnicoSinCambiosDeModelo()
    {
        await using var test = await TestDatabase.CreateAsync();
        Assert.False(test.Db.Database.HasPendingModelChanges());
        var entidad = test.Db.Model.FindEntityType(typeof(Producto))!;
        Assert.False(entidad.FindProperty(nameof(Producto.CodigoInterno))!.IsNullable);
        Assert.Contains(entidad.GetIndexes(), x => x.IsUnique
            && x.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Producto.CodigoInterno) }));
        test.Db.Productos.Add(new Producto
        {
            CodigoInterno = test.Producto.CodigoInterno,
            Nombre = "Duplicado rechazado",
            CategoriaId = test.Categoria.Id,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => test.Db.SaveChangesAsync());
    }

    private static ProductoInput Input(int categoriaId, string? codigoBarras) =>
        new(codigoBarras, "Producto generado", null, null, null, null, null, 100m, categoriaId);
}
