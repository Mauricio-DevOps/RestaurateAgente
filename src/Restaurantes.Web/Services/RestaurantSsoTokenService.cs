using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Restaurantes.Web.Models;
using Restaurantes.Web.Options;

namespace Restaurantes.Web.Services;

public sealed class RestaurantSsoTokenService
{
    private readonly SsoOptions _options;

    public RestaurantSsoTokenService(IOptions<SsoOptions> options)
    {
        _options = options.Value;
    }

    public string CreateToken(
        Guid restaurantId,
        string restaurantName,
        string companyPhone,
        string targetPath,
        RestaurantAccessMode accessMode)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            throw new InvalidOperationException("Sso:SigningKey is not configured.");
        }

        var payload = new SsoPayload(
            restaurantId.ToString("D"),
            restaurantName,
            companyPhone,
            targetPath,
            DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, _options.TokenLifetimeMinutes)).ToString("O"),
            accessMode.ToString());
        var json = JsonSerializer.Serialize(payload);
        var payloadPart = Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        var signaturePart = Base64UrlEncode(Sign(payloadPart));
        return $"{payloadPart}.{signaturePart}";
    }

    private byte[] Sign(string payloadPart)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SigningKey));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadPart));
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed record SsoPayload(
        string RestaurantId,
        string RestaurantName,
        string CompanyPhone,
        string TargetPath,
        string ExpiresUtc,
        string AccessMode);
}
