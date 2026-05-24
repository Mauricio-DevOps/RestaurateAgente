using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Restaurantes.Web.Services;

public interface IMercadoPagoClient
{
    Task<MercadoPagoUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken);

    Task<MercadoPagoPreferenceResult> CreatePreferenceAsync(
        string accessToken,
        MercadoPagoPreferenceCreateRequest request,
        CancellationToken cancellationToken);

    Task<MercadoPagoPaymentInfo> GetPaymentAsync(
        string accessToken,
        string paymentId,
        CancellationToken cancellationToken);
}

public sealed class MercadoPagoClient : IMercadoPagoClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;

    public MercadoPagoClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<MercadoPagoUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, "/users/me", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, body);

        var payload = JsonSerializer.Deserialize<MercadoPagoUserResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Mercado Pago retornou usuario invalido.");

        return new MercadoPagoUserInfo(payload.Id.ToString(CultureInfo.InvariantCulture), payload.Nickname);
    }

    public async Task<MercadoPagoPreferenceResult> CreatePreferenceAsync(
        string accessToken,
        MercadoPagoPreferenceCreateRequest request,
        CancellationToken cancellationToken)
    {
        using var httpRequest = CreateRequest(HttpMethod.Post, "/checkout/preferences", accessToken);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, body);

        var payload = JsonSerializer.Deserialize<MercadoPagoPreferenceResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Mercado Pago retornou preferencia invalida.");

        if (string.IsNullOrWhiteSpace(payload.Id))
        {
            throw new InvalidOperationException("Mercado Pago nao retornou o identificador da preferencia.");
        }

        return new MercadoPagoPreferenceResult(payload.Id, payload.InitPoint, payload.SandboxInitPoint);
    }

    public async Task<MercadoPagoPaymentInfo> GetPaymentAsync(
        string accessToken,
        string paymentId,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/v1/payments/{Uri.EscapeDataString(paymentId)}", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, body);

        var payload = JsonSerializer.Deserialize<MercadoPagoPaymentResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Mercado Pago retornou pagamento invalido.");

        return new MercadoPagoPaymentInfo(
            payload.Id.ToString(CultureInfo.InvariantCulture),
            payload.Status ?? "",
            payload.StatusDetail,
            payload.ExternalReference,
            payload.CollectorId?.ToString(CultureInfo.InvariantCulture),
            payload.DateCreated,
            payload.DateLastUpdated,
            payload.DateApproved);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Trim());
        return request;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Mercado Pago retornou erro {(int)response.StatusCode}: {body}");
        }
    }

    private sealed record MercadoPagoUserResponse(long Id, string? Nickname);

    private sealed record MercadoPagoPreferenceResponse(
        string? Id,
        [property: JsonPropertyName("init_point")] string? InitPoint,
        [property: JsonPropertyName("sandbox_init_point")] string? SandboxInitPoint);

    private sealed record MercadoPagoPaymentResponse(
        long Id,
        string? Status,
        [property: JsonPropertyName("status_detail")] string? StatusDetail,
        [property: JsonPropertyName("external_reference")] string? ExternalReference,
        [property: JsonPropertyName("collector_id")] long? CollectorId,
        [property: JsonPropertyName("date_created")] DateTimeOffset? DateCreated,
        [property: JsonPropertyName("date_last_updated")] DateTimeOffset? DateLastUpdated,
        [property: JsonPropertyName("date_approved")] DateTimeOffset? DateApproved);
}

public sealed record MercadoPagoUserInfo(string Id, string? Nickname);

public sealed record MercadoPagoPreferenceResult(
    string Id,
    string? InitPoint,
    string? SandboxInitPoint);

public sealed record MercadoPagoPaymentInfo(
    string Id,
    string Status,
    string? StatusDetail,
    string? ExternalReference,
    string? CollectorId,
    DateTimeOffset? DateCreated,
    DateTimeOffset? DateLastUpdated,
    DateTimeOffset? DateApproved);

public sealed record MercadoPagoPreferenceCreateRequest(
    IReadOnlyList<MercadoPagoPreferenceItemRequest> Items,
    MercadoPagoPreferencePayerRequest? Payer,
    [property: JsonPropertyName("external_reference")] string ExternalReference,
    [property: JsonPropertyName("notification_url")] string NotificationUrl,
    [property: JsonPropertyName("back_urls")] MercadoPagoBackUrlsRequest BackUrls,
    [property: JsonPropertyName("auto_return")] string AutoReturn,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MercadoPagoPreferenceItemRequest(
    string Title,
    int Quantity,
    [property: JsonPropertyName("currency_id")] string CurrencyId,
    [property: JsonPropertyName("unit_price")] decimal UnitPrice);

public sealed record MercadoPagoPreferencePayerRequest(
    string? Name,
    MercadoPagoPhoneRequest? Phone,
    MercadoPagoAddressRequest? Address);

public sealed record MercadoPagoPhoneRequest(
    [property: JsonPropertyName("area_code")] string? AreaCode,
    string? Number);

public sealed record MercadoPagoAddressRequest(
    [property: JsonPropertyName("street_name")] string? StreetName);

public sealed record MercadoPagoBackUrlsRequest(
    string Success,
    string Failure,
    string Pending);
