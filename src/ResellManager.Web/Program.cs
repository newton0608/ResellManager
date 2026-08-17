using Microsoft.AspNetCore.Identity;
using ResellManager.Infrastructure;
using ResellManager.Web.Components;
using ResellManager.Web.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.AddAuthorization();

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<IEmailSender<IdentityUser>, NoOpEmailSender>();

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapGroup("/account").MapIdentityApi<IdentityUser>();

app.Run();
