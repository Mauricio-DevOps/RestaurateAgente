using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Restaurantes.Web.Auth;
using Restaurantes.Web.Data;
using Restaurantes.Web.Models;
using Restaurantes.Web.Options;

namespace Restaurantes.Web.Controllers;

[ApiController]
[Route("api/internal/whatsapp-auth")]
public sealed class InternalWhatsAppAuthController : ControllerBase
{
    private const string ServiceKeyHeaderName = "X-Internal-Service-Key";

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] InternalWhatsAppLoginRequest request,
        [FromServices] UserManager<ApplicationUser> userManager,
        [FromServices] ApplicationDbContext db,
        [FromServices] IOptions<InternalApiOptions> internalApiOptions)
    {
        if (!IsAuthorized(Request.Headers[ServiceKeyHeaderName].ToString(), internalApiOptions.Value.ServiceKey))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Problem(
                title: "Invalid login request",
                detail: "Email and password are required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.ProfileStatus != EntityStatus.ACTIVE)
        {
            return Unauthorized();
        }

        var passwordMatches = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordMatches)
        {
            return Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        if (!roles.Contains(AppRoles.AdminRestaurante) || !user.RestaurantId.HasValue)
        {
            return Forbid();
        }

        var restaurant = await db.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == user.RestaurantId.Value);
        if (restaurant is null || restaurant.Status != EntityStatus.ACTIVE)
        {
            return Forbid();
        }

        if (!RestaurantPortalAccess.HasWhatsAppAccess(restaurant.AccessMode))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(restaurant.WhatsAppPhone))
        {
            return Problem(
                title: "WhatsApp phone missing",
                detail: "The restaurant does not have a WhatsApp phone configured.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Ok(new InternalWhatsAppLoginResponse(
            restaurant.Id.ToString(),
            restaurant.Name,
            restaurant.WhatsAppPhone,
            user.Email ?? user.UserName ?? request.Email.Trim()));
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

public sealed record InternalWhatsAppLoginRequest(string Email, string Password);

public sealed record InternalWhatsAppLoginResponse(
    string CompanyId,
    string CompanyName,
    string CompanyPhone,
    string Username);
