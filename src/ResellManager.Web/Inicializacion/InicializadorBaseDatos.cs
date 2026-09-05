using Microsoft.EntityFrameworkCore;
using ResellManager.Infrastructure.Persistence;

namespace ResellManager.Web.Inicializacion;

public static class InicializadorBaseDatos
{
    public static async Task InicializarBaseDatosAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
        // El esquema se actualiza incluso después de retirar las credenciales del usuario inicial.
        await db.Database.MigrateAsync();
    }
}
