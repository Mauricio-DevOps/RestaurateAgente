using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Restaurantes.Web.Auth;
using Restaurantes.Web.Models;
using Restaurantes.Web.Services;

namespace Restaurantes.Web.Controllers;

[Authorize(Roles = AppRoles.AdminRestaurante)]
[RequireRestaurantPortalAccess(RestaurantPortalArea.WhatsApp)]
public sealed class WhatsAppBridgeController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RestaurantService _restaurantService;
    private readonly RestaurantSsoTokenService _ssoTokenService;
    private readonly ExternalUrlResolver _externalUrlResolver;

    public WhatsAppBridgeController(
        UserManager<ApplicationUser> userManager,
        RestaurantService restaurantService,
        RestaurantSsoTokenService ssoTokenService,
        ExternalUrlResolver externalUrlResolver)
    {
        _userManager = userManager;
        _restaurantService = restaurantService;
        _ssoTokenService = ssoTokenService;
        _externalUrlResolver = externalUrlResolver;
    }

    [HttpGet("/sso/whatsapp")]
    public async Task<IActionResult> Index([FromQuery] string? target)
    {
        var context = await _restaurantService.GetWhatsAppSsoContextAsync(await GetRestaurantIdAsync());
        if (context is null)
        {
            return Redirect("/restaurante/operacao?whatsappMissing=1#whatsapp-contact");
        }

        var normalizedTarget = RestaurantPortalAccess.NormalizeWhatsAppTargetOrDefault(target);
        var token = _ssoTokenService.CreateToken(
            context.RestaurantId,
            context.RestaurantName,
            context.CompanyPhone,
            normalizedTarget,
            context.AccessMode);
        return Redirect(_externalUrlResolver.BuildWhatsAppAdminUrl($"/Account/Sso?token={Uri.EscapeDataString(token)}"));
    }

    [HttpGet("/restaurante/whatsapp/dashboard")]
    public IActionResult Dashboard()
    {
        return Redirect(RestaurantPortalAccess.BuildWhatsAppBridgePath("/Dashboard"));
    }

    [HttpGet("/restaurante/whatsapp/products")]
    public IActionResult Products()
    {
        return Redirect(RestaurantPortalAccess.BuildWhatsAppBridgePath("/Products"));
    }

    [HttpGet("/restaurante/whatsapp/orders")]
    public IActionResult Orders()
    {
        return Redirect(RestaurantPortalAccess.BuildWhatsAppBridgePath("/Orders"));
    }

    [HttpGet("/restaurante/whatsapp/agent")]
    public IActionResult Agent()
    {
        return Redirect(RestaurantPortalAccess.BuildWhatsAppBridgePath("/Agent"));
    }

    private async Task<Guid> GetRestaurantIdAsync()
    {
        var user = await _userManager.GetUserAsync(User) ?? throw new InvalidOperationException("UsuÃ¡rio nÃ£o autenticado.");
        return user.RestaurantId ?? throw new InvalidOperationException("UsuÃ¡rio sem restaurante.");
    }
}
