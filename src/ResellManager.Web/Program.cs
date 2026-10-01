using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using ResellManager.Application.Interfaces;
using ResellManager.Infrastructure;
using ResellManager.Infrastructure.Storage;
using ResellManager.Web.Components;
using ResellManager.Web.Identity;
using ResellManager.Web.Hosting;
using ResellManager.Web.Inicializacion;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.AddAuthorization();

builder.Services.AddProductionHosting(builder.Configuration);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("Login", context => RateLimitPartition.GetFixedWindowLimiter(
        // Normalizar IPv4/IPv4-mapped; solo usar la IP resuelta por Forwarded Headers.
        context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

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

builder.Services.AddOptions<AlmacenamientoImagenesProductoOptions>()
    .Configure<IConfiguration, IWebHostEnvironment>((options, configuracion, entorno) =>
    {
        var configurado = configuracion[$"{AlmacenamientoImagenesProductoOptions.Seccion}:DirectorioBase"]
            ?? "App_Data/productos";
        var directorio = Path.IsPathRooted(configurado)
            ? Path.GetFullPath(configurado)
            : Path.GetFullPath(Path.Combine(entorno.ContentRootPath, configurado));
        var publico = Path.GetFullPath(entorno.WebRootPath
            ?? Path.Combine(entorno.ContentRootPath, "wwwroot"));
        var comparacion = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (directorio.Equals(publico, comparacion)
            || directorio.StartsWith(Path.TrimEndingDirectorySeparator(publico)
                + Path.DirectorySeparatorChar, comparacion))
            throw new InvalidOperationException("El almacenamiento de imágenes debe estar fuera de wwwroot.");
        options.DirectorioBase = directorio;
    });

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.SlidingExpiration = true;
});

var app = builder.Build();

// Validar la configuración definitiva del host antes de abrir SQLite o crear usuarios.
_ = app.Services.GetRequiredService<IOptions<AlmacenamientoComprobantesOptions>>().Value;
_ = app.Services.GetRequiredService<IOptions<AlmacenamientoImagenesProductoOptions>>().Value;
_ = app.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
_ = app.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>>().Value;
await app.InicializarBaseDatosAsync();
await app.CrearUsuarioInicialSiEstaConfiguradoAsync();

app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers.XFrameOptions = "DENY";
        // Conservar políticas específicas, como CSP sandbox en comprobantes.
        context.Response.Headers.TryAdd("Content-Security-Policy", "frame-ancestors 'none'");
        return Task.CompletedTask;
    });
    await next();
});

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
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/health", () => Results.Text("OK")).AllowAnonymous();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode(options => options.ContentSecurityFrameAncestorsPolicy = "'none'");

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
}).AllowAnonymous().RequireRateLimiting("Login");

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

app.MapGet("/productos/{productoId:int}/imagen", async (
    int productoId,
    IProductoService productos,
    IAlmacenamientoImagenesProducto almacenamiento,
    HttpContext contexto,
    CancellationToken ct) =>
{
    var producto = await productos.ObtenerPorIdAsync(productoId, ct);
    if (!producto.IsSuccess || string.IsNullOrWhiteSpace(producto.Value?.ImagenPrincipalRuta))
        return Results.NotFound();
    var imagen = await almacenamiento.AbrirLecturaAsync(producto.Value.ImagenPrincipalRuta, ct);
    if (!imagen.IsSuccess || imagen.Value is null)
        return Results.NotFound();
    contexto.Response.Headers.CacheControl = "private, no-store";
    contexto.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.File(imagen.Value.Contenido, imagen.Value.ContentType);
}).RequireAuthorization();

app.Run();

public partial class Program;
