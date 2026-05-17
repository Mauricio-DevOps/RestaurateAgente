using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurantes.Web.Auth;
using Restaurantes.Web.Data;
using Restaurantes.Web.Models;
using Restaurantes.Web.Services;

namespace Restaurantes.Web.Controllers;

public sealed class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly ExternalUrlResolver _externalUrlResolver;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        ExternalUrlResolver externalUrlResolver)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
        _externalUrlResolver = externalUrlResolver;
    }

    [HttpGet("/login")]
    public IActionResult Login(string? next = null, string? returnUrl = null)
    {
        var target = ResolveNext(next, returnUrl);
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(GetDashboardPathForUser(target));
        }

        return View(new LoginInput { Next = target });
    }

    [HttpPost("/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInput input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var user = await _userManager.FindByEmailAsync(input.Email);
        if (user is null || user.ProfileStatus != EntityStatus.ACTIVE)
        {
            ModelState.AddModelError("", "Email ou senha invÃ¡lidos.");
            return View(input);
        }

        var result = await _signInManager.PasswordSignInAsync(user, input.Password, isPersistent: false, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Email ou senha invÃ¡lidos.");
            return View(input);
        }

        var roles = await _userManager.GetRolesAsync(user);
        var accessMode = await GetRestaurantAccessModeAsync(user);
        var next = SanitizeNext(input.Next);
        var target = GetDashboardPathForUser(roles, accessMode);
        return Redirect(next is not null && CanUserAccessPath(roles, accessMode, next) ? next : target);
    }

    [HttpPost("/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Redirect(_externalUrlResolver.BuildWhatsAppAdminUrl("/Account/LogoutRemote"));
    }

    [HttpGet("/logout-remote")]
    public async Task<IActionResult> LogoutRemote()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    private string GetDashboardPathForUser(string? requestedNext = null)
    {
        if (User.IsInRole(AppRoles.Master))
        {
            return CanUserAccessPath([AppRoles.Master], RestaurantAccessMode.Ambos, requestedNext)
                ? requestedNext!
                : "/master";
        }

        if (User.IsInRole(AppRoles.AdminRestaurante))
        {
            var accessMode = RestaurantPortalAccess.GetAccessMode(User);
            return CanUserAccessPath([AppRoles.AdminRestaurante], accessMode, requestedNext)
                ? requestedNext!
                : RestaurantPortalAccess.GetPreferredAdminPath(accessMode);
        }

        if (User.IsInRole(AppRoles.Garcom))
        {
            return CanUserAccessPath([AppRoles.Garcom], RestaurantAccessMode.Ambos, requestedNext)
                ? requestedNext!
                : "/garcom";
        }

        return "/";
    }

    private static string GetDashboardPathForUser(IList<string> roles, RestaurantAccessMode accessMode)
    {
        if (roles.Contains(AppRoles.Master))
        {
            return "/master";
        }

        if (roles.Contains(AppRoles.AdminRestaurante))
        {
            return RestaurantPortalAccess.GetPreferredAdminPath(accessMode);
        }

        if (roles.Contains(AppRoles.Garcom))
        {
            return "/garcom";
        }

        return "/";
    }

    private static bool CanUserAccessPath(IList<string> roles, RestaurantAccessMode accessMode, string? path)
    {
        return RestaurantPortalAccess.CanAccessPath(roles, accessMode, path);
    }

    private static string? ResolveNext(string? next, string? returnUrl)
    {
        return SanitizeNext(next) ?? SanitizeNext(returnUrl);
    }

    private static string? SanitizeNext(string? next)
    {
        if (string.IsNullOrWhiteSpace(next) || !next.StartsWith('/') || next.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        return next.Equals("/login", StringComparison.OrdinalIgnoreCase) ? null : next;
    }

    private async Task<RestaurantAccessMode> GetRestaurantAccessModeAsync(ApplicationUser user)
    {
        if (!user.RestaurantId.HasValue)
        {
            return RestaurantAccessMode.Ambos;
        }

        return await _db.Restaurants
            .Where(restaurant => restaurant.Id == user.RestaurantId.Value)
            .Select(restaurant => restaurant.AccessMode)
            .FirstOrDefaultAsync();
    }
}
