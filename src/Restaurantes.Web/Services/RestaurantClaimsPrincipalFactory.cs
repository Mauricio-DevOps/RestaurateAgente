using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Restaurantes.Web.Auth;
using Restaurantes.Web.Data;
using Restaurantes.Web.Models;

namespace Restaurantes.Web.Services;

public sealed class RestaurantClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    private readonly ApplicationDbContext _db;

    public RestaurantClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor,
        ApplicationDbContext db)
        : base(userManager, roleManager, optionsAccessor)
    {
        _db = db;
    }

    public override async Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var principal = await base.CreateAsync(user);
        if (principal.Identity is not ClaimsIdentity identity || !user.RestaurantId.HasValue)
        {
            return principal;
        }

        var accessMode = await _db.Restaurants
            .AsNoTracking()
            .Where(restaurant => restaurant.Id == user.RestaurantId.Value)
            .Select(restaurant => restaurant.AccessMode)
            .FirstOrDefaultAsync();

        AddOrReplaceClaim(identity, RestaurantPortalAccess.AccessModeClaimType, accessMode.ToString());
        return principal;
    }

    private static void AddOrReplaceClaim(ClaimsIdentity identity, string claimType, string value)
    {
        foreach (var claim in identity.FindAll(claimType).ToArray())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(claimType, value));
    }
}
