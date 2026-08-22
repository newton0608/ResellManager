using Microsoft.AspNetCore.Identity;
using ResellManager.Infrastructure;
using ResellManager.Web.Components;
using ResellManager.Web.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.AddAuthorization();

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<IEmailSender<IdentityUser>, NoOpEmailSender>();

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

await app.CrearUsuarioInicialSiEstaConfiguradoAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
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

app.Run();

public partial class Program;
