using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Restaurantes.Web.Auth;
using Restaurantes.Web.Models;
using Restaurantes.Web.Services;

namespace Restaurantes.Web.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.Garcom)]
[Route("api/garcom")]
public sealed class ApiGarcomController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RestaurantService _restaurantService;

    public ApiGarcomController(UserManager<ApplicationUser> userManager, RestaurantService restaurantService)
    {
        _userManager = userManager;
        _restaurantService = restaurantService;
    }

    [HttpGet("queue")]
    public async Task<IActionResult> Queue()
    {
        var restaurantId = await GetRestaurantIdAsync();
        var selectedWaiterId = GetSelectedWaiterId(restaurantId);
        return Ok(await _restaurantService.GetWaiterDashboardAsync(restaurantId, selectedWaiterId));
    }

    [HttpPost("events/status")]
    public async Task<IActionResult> UpdateStatus(UpdateOperationalEventStatusInput input)
    {
        var restaurantId = await GetRestaurantIdAsync();
        var selectedWaiterId = GetSelectedWaiterId(restaurantId);
        if (!selectedWaiterId.HasValue)
        {
            return BadRequest(new { error = "Selecione qual garçom você representa antes de atender os eventos." });
        }

        try
        {
            await _restaurantService.UpdateOperationalEventStatusAsync(restaurantId, selectedWaiterId.Value, input);
            return Ok(new { ok = true });
        }
        catch (InvalidOperationException error)
        {
            return BadRequest(new { error = error.Message });
        }
    }

    private Guid? GetSelectedWaiterId(Guid restaurantId)
    {
        var raw = Request.Cookies[GarcomController.GetWaiterCookieName(restaurantId)];
        return Guid.TryParse(raw, out var waiterId) ? waiterId : null;
    }

    private async Task<Guid> GetRestaurantIdAsync()
    {
        var user = await _userManager.GetUserAsync(User) ?? throw new InvalidOperationException("Usuário não autenticado.");
        return user.RestaurantId ?? throw new InvalidOperationException("Usuário sem restaurante.");
    }
}
