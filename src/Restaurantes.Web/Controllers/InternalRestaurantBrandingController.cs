using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Restaurantes.Web.Models;
using Restaurantes.Web.Options;
using Restaurantes.Web.Services;

namespace Restaurantes.Web.Controllers;

[ApiController]
[Route("api/internal/restaurant-branding")]
public sealed class InternalRestaurantBrandingController : ControllerBase
{
    private const string ServiceKeyHeaderName = "X-Internal-Service-Key";

    [HttpPost("sync")]
    public async Task<IActionResult> SyncBranding(
        [FromBody] BrandingSyncRequest request,
        [FromServices] RestaurantService restaurantService,
        [FromServices] IOptions<InternalApiOptions> internalApiOptions)
    {
        if (!IsAuthorized(Request.Headers[ServiceKeyHeaderName].ToString(), internalApiOptions.Value.ServiceKey))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.StoreId) ||
            string.IsNullOrWhiteSpace(request.SiteName) ||
            string.IsNullOrWhiteSpace(request.PaletteKey))
        {
            return Problem(
                title: "Invalid branding sync",
                detail: "StoreId, SiteName and PaletteKey are required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            await restaurantService.SyncBrandingFromWhatsAppAsync(request);
            return Ok();
        }
        catch (InvalidOperationException error)
        {
            return Problem(
                title: "Branding sync failed",
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
