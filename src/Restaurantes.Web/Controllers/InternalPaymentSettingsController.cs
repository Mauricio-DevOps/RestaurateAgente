using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Restaurantes.Web.Models;
using Restaurantes.Web.Options;
using Restaurantes.Web.Services;

namespace Restaurantes.Web.Controllers;

[ApiController]
[Route("api/internal/payment-settings/mercadopago")]
public sealed class InternalPaymentSettingsController : ControllerBase
{
    private const string ServiceKeyHeaderName = "X-Internal-Service-Key";

    [HttpGet]
    public async Task<IActionResult> GetMercadoPagoSettings(
        [FromQuery] string? storeId,
        [FromServices] RestaurantPaymentSettingsService paymentSettingsService,
        [FromServices] IOptions<InternalApiOptions> internalApiOptions,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(Request.Headers[ServiceKeyHeaderName].ToString(), internalApiOptions.Value.ServiceKey))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(storeId))
        {
            return Problem(
                title: "Invalid Mercado Pago settings query",
                detail: "storeId is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            return Ok(await paymentSettingsService.GetMercadoPagoSettingsByStoreIdAsync(
                storeId.Trim(),
                cancellationToken));
        }
        catch (InvalidOperationException error)
        {
            return Problem(
                title: "Mercado Pago settings not found",
                detail: error.Message,
                statusCode: StatusCodes.Status404NotFound);
        }
    }

    [HttpPut]
    public async Task<IActionResult> SaveMercadoPagoSettings(
        [FromBody] MercadoPagoStorePaymentSettingsInput request,
        [FromServices] RestaurantPaymentSettingsService paymentSettingsService,
        [FromServices] IOptions<InternalApiOptions> internalApiOptions,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(Request.Headers[ServiceKeyHeaderName].ToString(), internalApiOptions.Value.ServiceKey))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.StoreId))
        {
            return Problem(
                title: "Invalid Mercado Pago settings request",
                detail: "storeId is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            return Ok(await paymentSettingsService.SaveMercadoPagoSettingsByStoreIdAsync(
                request with
                {
                    StoreId = request.StoreId.Trim(),
                    AccessToken = request.AccessToken?.Trim(),
                    WebhookSecret = request.WebhookSecret?.Trim()
                },
                cancellationToken));
        }
        catch (InvalidOperationException error)
        {
            return Problem(
                title: "Invalid Mercado Pago settings request",
                detail: error.Message,
                statusCode: StatusCodes.Status400BadRequest);
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
