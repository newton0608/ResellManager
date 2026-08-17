using Microsoft.AspNetCore.Identity;

namespace ResellManager.Web.Identity;

public sealed class NoOpEmailSender : IEmailSender<IdentityUser>
{
    public Task SendConfirmationLinkAsync(
        IdentityUser user,
        string email,
        string confirmationLink)
    {
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(
        IdentityUser user,
        string email,
        string resetLink)
    {
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(
        IdentityUser user,
        string email,
        string resetCode)
    {
        return Task.CompletedTask;
    }
}