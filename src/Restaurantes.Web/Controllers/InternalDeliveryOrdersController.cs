using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Restaurantes.Web.Data;
using Restaurantes.Web.Models;
using Restaurantes.Web.Options;
using Restaurantes.Web.Services;

namespace Restaurantes.Web.Controllers;

[ApiController]
[Route("api/internal/delivery-orders")]
public sealed class InternalDeliveryOrdersController : ControllerBase
{
    private const string ServiceKeyHeaderName = "X-Internal-Service-Key";

    [HttpPost("whatsapp-payment")]
    public async Task<IActionResult> CreateWhatsAppPayment(
        [FromBody] WhatsAppDeliveryPaymentRequest request,
        [FromServices] ApplicationDbContext db,
        [FromServices] PublicOrderPaymentService publicOrderPaymentService,
        [FromServices] IOptions<InternalApiOptions> internalApiOptions,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(Request.Headers[ServiceKeyHeaderName].ToString(), internalApiOptions.Value.ServiceKey))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.StoreId) ||
            string.IsNullOrWhiteSpace(request.PhoneNumber) ||
            string.IsNullOrWhiteSpace(request.DeliveryAddress) ||
            request.Items is null ||
            request.Items.Count == 0)
        {
            return Problem(
                title: "Invalid WhatsApp delivery order",
                detail: "StoreId, phoneNumber, deliveryAddress and items are required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var restaurantPhone = WhatsAppPhoneNormalizer.Normalize(request.StoreId);
            var restaurant = await db.Restaurants.AsNoTracking().FirstOrDefaultAsync(
                item => item.WhatsAppPhone == restaurantPhone,
                cancellationToken);
            if (restaurant is null)
            {
                return Problem(
                    title: "Restaurant not found",
                    detail: "No restaurant is linked to this WhatsApp store.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            var orderItems = NormalizeItems(request.Items)
                .Select(item => new WhatsAppDeliveryOrderItemInput
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                })
                .ToList();

            var result = await publicOrderPaymentService.SubmitWhatsAppDeliveryOrderAsync(
                new WhatsAppDeliveryOrderSubmissionInput
                {
                    RestaurantId = restaurant.Id,
                    CustomerName = FirstNonEmpty(request.CustomerName, "Cliente WhatsApp"),
                    CustomerPhone = request.PhoneNumber,
                    DeliveryAddress = request.DeliveryAddress,
                    Items = orderItems
                },
                cancellationToken);

            return Ok(new WhatsAppDeliveryPaymentResponse(
                result.OrderId,
                result.PaymentStatus.ToString(),
                result.CheckoutUrl,
                result.TotalCents,
                result.TotalLabel));
        }
        catch (InvalidOperationException error)
        {
            return Problem(
                title: "WhatsApp payment order failed",
                detail: error.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static IReadOnlyList<NormalizedWhatsAppOrderItem> NormalizeItems(
        IReadOnlyList<WhatsAppDeliveryPaymentItemRequest> items)
    {
        var normalizedItems = items
            .Where(item => !string.IsNullOrWhiteSpace(item.ProductId) && item.Quantity > 0)
            .GroupBy(item => item.ProductId!.Trim(), StringComparer.Ordinal)
            .Select(group => new NormalizedWhatsAppOrderItem(
                group.Key,
                group.Sum(item => item.Quantity)))
            .ToArray();

        if (normalizedItems.Length == 0)
        {
            throw new InvalidOperationException("Adicione ao menos um item valido ao pedido.");
        }

        return normalizedItems;
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

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private sealed record NormalizedWhatsAppOrderItem(string ProductId, int Quantity);
}
