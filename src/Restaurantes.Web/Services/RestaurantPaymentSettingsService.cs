using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Restaurantes.Web.Data;
using Restaurantes.Web.Models;
using System.Globalization;
using System.Security.Cryptography;

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

        await SaveMercadoPagoSettingsForRestaurantAsync(restaurant, accessToken, webhookSecret, cancellationToken);
    }

    public async Task<MercadoPagoStorePaymentSettingsStatus> GetMercadoPagoSettingsByStoreIdAsync(
        string storeId,
        CancellationToken cancellationToken = default)
    {
        var restaurant = await GetRestaurantByStoreIdAsync(storeId, asNoTracking: true, cancellationToken);
        var settings = await _db.RestaurantPaymentSettings.AsNoTracking().FirstOrDefaultAsync(item =>
            item.RestaurantId == restaurant.Id &&
            item.Provider == ProviderMercadoPago,
            cancellationToken);

        return ToStatus(restaurant, settings);
    }

    public async Task<MercadoPagoStorePaymentSettingsStatus> SaveMercadoPagoSettingsByStoreIdAsync(
        MercadoPagoStorePaymentSettingsInput input,
        CancellationToken cancellationToken = default)
    {
        var restaurant = await GetRestaurantByStoreIdAsync(input.StoreId, asNoTracking: false, cancellationToken);
        var settings = await _db.RestaurantPaymentSettings.FirstOrDefaultAsync(item =>
            item.RestaurantId == restaurant.Id &&
            item.Provider == ProviderMercadoPago,
            cancellationToken);

        var accessToken = NormalizeOptionalSecret(input.AccessToken);
        var webhookSecret = NormalizeOptionalSecret(input.WebhookSecret);
        if (settings is null && (accessToken is null || webhookSecret is null))
        {
            throw new InvalidOperationException("Informe o access token e o webhook secret do Mercado Pago.");
        }

        if (settings is not null)
        {
            accessToken ??= UnprotectSavedSecret(settings.ProtectedAccessToken);
            webhookSecret ??= UnprotectSavedSecret(settings.ProtectedWebhookSecret);
        }

        return await SaveMercadoPagoSettingsForRestaurantAsync(
            restaurant,
            accessToken ?? string.Empty,
            webhookSecret ?? string.Empty,
            cancellationToken);
    }

    private async Task<MercadoPagoStorePaymentSettingsStatus> SaveMercadoPagoSettingsForRestaurantAsync(
        Restaurant restaurant,
        string accessToken,
        string webhookSecret,
        CancellationToken cancellationToken)
    {
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

        return ToStatus(restaurant, settings);
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

    private async Task<Restaurant> GetRestaurantByStoreIdAsync(
        string storeId,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        var normalizedStoreId = WhatsAppPhoneNormalizer.Normalize(storeId);
        IQueryable<Restaurant> query = _db.Restaurants;
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(
            restaurant => restaurant.WhatsAppPhone == normalizedStoreId,
            cancellationToken)
            ?? throw new InvalidOperationException("Restaurante nao encontrado para este WhatsApp.");
    }

    private MercadoPagoStorePaymentSettingsStatus ToStatus(
        Restaurant restaurant,
        RestaurantPaymentSettings? settings)
    {
        return new MercadoPagoStorePaymentSettingsStatus(
            restaurant.WhatsAppPhone ?? string.Empty,
            settings?.IsEnabled == true,
            !string.IsNullOrWhiteSpace(settings?.ProtectedAccessToken),
            !string.IsNullOrWhiteSpace(settings?.ProtectedWebhookSecret),
            settings?.MercadoPagoUserId,
            settings is null ? string.Empty : settings.UpdatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }

    private string UnprotectSavedSecret(string protectedValue)
    {
        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                "Credenciais Mercado Pago salvas nao puderam ser descriptografadas. Re-salve as credenciais.",
                ex);
        }
    }

    private static string? NormalizeOptionalSecret(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record RestaurantPaymentCredential(
    Guid RestaurantId,
    string Provider,
    string AccessToken,
    string WebhookSecret,
    string? MercadoPagoUserId);
