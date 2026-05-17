using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Restaurantes.Web.Auth;
using Restaurantes.Web.Models;
using Restaurantes.Web.Services;

namespace Restaurantes.Web.Controllers;

[Authorize(Roles = AppRoles.Garcom)]
[Route("garcom")]
public sealed class GarcomController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RestaurantService _restaurantService;

    public GarcomController(UserManager<ApplicationUser> userManager, RestaurantService restaurantService)
    {
        _userManager = userManager;
        _restaurantService = restaurantService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var restaurantId = await GetRestaurantIdAsync();
        var selectedWaiterId = GetSelectedWaiterId(restaurantId);
        return View(await _restaurantService.GetWaiterDashboardAsync(restaurantId, selectedWaiterId));
    }

    [HttpPost("selecionar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectWaiter(Guid waiterId)
    {
        var restaurantId = await GetRestaurantIdAsync();
        Response.Cookies.Append(GetWaiterCookieName(restaurantId), waiterId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return RedirectToAction(nameof(Index));
    }

    internal static string GetWaiterCookieName(Guid restaurantId) => $"restaurante_waiter_{restaurantId:N}";

    private Guid? GetSelectedWaiterId(Guid restaurantId)
    {
        var raw = Request.Cookies[GetWaiterCookieName(restaurantId)];
        return Guid.TryParse(raw, out var waiterId) ? waiterId : null;
    }

    private async Task<Guid> GetRestaurantIdAsync()
    {
        var user = await _userManager.GetUserAsync(User) ?? throw new InvalidOperationException("Usuário não autenticado.");
        return user.RestaurantId ?? throw new InvalidOperationException("Usuário sem restaurante.");
    }
}
