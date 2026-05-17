using System.Net.Http.Json;
using Restaurantes.Web.Models;

namespace Restaurantes.Web.Services;

public sealed class InternalWhatsAppApiClient
{
    private const string ServiceKeyHeaderName = "X-Internal-Service-Key";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public InternalWhatsAppApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task SyncCompanyAsync(
        string restaurantName,
        string companyPhone,
        string? previousCompanyPhone,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/companies/sync")
        {
            Content = JsonContent.Create(new InternalCompanySyncRequest(
                restaurantName,
                companyPhone,
                previousCompanyPhone))
        };

        request.Headers.Add(ServiceKeyHeaderName, _configuration["InternalApi:ServiceKey"] ?? "");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<WhatsAppProductSyncResponse> SyncProductFromMenuAsync(
        WhatsAppProductSyncRequest syncRequest,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/products/sync-from-menu")
        {
            Content = JsonContent.Create(syncRequest)
        };

        request.Headers.Add(ServiceKeyHeaderName, _configuration["InternalApi:ServiceKey"] ?? "");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WhatsAppProductSyncResponse>(cancellationToken) ??
            throw new HttpRequestException("Empty product sync response.");
    }

    private sealed record InternalCompanySyncRequest(
        string CompanyName,
        string CompanyPhone,
        string? PreviousCompanyPhone);
}
