using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Restaurantes.Web.Auth;
using Restaurantes.Web.Models;
using Restaurantes.Web.Services;

namespace Restaurantes.Web.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.AdminRestaurante)]
[Route("api/restaurante/delivery")]
public sealed class ApiRestauranteDeliveryController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RestaurantService _restaurantService;

    public ApiRestauranteDeliveryController(UserManager<ApplicationUser> userManager, RestaurantService restaurantService)
    {
        _userManager = userManager;
        _restaurantService = restaurantService;
    }

    [HttpGet("orders")]
    public async Task<IActionResult> Orders()
    {
        return Ok(new { orders = await _restaurantService.GetDeliveryOrdersAsync(await GetRestaurantIdAsync()) });
    }

    [HttpPost("orders/status")]
    public async Task<IActionResult> UpdateStatus(DeliveryOrderStatusInput input)
    {
        try
        {
            await _restaurantService.UpdateDeliveryOrderStatusAsync(await GetRestaurantIdAsync(), input);
            return Ok(new { ok = true });
        }
        catch (InvalidOperationException error)
        {
            return BadRequest(new { error = error.Message });
        }
    }

    private async Task<Guid> GetRestaurantIdAsync()
    {
        var user = await _userManager.GetUserAsync(User) ?? throw new InvalidOperationException("Usuario nao autenticado.");
        return user.RestaurantId ?? throw new InvalidOperationException("Usuario sem restaurante.");
    }
}
