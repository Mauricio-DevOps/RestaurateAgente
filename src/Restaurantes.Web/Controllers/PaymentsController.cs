using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restaurantes.Web.Services;

namespace Restaurantes.Web.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/payments/mercadopago/webhooks/{restaurantId:guid}")]
public sealed class PaymentsController : ControllerBase
{
    private readonly MercadoPagoWebhookService _webhookService;

    public PaymentsController(MercadoPagoWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    [HttpPost]
    public async Task<IActionResult> MercadoPagoWebhook(
        Guid restaurantId,
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken)
    {
        var dataIdFromQuery = FirstNonEmpty(
            Request.Query["data.id"].FirstOrDefault(),
            Request.Query["id"].FirstOrDefault());
        var xSignature = Request.Headers["x-signature"].FirstOrDefault();
        var xRequestId = Request.Headers["x-request-id"].FirstOrDefault();
        var payloadJson = payload.ValueKind == JsonValueKind.Undefined
            ? "{}"
            : payload.GetRawText();

        try
        {
            var result = await _webhookService.ProcessAsync(
                restaurantId,
                payload,
                payloadJson,
                dataIdFromQuery,
                xSignature,
                xRequestId,
                cancellationToken);

            return Ok(new { ok = true, result.Status, result.Updated });
        }
        catch (UnauthorizedAccessException error)
        {
            return Unauthorized(new { error = error.Message });
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
