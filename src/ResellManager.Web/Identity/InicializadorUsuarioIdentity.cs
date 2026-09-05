using Microsoft.AspNetCore.Identity;

namespace ResellManager.Web.Identity;

public static class InicializadorUsuarioIdentity
{
    private const string ClaveCorreo = "UsuarioInicial:Correo";
    private const string ClaveContrasena = "UsuarioInicial:Contrasena";

    public static async Task CrearUsuarioInicialSiEstaConfiguradoAsync(this WebApplication app)
    {
        var correo = app.Configuration[ClaveCorreo]?.Trim();
        var contrasena = app.Configuration[ClaveContrasena];
        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(InicializadorUsuarioIdentity));

        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(contrasena))
        {
            logger.LogInformation(
                "No se creó el usuario inicial porque no están configuradas ambas claves {ClaveCorreo} y {ClaveContrasena}.",
                ClaveCorreo,
                ClaveContrasena);
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        if (await userManager.FindByEmailAsync(correo) is not null)
        {
            logger.LogInformation("El usuario inicial configurado ya existe; no se modificaron sus credenciales.");
            return;
        }

        var usuario = new IdentityUser
        {
            UserName = correo,
            Email = correo,
            EmailConfirmed = true
        };

        var resultado = await userManager.CreateAsync(usuario, contrasena);
        if (!resultado.Succeeded)
        {
            var errores = string.Join(", ", resultado.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"No fue posible crear el usuario inicial: {errores}");
        }

        logger.LogInformation("Se creó el usuario inicial configurado.");
    }
}
