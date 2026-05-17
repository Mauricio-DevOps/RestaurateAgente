using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Restaurantes.Web.Models;
using Restaurantes.Web.Options;
using Restaurantes.Web.Services;

namespace Restaurantes.Web.Controllers;

[ApiController]
[Route("api/internal/menu-items")]
public sealed class InternalMenuItemsController : ControllerBase
{
    private const string ServiceKeyHeaderName = "X-Internal-Service-Key";

    [HttpPost("sync-from-product")]
    public async Task<IActionResult> SyncFromProduct(
        [FromBody] MenuItemSyncFromProductRequest request,
        [FromServices] RestaurantService restaurantService,
        [FromServices] IOptions<InternalApiOptions> internalApiOptions)
    {
        if (!IsAuthorized(Request.Headers[ServiceKeyHeaderName].ToString(), internalApiOptions.Value.ServiceKey))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.StoreId) ||
            string.IsNullOrWhiteSpace(request.ProductId) ||
            string.IsNullOrWhiteSpace(request.Name) ||
            request.RetailPrice < 0)
        {
            return Problem(
                title: "Invalid menu item sync",
                detail: "StoreId, ProductId, Name and a non-negative RetailPrice are required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var item = await restaurantService.SyncMenuItemFromProductAsync(request);
            return Ok(item);
        }
        catch (InvalidOperationException error)
        {
            return Problem(
                title: "Menu item sync failed",
                detail: error.Message,
                statusCode: StatusCodes.Status404NotFound);
        }
    }

    private static bool IsAuthorized(string providedKey, string configuredKey)
    {
        if (string.IsNullOrWhiteSpace(providedKey) || string.IsNullOrWhiteSpace(configuredKey))
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        return providedBytes.Length == configuredBytes.Length &&
            CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }
}
