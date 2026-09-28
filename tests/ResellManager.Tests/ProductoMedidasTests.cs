using System.ComponentModel.DataAnnotations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ResellManager.Application.DTOs;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Productos;

namespace ResellManager.Tests;

public sealed class ProductoMedidasTests
{
    public static IEnumerable<object[]> CasosMedidas()
    {
        yield return [null!, null!, true];
        yield return [100m, null!, true];
        yield return [null!, 500m, true];
        yield return [100m, 500m, false];
        yield return [0m, null!, false];
        yield return [-1m, null!, false];
        yield return [null!, 0m, false];
        yield return [null!, -1m, false];
    }

    [Theory]
    [MemberData(nameof(CasosMedidas))]
    public async Task Crear_ValidaMedidas(decimal? contenidoMl, decimal? pesoGramos, bool valido)
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new ProductoService(test.Db);

        var resultado = await servicio.CrearAsync(Input(test, contenidoMl, pesoGramos));

        Assert.Equal(valido, resultado.IsSuccess);
        if (valido)
        {
            Assert.Equal(contenidoMl, resultado.Value!.ContenidoMl);
            Assert.Equal(pesoGramos, resultado.Value.PesoGramos);
            var guardado = await test.Db.Productos.AsNoTracking()
                .SingleAsync(x => x.Id == resultado.Value.Id);
            Assert.Equal(contenidoMl, guardado.ContenidoMl);
            Assert.Equal(pesoGramos, guardado.PesoGramos);
        }
        else
        {
            Assert.NotNull(resultado.ErrorMessage);
            Assert.Equal(1, await test.Db.Productos.CountAsync());
        }
    }

    [Theory]
    [MemberData(nameof(CasosMedidas))]
    public async Task Editar_ValidaMedidas(decimal? contenidoMl, decimal? pesoGramos, bool valido)
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new ProductoService(test.Db);

        var resultado = await servicio.EditarAsync(
            test.Producto.Id,
            Input(test, contenidoMl, pesoGramos));

        Assert.Equal(valido, resultado.IsSuccess);
        test.Db.ChangeTracker.Clear();
        var guardado = await test.Db.Productos.AsNoTracking()
            .SingleAsync(x => x.Id == test.Producto.Id);
        Assert.Equal(valido ? contenidoMl : null, guardado.ContenidoMl);
        Assert.Equal(valido ? pesoGramos : null, guardado.PesoGramos);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public async Task Crear_ValidaLongitudPresentacion(int longitud, bool valido)
    {
        await using var test = await TestDatabase.CreateAsync();
        var resultado = await new ProductoService(test.Db).CrearAsync(
            Input(test, presentacion: new string('P', longitud)));

        Assert.Equal(valido, resultado.IsSuccess);
        if (valido)
            Assert.Equal(longitud, resultado.Value!.Presentacion!.Length);
    }

    [Fact]
    public async Task Editar_ValidaLongitudPresentacion()
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new ProductoService(test.Db);
        var resultado = await servicio.EditarAsync(
            test.Producto.Id,
            Input(test, presentacion: new string('P', 101)));

        Assert.False(resultado.IsSuccess);
        Assert.Null((await test.Db.Productos.AsNoTracking()
            .SingleAsync(x => x.Id == test.Producto.Id)).Presentacion);
    }

    [Fact]
    public void Formulario_ConservaMedidasYPresentacionEnContrato()
    {
        var modelo = new ProductoFormModel
        {
            Nombre = "Perfume",
            CategoriaId = 1,
            ContenidoMl = 100m,
            Presentacion = "Frasco",
        };

        var input = modelo.ToInput();
        Assert.Equal(100m, input.ContenidoMl);
        Assert.Null(input.PesoGramos);
        Assert.Equal("Frasco", input.Presentacion);

        var dto = new ProductoDto(1, "PROD-1", null, "Perfume", null, null, null,
            null, null, 0m, 1, "General", 100m, null, "Frasco");
        var desdeDto = ProductoFormModel.FromDto(dto);
        Assert.Equal(100m, desdeDto.ContenidoMl);
        Assert.Null(desdeDto.PesoGramos);
        Assert.Equal("Frasco", desdeDto.Presentacion);
    }

    [Theory]
    [MemberData(nameof(CasosMedidas))]
    public void Formulario_ValidaMedidas(decimal? contenidoMl, decimal? pesoGramos, bool valido)
    {
        var modelo = new ProductoFormModel
        {
            Nombre = "Producto",
            CategoriaId = 1,
            ContenidoMl = contenidoMl,
            PesoGramos = pesoGramos,
        };

        Assert.Equal(valido, Validator.TryValidateObject(
            modelo, new ValidationContext(modelo), [], validateAllProperties: true));
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Formulario_ValidaLongitudPresentacion(int longitud, bool valido)
    {
        var modelo = new ProductoFormModel
        {
            Nombre = "Producto",
            CategoriaId = 1,
            Presentacion = new string('P', longitud),
        };

        Assert.Equal(valido, Validator.TryValidateObject(
            modelo, new ValidationContext(modelo), [], validateAllProperties: true));
    }

    [Fact]
    public void Conversiones_UsanUnidadesCanonicasYRedondeoExplicito()
    {
        Assert.Equal(125.25m, ProductoMedidasConversion.AContenidoMl(125.25m, UnidadVolumen.Ml));
        Assert.Equal(1500.00m, ProductoMedidasConversion.AContenidoMl(1.5m, UnidadVolumen.L));
        Assert.Equal(250.25m, ProductoMedidasConversion.APesoGramos(250.25m, UnidadPeso.G));
        Assert.Equal(2000.00m, ProductoMedidasConversion.APesoGramos(2m, UnidadPeso.Kg));
        Assert.Equal(2267.96m, ProductoMedidasConversion.APesoGramos(5m, UnidadPeso.Lb));
        Assert.Equal(0.01m, ProductoMedidasConversion.AContenidoMl(0.005m, UnidadVolumen.Ml));
        Assert.Equal(0.01m, ProductoMedidasConversion.APesoGramos(0.005m, UnidadPeso.G));
        Assert.Null(ProductoMedidasConversion.AContenidoMl(null, UnidadVolumen.L));
        Assert.Null(ProductoMedidasConversion.APesoGramos(null, UnidadPeso.Lb));
    }

    [Fact]
    public void Conversiones_CarganUnidadComodaParaEditar()
    {
        Assert.Equal((999.99m, UnidadVolumen.Ml),
            ProductoMedidasConversion.VolumenParaEditar(999.99m));
        Assert.Equal((1.5m, UnidadVolumen.L),
            ProductoMedidasConversion.VolumenParaEditar(1500m));
        Assert.Equal((999.99m, UnidadPeso.G),
            ProductoMedidasConversion.PesoParaEditar(999.99m));
        Assert.Equal((2m, UnidadPeso.Kg),
            ProductoMedidasConversion.PesoParaEditar(2000m));
        Assert.Equal((null, UnidadVolumen.Ml),
            ProductoMedidasConversion.VolumenParaEditar(null));
        Assert.Equal((null, UnidadPeso.G),
            ProductoMedidasConversion.PesoParaEditar(null));
    }

    [Fact]
    public void Formulario_ToInput_ConvierteMedidasSinPersistirUnidad()
    {
        var modelo = new ProductoFormModel { Nombre = "Producto", CategoriaId = 1 };

        modelo.Volumen = 1.5m;
        modelo.VolumenUnidad = UnidadVolumen.L;
        Assert.Equal(1500.00m, modelo.ToInput().ContenidoMl);
        Assert.Null(modelo.ToInput().PesoGramos);

        modelo.Volumen = null;
        modelo.Peso = 2m;
        modelo.PesoUnidad = UnidadPeso.Kg;
        Assert.Null(modelo.ToInput().ContenidoMl);
        Assert.Equal(2000.00m, modelo.ToInput().PesoGramos);

        modelo.PesoUnidad = UnidadPeso.Lb;
        modelo.Peso = 5m;
        Assert.Equal(2267.96m, modelo.ToInput().PesoGramos);

        modelo.Peso = null;
        Assert.Null(modelo.ToInput().ContenidoMl);
        Assert.Null(modelo.ToInput().PesoGramos);
    }

    [Fact]
    public void Formulario_FromDto_ConservaValorCanonicoAlEditar()
    {
        var dto = new ProductoDto(1, "PROD-1", null, "Producto", null, null, null,
            null, null, 0m, 1, "General", null, 2267.96m, "Bolsa");

        var modelo = ProductoFormModel.FromDto(dto);

        Assert.Equal(2.26796m, modelo.Peso);
        Assert.Equal(UnidadPeso.Kg, modelo.PesoUnidad);
        Assert.Equal(2267.96m, modelo.ToInput().PesoGramos);
        Assert.Equal("Bolsa", modelo.ToInput().Presentacion);
    }
    [Fact]
    public async Task BaseDeDatos_RechazaEstadosInvalidosSinServicio()
    {
        await using var test = await TestDatabase.CreateAsync();

        foreach (var (contenidoMl, pesoGramos, presentacion) in new[]
        {
            (0m as decimal?, null as decimal?, null as string),
            (-1m, null, null),
            (null, 0m, null),
            (null, -1m, null),
            (100m, 500m, null),
            (null, null, new string('P', 101)),
        })
        {
            test.Producto.ContenidoMl = contenidoMl;
            test.Producto.PesoGramos = pesoGramos;
            test.Producto.Presentacion = presentacion;
            await Assert.ThrowsAsync<DbUpdateException>(() => test.Db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Migracion_ConservaProductoExistenteYAplicaRestricciones()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ResellManagerDbContext>()
            .UseSqlite(connection).Options;
        await using var db = new ResellManagerDbContext(options);
        var migrator = db.Database.GetService<IMigrator>();

        await migrator.MigrateAsync("20260901043948_AddCanalVentaToPedido");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Categorias (Nombre) VALUES ('General')");
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Productos
                (CodigoInterno, Nombre, Descripcion, PrecioSugerido, CategoriaId)
            VALUES ('PROD-HISTORICO', 'Producto histórico', 'Sin alteraciones', 75.5, 1)
            """);

        await migrator.MigrateAsync();

        var producto = await db.Productos.AsNoTracking()
            .SingleAsync(x => x.CodigoInterno == "PROD-HISTORICO");
        Assert.Equal("Producto histórico", producto.Nombre);
        Assert.Equal("Sin alteraciones", producto.Descripcion);
        Assert.Equal(75.5m, producto.PrecioSugerido);
        Assert.Null(producto.ContenidoMl);
        Assert.Null(producto.PesoGramos);
        Assert.Null(producto.Presentacion);

        foreach (var sql in new[]
        {
            "UPDATE Productos SET ContenidoMl = 0 WHERE Id = 1",
            "UPDATE Productos SET ContenidoMl = -1 WHERE Id = 1",
            "UPDATE Productos SET PesoGramos = 0 WHERE Id = 1",
            "UPDATE Productos SET PesoGramos = -1 WHERE Id = 1",
            "UPDATE Productos SET ContenidoMl = 100, PesoGramos = 500 WHERE Id = 1",
        })
        {
            await Assert.ThrowsAsync<SqliteException>(
                () => db.Database.ExecuteSqlRawAsync(sql));
        }

        var presentacionLarga = new string('P', 101);
        await Assert.ThrowsAsync<SqliteException>(
            () => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Productos SET Presentacion = {presentacionLarga} WHERE Id = {producto.Id}"));
    }

    private static ProductoInput Input(
        TestDatabase test,
        decimal? contenidoMl = null,
        decimal? pesoGramos = null,
        string? presentacion = null) =>
        new(null, "Producto con medidas", null, null, null, null, null,
            100m, test.Categoria.Id, contenidoMl, pesoGramos, presentacion);
}
