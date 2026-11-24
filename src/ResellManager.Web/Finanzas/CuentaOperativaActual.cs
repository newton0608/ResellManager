using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using ResellManager.Application.Interfaces;

namespace ResellManager.Web.Finanzas;

// Identity autentica; la lista de IDs permite una concesión explícita sin crear roles generales.
public sealed class CuentaOperativaActual(AuthenticationStateProvider estado, IHttpContextAccessor contexto,
    IConfiguration configuracion, UserManager<IdentityUser> usuarios) : ICuentaOperativa
{
    public async Task<CuentaOperativa?> ObtenerAsync(CancellationToken ct = default)
    {
        var principal = contexto.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            try { principal = (await estado.GetAuthenticationStateAsync()).User; }
            catch (InvalidOperationException) { return null; } // Sin petición ni circuito autenticado.
        }
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(id) || await usuarios.FindByIdAsync(id) is null) return null;
        var ids = configuracion.GetSection("SeguridadOperaciones:AdministradoresIds").Get<string[]>() ?? [];
        return new(id, ids.Contains(id, StringComparer.Ordinal));
    }
}
