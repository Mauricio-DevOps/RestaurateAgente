using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using Restaurantes.Web.Auth;
using Restaurantes.Web.Models;
using Restaurantes.Web.Services;
using System.Text;

namespace Restaurantes.Web.Controllers;

[Authorize(Roles = AppRoles.AdminRestaurante)]
[Route("restaurante")]
public sealed class RestauranteController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RestaurantService _restaurantService;
    private readonly WaiterLoginService _waiterLoginService;
    private readonly InternalWhatsAppApiClient _internalWhatsAppApiClient;
    private readonly IWebHostEnvironment _environment;

    public RestauranteController(
        UserManager<ApplicationUser> userManager,
        RestaurantService restaurantService,
        WaiterLoginService waiterLoginService,
        InternalWhatsAppApiClient internalWhatsAppApiClient,
        IWebHostEnvironment environment)
    {
        _userManager = userManager;
        _restaurantService = restaurantService;
        _waiterLoginService = waiterLoginService;
        _internalWhatsAppApiClient = internalWhatsAppApiClient;
        _environment = environment;
    }

    [HttpGet("")]
    [RequireRestaurantPortalAccess(RestaurantPortalArea.Restaurante)]
    public async Task<IActionResult> Index()
    {
        var restaurantId = await GetRestaurantIdAsync();
        return View(await _restaurantService.GetOverviewAsync(restaurantId));
    }

    [HttpGet("cardapio")]
    [RequireRestaurantPortalAccess(RestaurantPortalArea.Restaurante)]
    public async Task<IActionResult> Cardapio()
    {
        return View(await _restaurantService.GetMenuEditorAsync(await GetRestaurantIdAsync()));
    }

    [HttpGet("cardapio/qrcode")]
    [RequireRestaurantPortalAccess(RestaurantPortalArea.Restaurante)]
    public async Task<IActionResult> MenuQrCode([FromQuery(Name = "mesa")] string? tableNumber)
    {
        var restaurantId = await GetRestaurantIdAsync();
        var menuUrl = BuildPublicMenuUrl(restaurantId, tableNumber);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(menuUrl, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new SvgQRCode(data);
        var svg = qrCode.GetGraphic(8);

        return File(Encoding.UTF8.GetBytes(svg), "image/svg+xml");
    }

    [HttpPost("cardapio/marca")]
    [RequireRestaurantPortalAccess(RestaurantPortalArea.Restaurante)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBrand(string restaurantName, string? publicDescription, string primaryColor, string menuTheme, string menuMode, IFormFile? coverImage)
    {
        var restaurantId = await GetRestaurantIdAsync();
        var coverUrl = await SaveUploadAsync(restaurantId, coverImage);
        await _restaurantService.UpdateRestaurantBrandAsync(restaurantId, restaurantName, publicDescription, primaryColor, menuTheme, menuMode, coverUrl);
        return RedirectToAction(nameof(Cardapio));
    }

    [HttpPost("cardapio/categorias")]
    [RequireRestaurantPortalAccess(RestaurantPortalArea.Restaurante)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(string name)
    {
        await _restaurantService.AddCategoryAsync(await GetRestaurantIdAsync(), name);
        return RedirectToAction(nameof(Cardapio));
    }

    [HttpPost("cardapio/categorias/{categoryId:guid}/toggle")]
    [RequireRestaurantPortalAccess(RestaurantPortalArea.Restaurante)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCategory(Guid categoryId)
    {
        await _restaurantService.ToggleCategoryAsync(await GetRestaurantIdAsync(), categoryId);
        return RedirectToAction(nameof(Cardapio));
    }

    [HttpPost("cardapio/itens")]
    [RequireRestaurantPortalAccess(RestaurantPortalArea.Restaurante)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(Guid categoryId, string name, string description, string priceInput, IFormFile? image, bool syncToWhatsApp, MenuItemPromotionInput promotion, CancellationToken cancellationToken)
    {
        var restaurantId = await GetRestaurantIdAsync();
        var imageUrl = await SaveUploadAsync(restaurantId, image);
        var item = await _restaurantService.AddMenuItemAsync(restaurantId, categoryId, name, description, priceInput, imageUrl, promotion);
        if (syncToWhatsApp)
        {
            await SyncMenuItemToWhatsAppAsync(item, cancellationToken);
        }

        return RedirectToAction(nameof(Cardapio));
    }

    [HttpPost("cardapio/itens/{itemId:guid}")]
    [RequireRestaurantPortalAccess(RestaurantPortalArea.Restaurante)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItem(Guid itemId, string name, string description, string priceInput, IFormFile? image, bool syncToWhatsApp, MenuItemPromotionInput promotion, CancellationToken cancellationToken)
    {
        var restaurantId = await GetRestaurantIdAsync();
        var imageUrl = await SaveUploadAsync(restaurantId, image);
        var item = await _restaurantService.UpdateMenuItemAsync(restaurantId, itemId, name, description, priceInput, imageUrl, promotion);
        TempData["Feedback"] = "Item atualizado.";
        if (syncToWhatsApp)
        {
            await SyncMenuItemToWhatsAppAsync(item, cancellationToken);
        }

        return RedirectToAction(nameof(Cardapio));
    }

    [HttpPost("cardapio/itens/{itemId:guid}/toggle")]
    [RequireRestaurantPortalAccess(RestaurantPortalArea.Restaurante)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleItem(Guid itemId)
    {
        await _restaurantService.ToggleMenuItemAsync(await GetRestaurantIdAsync(), itemId);
        return RedirectToAction(nameof(Cardapio));
    }

    [HttpPost("cardapio/itens/{itemId:guid}/delete")]
    [RequireRestaurantPortalAccess(RestaurantPortalArea.Restaurante)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem(Guid itemId)
    {
        await _restaurantService.DeleteMenuItemAsync(await GetRestaurantIdAsync(), itemId);
        return RedirectToAction(nameof(Cardapio));
    }

    [HttpPost("cardapio/cupons")]
    [RequireRestaurantPortalAccess(RestaurantPortalArea.Restaurante)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCoupon(DiscountCouponInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Feedback"] = "Revise o codigo e o valor do cupom.";
            return Redirect("/restaurante/cardapio#discount-coupons");
        }

        try
        {
            await _restaurantService.AddDiscountCouponAsync(await GetRestaurantIdAsync(), input);
            TempData["Feedback"] = "Cupom cadastrado.";
        }
        catch (InvalidOperationException error)
        {
            TempData["Feedback"] = error.Message;
        }

        return Redirect("/restaurante/cardapio#discount-coupons");
    }

    [HttpPost("cardapio/cupons/{couponId:guid}")]
    [RequireRestaurantPortalAccess(RestaurantPortalArea.Restaurante)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCoupon(Guid couponId, DiscountCouponInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Feedback"] = "Revise o codigo e o valor do cupom.";
            return Redirect("/restaurante/cardapio#discount-coupons");
        }

        try
        {
            await _restaurantService.UpdateDiscountCouponAsync(await GetRestaurantIdAsync(), couponId, input);
            TempData["Feedback"] = "Cupom atualizado.";
        }
        catch (InvalidOperationException error)
        {
            TempData["Feedback"] = error.Message;
        }

        return Redirect("/restaurante/cardapio#discount-coupons");
    }

    [HttpPost("cardapio/cupons/{couponId:guid}/toggle")]
    [RequireRestaurantPortalAccess(RestaurantPortalArea.Restaurante)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCoupon(Guid couponId)
    {
        try
        {
            await _restaurantService.ToggleDiscountCouponAsync(await GetRestaurantIdAsync(), couponId);
            TempData["Feedback"] = "Status do cupom atualizado.";
        }
        catch (InvalidOperationException error)
        {
            TempData["Feedback"] = error.Message;
        }

        return Redirect("/restaurante/cardapio#discount-coupons");
    }

    [HttpGet("operacao")]
    public async Task<IActionResult> Operacao()
    {
        var model = await _restaurantService.GetOperationsAsync(await GetRestaurantIdAsync());
        model.CanManageRestaurantOperations = RestaurantPortalAccess.HasRestaurantAccess(User);
        model.CanManageWhatsApp = RestaurantPortalAccess.HasWhatsAppAccess(User);
        return View(model);
    }

    [HttpGet("delivery")]
    [RequireRestaurantPortalAccess(RestaurantPortalArea.Restaurante)]
    public async Task<IActionResult> Delivery()
    {
        return View(await _restaurantService.GetDeliveryDashboardAsync(await GetRestaurantIdAsync()));
    }

    [HttpPost("operacao/login-garcom")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveWaiterLogin(WaiterLoginInput input)
    {
        if (EnsureRestaurantOperationsAccess() is IActionResult deniedResult)
        {
            return deniedResult;
        }

        if (!ModelState.IsValid)
        {
            TempData["Feedback"] = "Revise o email, nome e senha do login do garcom.";
            return RedirectToAction(nameof(Operacao));
        }

        try
        {
            await _waiterLoginService.SaveWaiterLoginAsync(await GetRestaurantIdAsync(), input);
            TempData["Feedback"] = "Login do garcom salvo.";
        }
        catch (InvalidOperationException error)
        {
            TempData["Feedback"] = error.Message;
        }

        return RedirectToAction(nameof(Operacao));
    }

    [HttpPost("operacao/whatsapp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveWhatsAppPhone(WhatsAppContactInput input, CancellationToken cancellationToken)
    {
        if (EnsureWhatsAppOperationsAccess() is IActionResult deniedResult)
        {
            return deniedResult;
        }

        if (!ModelState.IsValid)
        {
            TempData["Feedback"] = "Informe o telefone de WhatsApp do restaurante.";
            return Redirect("/restaurante/operacao#whatsapp-contact");
        }

        try
        {
            var update = await _restaurantService.SaveWhatsAppPhoneAsync(await GetRestaurantIdAsync(), input.Phone);
            await _internalWhatsAppApiClient.SyncCompanyAsync(
                update.RestaurantName,
                update.NewPhone,
                update.PreviousPhone,
                cancellationToken);
            TempData["Feedback"] = "Telefone de WhatsApp salvo e sincronizado.";
        }
        catch (InvalidOperationException error)
        {
            TempData["Feedback"] = error.Message;
        }
        catch (HttpRequestException)
        {
            TempData["Feedback"] = "Telefone salvo, mas nao foi possivel sincronizar a API do WhatsApp.";
        }
        catch (TaskCanceledException)
        {
            TempData["Feedback"] = "Telefone salvo, mas a API do WhatsApp demorou para responder.";
        }

        return Redirect("/restaurante/operacao#whatsapp-contact");
    }

    [HttpPost("operacao/sla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSla(RestaurantSlaInput input)
    {
        if (EnsureRestaurantOperationsAccess() is IActionResult deniedResult)
        {
            return deniedResult;
        }

        if (!ModelState.IsValid)
        {
            TempData["Feedback"] = "Informe tempos de SLA entre 1 e 1440 minutos, ou deixe o campo vazio.";
            return Redirect("/restaurante/operacao#sla-settings");
        }

        try
        {
            await _restaurantService.SaveOperationalSlaAsync(await GetRestaurantIdAsync(), input);
            TempData["Feedback"] = "SLA operacional salvo.";
        }
        catch (InvalidOperationException error)
        {
            TempData["Feedback"] = error.Message;
        }

        return Redirect("/restaurante/operacao#sla-settings");
    }

    [HttpPost("operacao/garcons")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddWaiter(string name)
    {
        if (EnsureRestaurantOperationsAccess() is IActionResult deniedResult)
        {
            return deniedResult;
        }

        await _restaurantService.AddWaiterAsync(await GetRestaurantIdAsync(), name);
        return RedirectToAction(nameof(Operacao));
    }

    [HttpPost("operacao/garcons/{waiterId:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteWaiter(Guid waiterId)
    {
        if (EnsureRestaurantOperationsAccess() is IActionResult deniedResult)
        {
            return deniedResult;
        }

        await _restaurantService.DeleteWaiterAsync(await GetRestaurantIdAsync(), waiterId);
        return RedirectToAction(nameof(Operacao));
    }

    [HttpPost("operacao/mesas")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTable(string tableNumber, Guid? assignedWaiterId)
    {
        if (EnsureRestaurantOperationsAccess() is IActionResult deniedResult)
        {
            return deniedResult;
        }

        await _restaurantService.AddTableAsync(await GetRestaurantIdAsync(), tableNumber, assignedWaiterId);
        return RedirectToAction(nameof(Operacao));
    }

    [HttpPost("operacao/mesas/{tableId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTable(Guid tableId, Guid? assignedWaiterId)
    {
        if (EnsureRestaurantOperationsAccess() is IActionResult deniedResult)
        {
            return deniedResult;
        }

        await _restaurantService.UpdateTableAsync(await GetRestaurantIdAsync(), tableId, assignedWaiterId);
        return RedirectToAction(nameof(Operacao));
    }

    [HttpPost("operacao/mesas/{tableId:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTable(Guid tableId)
    {
        if (EnsureRestaurantOperationsAccess() is IActionResult deniedResult)
        {
            return deniedResult;
        }

        await _restaurantService.DeleteTableAsync(await GetRestaurantIdAsync(), tableId);
        return RedirectToAction(nameof(Operacao));
    }

    private IActionResult? EnsureRestaurantOperationsAccess()
    {
        if (RestaurantPortalAccess.HasRestaurantAccess(User))
        {
            return null;
        }

        TempData["Feedback"] = "Seu acesso atual nao permite alterar essa parte da operacao.";
        return RedirectToAction(nameof(Operacao));
    }

    private IActionResult? EnsureWhatsAppOperationsAccess()
    {
        if (RestaurantPortalAccess.HasWhatsAppAccess(User))
        {
            return null;
        }

        TempData["Feedback"] = "Seu acesso atual nao permite alterar o WhatsApp do restaurante.";
        return RedirectToAction(nameof(Operacao));
    }

    private async Task<Guid> GetRestaurantIdAsync()
    {
        var user = await _userManager.GetUserAsync(User) ?? throw new InvalidOperationException("Usuario nao autenticado.");
        return user.RestaurantId ?? throw new InvalidOperationException("Usuario sem restaurante.");
    }

    private string BuildPublicMenuUrl(Guid restaurantId, string? tableNumber)
    {
        var url = Url.Action("Public", "Cardapio", new { restaurantId }, Request.Scheme)
            ?? $"{Request.Scheme}://{Request.Host}/cardapio/{restaurantId}";
        return string.IsNullOrWhiteSpace(tableNumber)
            ? url
            : $"{url}?mesa={Uri.EscapeDataString(tableNumber.Trim())}";
    }

    private async Task SyncMenuItemToWhatsAppAsync(MenuItemWhatsAppSyncContext item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.StoreId))
        {
            TempData["Feedback"] = "Item salvo, mas cadastre o WhatsApp do restaurante antes de sincronizar com Produtos WhatsApp.";
            return;
        }

        try
        {
            var product = await _internalWhatsAppApiClient.SyncProductFromMenuAsync(
                new WhatsAppProductSyncRequest(
                    item.StoreId,
                    item.WhatsAppProductId,
                    item.Name,
                    item.Description,
                    item.RetailPrice,
                    item.IsActive),
                cancellationToken);
            await _restaurantService.LinkMenuItemToWhatsAppProductAsync(item.RestaurantId, item.MenuItemId, product.Id);
            TempData["Feedback"] = "Item salvo e sincronizado com Produtos WhatsApp.";
        }
        catch (HttpRequestException)
        {
            TempData["Feedback"] = "Item salvo, mas nao foi possivel sincronizar com Produtos WhatsApp.";
        }
        catch (TaskCanceledException)
        {
            TempData["Feedback"] = "Item salvo, mas a API do WhatsApp demorou para responder.";
        }
    }

    private async Task<string?> SaveUploadAsync(Guid restaurantId, IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(file.FileName);
        if (!allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Use imagem JPG, PNG ou WEBP.");
        }

        var relativeDirectory = Path.Combine("uploads", restaurantId.ToString());
        var absoluteDirectory = Path.Combine(_environment.WebRootPath, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(absoluteDirectory, fileName);
        await using var stream = System.IO.File.Create(absolutePath);
        await file.CopyToAsync(stream);
        return "/" + Path.Combine(relativeDirectory, fileName).Replace('\\', '/');
    }
}
