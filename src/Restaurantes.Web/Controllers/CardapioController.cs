using Microsoft.AspNetCore.Mvc;
using Restaurantes.Web.Services;

namespace Restaurantes.Web.Controllers;

public sealed class CardapioController : Controller
{
    private readonly RestaurantService _restaurantService;

    public CardapioController(RestaurantService restaurantService)
    {
        _restaurantService = restaurantService;
    }

    [HttpGet("cardapio/{restaurantId:guid}")]
    public async Task<IActionResult> Public(Guid restaurantId, [FromQuery(Name = "mesa")] string? tableNumber)
    {
        var view = await _restaurantService.GetPublicMenuAsync(restaurantId, tableNumber);
        return view is null ? NotFound() : View(view);
    }
}
