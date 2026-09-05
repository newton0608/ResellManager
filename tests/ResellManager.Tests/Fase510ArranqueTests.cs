using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ResellManager.Infrastructure.Persistence;

namespace ResellManager.Tests;

[Collection("Integración web")]
public sealed class Fase510ArranqueTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("solo-correo@example.test", "")]
    [InlineData("", "Solo-Contrasena1!")]
    public async Task ArranqueSinCredencialesCompletas_MigraSQLiteLimpioSinCrearUsuarios(
        string correo, string contrasena)
    {
        using var entorno = new EntornoArranque();
        using var app = entorno.CrearApp(correo, contrasena);
        using var cliente = app.CreateClient();
        (await cliente.GetAsync("/login")).EnsureSuccessStatusCode();

        await using var scope = app.Services.CreateAsyncScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        Assert.Equal(correo, config["UsuarioInicial:Correo"]);
        Assert.Equal(contrasena, config["UsuarioInicial:Contrasena"]);
        var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
        Assert.Equal(config.GetConnectionString("ResellManager"), db.Database.GetConnectionString());
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.Pedidos.ToListAsync());
        Assert.Empty(await db.UnidadesInventario.ToListAsync());
    }

    [Fact]
    public async Task ReinicioSinCredencialesYConOtraContrasena_NoModificaUsuarioExistente()
    {
        using var entorno = new EntornoArranque();
        string hashOriginal;
        using (var primera = entorno.CrearApp(AplicacionAutenticacionFactory.CorreoUsuario,
            AplicacionAutenticacionFactory.ContrasenaValida))
        {
            using var cliente = primera.CreateClient();
            await using var scope = primera.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
            hashOriginal = (await db.Users.SingleAsync()).PasswordHash!;
        }

        using (var sinCredenciales = entorno.CrearApp("", ""))
        {
            using var cliente = sinCredenciales.CreateClient();
            await ComprobarUsuarioAsync(sinCredenciales, hashOriginal);
        }

        using (var otraContrasena = entorno.CrearApp(
            AplicacionAutenticacionFactory.CorreoUsuario, "Otra-Contrasena2!"))
        {
            using var cliente = otraContrasena.CreateClient();
            await ComprobarUsuarioAsync(otraContrasena, hashOriginal);
        }
    }

    [Theory]
    [InlineData("wwwroot")]
    [InlineData("wwwroot/recibos-privados")]
    [InlineData("App_Data/../wwwroot/recibos-privados")]
    public void ArranqueRechazaComprobantesDentroDelDirectorioPublico(string ruta)
    {
        using var entorno = new EntornoArranque();
        using var app = entorno.CrearApp("", "", ruta);
        var error = Assert.Throws<InvalidOperationException>(() => app.CreateClient());
        Assert.Contains("fuera de wwwroot", error.Message);
    }

    private static async Task ComprobarUsuarioAsync(WebApplicationFactory<Program> app, string hash)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
        var usuario = await db.Users.SingleAsync();
        Assert.Equal(hash, usuario.PasswordHash);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True(await usuarios.CheckPasswordAsync(usuario,
            AplicacionAutenticacionFactory.ContrasenaValida));
    }

    private sealed class EntornoArranque : IDisposable
    {
        private readonly string directorio = Path.Combine(
            Path.GetTempPath(), $"resellmanager-cierre-arranque-{Guid.NewGuid():N}");

        public EntornoArranque() => Directory.CreateDirectory(directorio);

        public WebApplicationFactory<Program> CrearApp(string correo, string contrasena,
            string? rutaComprobantes = null) => new ArranqueFactory(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ResellManager"] =
                    $"Data Source={Path.Combine(directorio, "prueba.db")};Pooling=False",
                ["UsuarioInicial:Correo"] = correo,
                ["UsuarioInicial:Contrasena"] = contrasena,
                ["AlmacenamientoComprobantes:DirectorioBase"] =
                    rutaComprobantes ?? Path.Combine(directorio, "privado")
            });

        public void Dispose() => Directory.Delete(directorio, recursive: true);
    }

    private sealed class ArranqueFactory(Dictionary<string, string?> configuracion)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(configuracion));
        }
    }
}
