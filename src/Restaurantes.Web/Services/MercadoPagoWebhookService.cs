using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Restaurantes.Web.Data;
using Restaurantes.Web.Models;

namespace Restaurantes.Web.Services;

public sealed class MercadoPagoWebhookService
{
    private readonly ApplicationDbContext _db;
    private readonly RestaurantPaymentSettingsService _paymentSettingsService;
    private readonly IMercadoPagoClient _mercadoPagoClient;
    private readonly RestaurantService _restaurantService;

    public MercadoPagoWebhookService(
        ApplicationDbContext db,
        RestaurantPaymentSettingsService paymentSettingsService,
        IMercadoPagoClient mercadoPagoClient,
        RestaurantService restaurantService)
    {
        _db = db;
        _paymentSettingsService = paymentSettingsService;
        _mercadoPagoClient = mercadoPagoClient;
        _restaurantService = restaurantService;
    }

    public async Task<MercadoPagoWebhookProcessResult> ProcessAsync(
        Guid restaurantId,
        JsonElement payload,
        string payloadJson,
        string? dataIdFromQuery,
        string? xSignature,
        string? xRequestId,
        CancellationToken cancellationToken)
    {
        var credential = await _paymentSettingsService.GetActiveMercadoPagoCredentialAsync(restaurantId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Credenciais Mercado Pago nao configuradas para este restaurante.");

        var paymentId = FirstNonEmpty(dataIdFromQuery, ReadString(payload, "data", "id"), ExtractResourceId(payload));
        if (string.IsNullOrWhiteSpace(paymentId))
        {
            return new MercadoPagoWebhookProcessResult(false, "payment_id_missing");
        }

        if (!ValidateSignature(xSignature, xRequestId, dataIdFromQuery, credential.WebhookSecret))
        {
            throw new UnauthorizedAccessException("Assinatura Mercado Pago invalida.");
        }

        var action = ReadString(payload, "action");
        var eventId = FirstNonEmpty(ReadString(payload, "id"), xRequestId, $"{action}:{paymentId}");
        if (await _db.PaymentWebhookEvents.AnyAsync(item =>
            item.Provider == "MercadoPago" &&
            item.EventId == eventId,
            cancellationToken))
        {
            return new MercadoPagoWebhookProcessResult(false, "duplicate");
        }

        MercadoPagoPaymentInfo payment;
        try
        {
            payment = await _mercadoPagoClient.GetPaymentAsync(
                credential.AccessToken,
                paymentId,
                cancellationToken);
        }
        catch (MercadoPagoApiException error) when (IsPaymentNotFound(error))
        {
            await SaveWebhookEventAsync(restaurantId, eventId, paymentId, action, "not_found", xRequestId, payloadJson, cancellationToken);
            return new MercadoPagoWebhookProcessResult(false, "payment_not_found");
        }

        if (!Guid.TryParse(payment.ExternalReference, out var orderId))
        {
            await SaveWebhookEventAsync(restaurantId, eventId, paymentId, action, payment.Status, xRequestId, payloadJson, cancellationToken);
            return new MercadoPagoWebhookProcessResult(false, "external_reference_invalid");
        }

        if (!string.IsNullOrWhiteSpace(credential.MercadoPagoUserId) &&
            !string.IsNullOrWhiteSpace(payment.CollectorId) &&
            !string.Equals(credential.MercadoPagoUserId, payment.CollectorId, StringComparison.Ordinal))
        {
            await SaveWebhookEventAsync(restaurantId, eventId, paymentId, action, payment.Status, xRequestId, payloadJson, cancellationToken);
            return new MercadoPagoWebhookProcessResult(false, "collector_mismatch");
        }

        var paymentStatus = MapPaymentStatus(payment.Status);
        var updated = await _restaurantService.UpdateDeliveryPaymentStatusAsync(
            restaurantId,
            orderId,
            paymentStatus,
            payment.Id,
            payment.Status,
            payment.StatusDetail,
            payment.DateCreated,
            payment.DateLastUpdated,
            payment.DateApproved,
            cancellationToken);

        await SaveWebhookEventAsync(restaurantId, eventId, paymentId, action, payment.Status, xRequestId, payloadJson, cancellationToken);
        return new MercadoPagoWebhookProcessResult(updated, updated ? "updated" : "order_not_found");
    }

    private async Task SaveWebhookEventAsync(
        Guid restaurantId,
        string eventId,
        string paymentId,
        string? action,
        string? paymentStatus,
        string? xRequestId,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        _db.PaymentWebhookEvents.Add(new PaymentWebhookEvent
        {
            RestaurantId = restaurantId,
            Provider = "MercadoPago",
            EventId = eventId,
            ResourceId = paymentId,
            Action = action,
            PaymentStatus = paymentStatus,
            RequestId = string.IsNullOrWhiteSpace(xRequestId) ? null : xRequestId.Trim(),
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static bool ValidateSignature(
        string? xSignature,
        string? xRequestId,
        string? dataIdFromQuery,
        string webhookSecret)
    {
        if (string.IsNullOrWhiteSpace(xSignature) ||
            string.IsNullOrWhiteSpace(xRequestId) ||
            string.IsNullOrWhiteSpace(webhookSecret))
        {
            return false;
        }

        var values = xSignature
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);

        if (!values.TryGetValue("ts", out var timestamp) || !values.TryGetValue("v1", out var receivedSignature))
        {
            return false;
        }

        var signedTemplate = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(dataIdFromQuery))
        {
            signedTemplate.Append("id:")
                .Append(dataIdFromQuery.Trim().ToLowerInvariant())
                .Append(';');
        }

        signedTemplate.Append("request-id:")
            .Append(xRequestId.Trim())
            .Append(';')
            .Append("ts:")
            .Append(timestamp)
            .Append(';');

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret.Trim()));
        var computedSignature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedTemplate.ToString())))
            .ToLowerInvariant();
        return FixedTimeEquals(computedSignature, receivedSignature);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
            CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static PaymentStatus MapPaymentStatus(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "approved" => PaymentStatus.PAGAMENTO_APROVADO,
            "rejected" or "cancelled" or "refunded" or "charged_back" => PaymentStatus.PAGAMENTO_NEGADO,
            _ => PaymentStatus.AGUARDANDO_PAGAMENTO
        };
    }

    private static bool IsPaymentNotFound(MercadoPagoApiException error)
    {
        if (error.StatusCode == 404)
        {
            return true;
        }

        return error.StatusCode == 400 &&
            error.ResponseBody.Contains("not_found", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractResourceId(JsonElement payload)
    {
        var resource = ReadString(payload, "resource");
        if (string.IsNullOrWhiteSpace(resource))
        {
            return null;
        }

        var index = resource.LastIndexOf('/');
        return index >= 0 && index < resource.Length - 1
            ? resource[(index + 1)..]
            : resource;
    }

    private static string? ReadString(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty(propertyName, out var property))
        {
            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.GetRawText(),
                _ => null
            };
        }

        return null;
    }

    private static string? ReadString(JsonElement payload, string parentName, string propertyName)
    {
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty(parentName, out var parent) &&
            parent.ValueKind == JsonValueKind.Object)
        {
            return ReadString(parent, propertyName);
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}

public sealed record MercadoPagoWebhookProcessResult(bool Updated, string Status);
