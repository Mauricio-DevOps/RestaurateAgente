using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Restaurantes.Web.Data;
using Restaurantes.Web.Models;

namespace Restaurantes.Web.Services;

public sealed class RestaurantPaymentSettingsService
{
    private const string ProviderMercadoPago = "MercadoPago";

    private readonly ApplicationDbContext _db;
    private readonly IDataProtector _protector;
    private readonly IMercadoPagoClient _mercadoPagoClient;

    public RestaurantPaymentSettingsService(
        ApplicationDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        IMercadoPagoClient mercadoPagoClient)
    {
        _db = db;
        _protector = dataProtectionProvider.CreateProtector("Restaurantes.Web.PaymentSettings.v1");
        _mercadoPagoClient = mercadoPagoClient;
    }

    public async Task SaveMercadoPagoSettingsAsync(
        RestaurantPaymentSettingsInput input,
        CancellationToken cancellationToken = default)
    {
        var restaurant = await _db.Restaurants.FirstOrDefaultAsync(item => item.Id == input.RestaurantId, cancellationToken)
            ?? throw new InvalidOperationException("Restaurante nao encontrado.");

        var accessToken = input.AccessToken.Trim();
        var webhookSecret = input.WebhookSecret.Trim();
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(webhookSecret))
        {
            throw new InvalidOperationException("Informe o access token e o webhook secret do Mercado Pago.");
        }

        var userInfo = await _mercadoPagoClient.GetUserInfoAsync(accessToken, cancellationToken);
        var settings = await _db.RestaurantPaymentSettings.FirstOrDefaultAsync(item =>
            item.RestaurantId == restaurant.Id &&
            item.Provider == ProviderMercadoPago,
            cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (settings is null)
        {
            settings = new RestaurantPaymentSettings
            {
                RestaurantId = restaurant.Id,
                Provider = ProviderMercadoPago,
                CreatedAt = now
            };
            _db.RestaurantPaymentSettings.Add(settings);
        }

        settings.ProtectedAccessToken = _protector.Protect(accessToken);
        settings.ProtectedWebhookSecret = _protector.Protect(webhookSecret);
        settings.MercadoPagoUserId = userInfo.Id;
        settings.IsEnabled = true;
        settings.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DisableMercadoPagoSettingsAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default)
    {
        var settings = await _db.RestaurantPaymentSettings.FirstOrDefaultAsync(item =>
            item.RestaurantId == restaurantId &&
            item.Provider == ProviderMercadoPago,
            cancellationToken);
        if (settings is null)
        {
            return;
        }

        settings.IsEnabled = false;
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<RestaurantPaymentCredential?> GetActiveMercadoPagoCredentialAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default)
    {
        var settings = await _db.RestaurantPaymentSettings.AsNoTracking().FirstOrDefaultAsync(item =>
            item.RestaurantId == restaurantId &&
            item.Provider == ProviderMercadoPago &&
            item.IsEnabled,
            cancellationToken);
        if (settings is null)
        {
            return null;
        }

        return new RestaurantPaymentCredential(
            settings.RestaurantId,
            settings.Provider,
            _protector.Unprotect(settings.ProtectedAccessToken),
            _protector.Unprotect(settings.ProtectedWebhookSecret),
            settings.MercadoPagoUserId);
    }
}

public sealed record RestaurantPaymentCredential(
    Guid RestaurantId,
    string Provider,
    string AccessToken,
    string WebhookSecret,
    string? MercadoPagoUserId);
