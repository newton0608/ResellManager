using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ResellManager.Infrastructure.Persistence;

/// <summary>
/// Database context for identity and future ResellManager persistence.
/// </summary>
public sealed class ResellManagerDbContext(DbContextOptions<ResellManagerDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
}
