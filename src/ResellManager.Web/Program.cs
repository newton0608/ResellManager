using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using ResellManager.Application.Interfaces;
using ResellManager.Infrastructure;
using ResellManager.Infrastructure.Storage;
using ResellManager.Web.Components;
using ResellManager.Web.Identity;
using ResellManager.Web.Inicializacion;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.AddAuthorization();

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<IEmailSender<IdentityUser>, NoOpEmailSender>();

builder.Services.AddOptions<AlmacenamientoComprobantesOptions>()
    .Configure<IConfiguration, IWebHostEnvironment>((options, configuracion, entorno) =>
    {
        var directorioConfigurado =
            configuracion[$"{AlmacenamientoComprobantesOptions.Seccion}:DirectorioBase"] ?? "App_Data";
        var directorioComprobantes = Path.IsPathRooted(directorioConfigurado)
            ? Path.GetFullPath(directorioConfigurado)
            : Path.GetFullPath(Path.Combine(entorno.ContentRootPath, directorioConfigurado));
        var directorioPublico = Path.GetFullPath(entorno.WebRootPath
            ?? Path.Combine(entorno.ContentRootPath, "wwwroot"));
        var comparacionRutas = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (directorioComprobantes.Equals(directorioPublico, comparacionRutas)
            || directorioComprobantes.StartsWith(
                Path.TrimEndingDirectorySeparator(directorioPublico) + Path.DirectorySeparatorChar,
                comparacionRutas))
        {
            throw new InvalidOperationException("El almacenamiento de comprobantes debe estar fuera de wwwroot.");
        }

        options.DirectorioBase = directorioComprobantes;
    });

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.SlidingExpiration = true;
});

var app = builder.Build();

// Validar la configuración definitiva del host antes de abrir SQLite o crear usuarios.
_ = app.Services.GetRequiredService<IOptions<AlmacenamientoComprobantesOptions>>().Value;
await app.InicializarBaseDatosAsync();
await app.CrearUsuarioInicialSiEstaConfiguradoAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseStatusCodePagesWithReExecute("/no-encontrado");
app.Use(async (contexto, siguiente) =>
{
    await siguiente();
    // La página de navegación no sustituye errores de formularios ni otros estados HTTP.
    if (contexto.Response.StatusCode != StatusCodes.Status404NotFound
        || !(HttpMethods.IsGet(contexto.Request.Method) || HttpMethods.IsHead(contexto.Request.Method)))
    {
        var paginasEstado = contexto.Features.Get<IStatusCodePagesFeature>();
        if (paginasEstado is not null)
            paginasEstado.Enabled = false;
    }
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/account/login", async (
    [Microsoft.AspNetCore.Mvc.FromForm] string correo,
    [Microsoft.AspNetCore.Mvc.FromForm] string contrasena,
    [Microsoft.AspNetCore.Mvc.FromForm] string? recordar,
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager) =>
{
    var usuario = await userManager.FindByEmailAsync(correo.Trim());
    var resultado = usuario is null
        ? SignInResult.Failed
        : await signInManager.PasswordSignInAsync(
            usuario,
            contrasena,
            isPersistent: recordar is not null,
            lockoutOnFailure: true);

    return resultado.Succeeded
        ? Results.LocalRedirect("/")
        : Results.LocalRedirect("/login?error=credenciales");
}).AllowAnonymous();

app.MapPost("/account/logout", async (
    [Microsoft.AspNetCore.Mvc.FromForm] string confirmacion,
    SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.LocalRedirect("/login?sesionCerrada=true");
}).RequireAuthorization();

app.MapGet(
        "/comprobantes/{compraId:int}",
        async (
            int compraId,
            ICompraService compras,
            IAlmacenamientoComprobantes almacenamiento,
            HttpContext contexto,
            CancellationToken ct
        ) =>
        {
            var comprobante = await compras.ObtenerComprobanteAsync(compraId, ct);
            if (!comprobante.IsSuccess || comprobante.Value is null)
                return Results.NotFound();

            var archivo = await almacenamiento.AbrirLecturaAsync(
                comprobante.Value.RutaDocumento,
                ct
            );
            if (!archivo.IsSuccess || archivo.Value is null)
                return Results.NotFound();

            contexto.Response.Headers.XContentTypeOptions = "nosniff";
            contexto.Response.Headers.ContentSecurityPolicy = "sandbox";
            return Results.File(
                archivo.Value.Contenido,
                archivo.Value.ContentType,
                enableRangeProcessing: true
            );
        }
    )
    .RequireAuthorization();

app.Run();

public partial class Program;
