using Microsoft.Extensions.Options;
using Restaurantes.Web.Models;
using Restaurantes.Web.Options;

namespace Restaurantes.Web.Services;

public sealed class PublicOrderPaymentService
{
    private readonly RestaurantService _restaurantService;
    private readonly RestaurantPaymentSettingsService _paymentSettingsService;
    private readonly IMercadoPagoClient _mercadoPagoClient;
    private readonly ExternalUrlResolver _externalUrlResolver;
    private readonly MercadoPagoOptions _options;

    public PublicOrderPaymentService(
        RestaurantService restaurantService,
        RestaurantPaymentSettingsService paymentSettingsService,
        IMercadoPagoClient mercadoPagoClient,
        ExternalUrlResolver externalUrlResolver,
        IOptions<MercadoPagoOptions> options)
    {
        _restaurantService = restaurantService;
        _paymentSettingsService = paymentSettingsService;
        _mercadoPagoClient = mercadoPagoClient;
        _externalUrlResolver = externalUrlResolver;
        _options = options.Value;
    }

    public async Task<PublicOrderSubmissionResult> SubmitPublicOrderAsync(
        PublicOrderSubmissionInput input,
        CancellationToken cancellationToken)
    {
        if (input.TableId.HasValue)
        {
            return await _restaurantService.SubmitPublicOrderAsync(input);
        }

        var credential = await _paymentSettingsService.GetActiveMercadoPagoCredentialAsync(
            input.RestaurantId,
            cancellationToken)
            ?? throw new InvalidOperationException("Pagamento online nao configurado para este restaurante.");

        var result = await _restaurantService.SubmitPublicOrderAsync(input);
        if (result.TotalCents <= 0)
        {
            throw new InvalidOperationException("O total do pedido precisa ser maior que zero para pagamento online.");
        }

        var preference = await _mercadoPagoClient.CreatePreferenceAsync(
            credential.AccessToken,
            BuildPreferenceRequest(input.RestaurantId, result),
            cancellationToken);
        var checkoutUrl = ResolveCheckoutUrl(preference);

        await _restaurantService.AttachDeliveryPaymentPreferenceAsync(
            input.RestaurantId,
            result.OrderId,
            preference.Id,
            checkoutUrl);

        result.PaymentStatus = PaymentStatus.AGUARDANDO_PAGAMENTO;
        result.CheckoutUrl = checkoutUrl;
        return result;
    }

    private MercadoPagoPreferenceCreateRequest BuildPreferenceRequest(
        Guid restaurantId,
        PublicOrderSubmissionResult result)
    {
        var orderId = result.OrderId.ToString("D");
        var notificationUrl = _externalUrlResolver.BuildMercadoPagoCallbackUrl(
            $"/api/payments/mercadopago/webhooks/{restaurantId}?source_news=webhooks");
        var successUrl = _externalUrlResolver.BuildMercadoPagoCallbackUrl(
            $"/cardapio/{restaurantId}?payment=success&orderId={orderId}");
        var failureUrl = _externalUrlResolver.BuildMercadoPagoCallbackUrl(
            $"/cardapio/{restaurantId}?payment=failure&orderId={orderId}");
        var pendingUrl = _externalUrlResolver.BuildMercadoPagoCallbackUrl(
            $"/cardapio/{restaurantId}?payment=pending&orderId={orderId}");

        return new MercadoPagoPreferenceCreateRequest(
            Items:
            [
                new MercadoPagoPreferenceItemRequest(
                    "Pedido delivery",
                    1,
                    "BRL",
                    result.TotalCents / 100m)
            ],
            Payer: new MercadoPagoPreferencePayerRequest(
                result.CustomerName,
                Phone: new MercadoPagoPhoneRequest(null, NormalizeDigits(result.CustomerPhone)),
                Address: new MercadoPagoAddressRequest(result.DeliveryAddress)),
            ExternalReference: orderId,
            NotificationUrl: notificationUrl,
            BackUrls: new MercadoPagoBackUrlsRequest(successUrl, failureUrl, pendingUrl),
            AutoReturn: "approved",
            Metadata: new Dictionary<string, string>
            {
                ["restaurant_id"] = restaurantId.ToString("D"),
                ["order_id"] = orderId
            });
    }

    private string ResolveCheckoutUrl(MercadoPagoPreferenceResult preference)
    {
        var checkoutUrl = _options.UseSandboxCheckout
            ? FirstNonEmpty(preference.SandboxInitPoint, preference.InitPoint)
            : FirstNonEmpty(preference.InitPoint, preference.SandboxInitPoint);

        if (string.IsNullOrWhiteSpace(checkoutUrl))
        {
            throw new InvalidOperationException("Mercado Pago nao retornou o link de pagamento.");
        }

        return checkoutUrl;
    }

    private static string? NormalizeDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
