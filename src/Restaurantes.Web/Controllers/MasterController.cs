using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restaurantes.Web.Auth;
using Restaurantes.Web.Models;
using Restaurantes.Web.Services;

namespace Restaurantes.Web.Controllers;

[Authorize(Roles = AppRoles.Master)]
[Route("master")]
public sealed class MasterController : Controller
{
    private readonly MasterService _masterService;
    private readonly RestaurantPaymentSettingsService _paymentSettingsService;

    public MasterController(
        MasterService masterService,
        RestaurantPaymentSettingsService paymentSettingsService)
    {
        _masterService = masterService;
        _paymentSettingsService = paymentSettingsService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        return View(await _masterService.GetDashboardAsync());
    }

    [HttpPost("admins")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAdmin(CreateRestaurantAdminInput input)
    {
        if (!ModelState.IsValid)
        {
            var view = await _masterService.GetDashboardAsync();
            view.CreateInput = input;
            return View("Index", view);
        }

        await _masterService.CreateRestaurantAdminAsync(input);
        TempData["Feedback"] = "Administrador criado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("restaurants/{restaurantId:guid}/access-mode")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAccessMode(Guid restaurantId, UpdateRestaurantAccessModeInput input)
    {
        await _masterService.UpdateRestaurantAccessModeAsync(restaurantId, input.AccessMode);
        TempData["Feedback"] = "Modo de acesso atualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("restaurants/{restaurantId:guid}/payments/mercadopago")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMercadoPagoSettings(Guid restaurantId, RestaurantPaymentSettingsInput input)
    {
        input.RestaurantId = restaurantId;
        try
        {
            await _paymentSettingsService.SaveMercadoPagoSettingsAsync(input, HttpContext.RequestAborted);
            TempData["Feedback"] = "Credenciais Mercado Pago salvas.";
        }
        catch (InvalidOperationException error)
        {
            TempData["Feedback"] = error.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("restaurants/{restaurantId:guid}/payments/mercadopago/disable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableMercadoPagoSettings(Guid restaurantId)
    {
        await _paymentSettingsService.DisableMercadoPagoSettingsAsync(restaurantId, HttpContext.RequestAborted);
        TempData["Feedback"] = "Pagamento Mercado Pago desativado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("admins/{userId}/access-mode")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAccessModeFromAdmin(string userId, UpdateRestaurantAccessModeInput input)
    {
        await _masterService.UpdateRestaurantAccessModeAsync(userId, input.AccessMode);
        TempData["Feedback"] = "Modo de acesso atualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("admins/{userId}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAdmin(string userId)
    {
        await _masterService.ToggleAdminStatusAsync(userId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("admins/{userId}/password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string userId, string password)
    {
        await _masterService.ResetAdminPasswordAsync(userId, password);
        TempData["Feedback"] = "Senha atualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("admins/{userId}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAdmin(string userId, Guid? restaurantId)
    {
        await _masterService.DeleteAdminAsync(userId, restaurantId);
        TempData["Feedback"] = "Administrador e restaurante removidos.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("restaurants/{restaurantId:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRestaurant(Guid restaurantId)
    {
        await _masterService.DeleteRestaurantAsync(restaurantId);
        TempData["Feedback"] = "Administrador e restaurante removidos.";
        return RedirectToAction(nameof(Index));
    }
}
