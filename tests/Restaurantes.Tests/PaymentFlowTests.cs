using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Restaurantes.Web.Data;
using Restaurantes.Web.Models;
using Restaurantes.Web.Options;
using Restaurantes.Web.Services;

namespace Restaurantes.Tests;

public sealed class PaymentFlowTests
{
    [Fact]
    public async Task DeliveryOrder_WithoutPaymentToken_IsRejected()
    {
        await using var fixture = await PaymentFixture.CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.PaymentService.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
            {
                RestaurantId = fixture.Restaurant.Id,
                CustomerName = "Maria",
                CustomerPhone = "11999999999",
                DeliveryAddress = "Rua Um, 123",
                Items = [new PublicOrderItemInput { MenuItemId = fixture.Risoto.Id, Quantity = 1 }]
            }, CancellationToken.None));

        Assert.Contains("Pagamento online", error.Message);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task DeliveryOrder_WithPaymentToken_CreatesPendingOrderAndPreference()
    {
        await using var fixture = await PaymentFixture.CreateAsync();
        await fixture.SaveMercadoPagoSettingsAsync("token-restaurante", "secret-restaurante");

        var result = await fixture.PaymentService.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
        {
            RestaurantId = fixture.Restaurant.Id,
            CustomerName = "Maria",
            CustomerPhone = "11999999999",
            DeliveryAddress = "Rua Um, 123",
            Items = [new PublicOrderItemInput { MenuItemId = fixture.Risoto.Id, Quantity = 1 }]
        }, CancellationToken.None);

        var order = await fixture.Db.Orders.SingleAsync();
        Assert.Equal(PaymentStatus.AGUARDANDO_PAGAMENTO, order.PaymentStatus);
        Assert.Equal("MercadoPago", order.PaymentProvider);
        Assert.Equal("pref-1", order.PaymentPreferenceId);
        Assert.Equal("https://sandbox.example/checkout", order.PaymentCheckoutUrl);
        Assert.Equal("https://sandbox.example/checkout", result.CheckoutUrl);
        Assert.Equal("token-restaurante", fixture.MercadoPagoClient.LastPreferenceAccessToken);
        Assert.Equal(order.Id.ToString("D"), fixture.MercadoPagoClient.LastPreferenceRequest?.ExternalReference);
    }

    [Fact]
    public async Task WhatsAppDeliveryOrder_WithCustomerEmail_SendsPayerEmailToPreference()
    {
        await using var fixture = await PaymentFixture.CreateAsync();
        await fixture.SaveMercadoPagoSettingsAsync("token-restaurante", "secret-restaurante");

        await fixture.PaymentService.SubmitWhatsAppDeliveryOrderAsync(new WhatsAppDeliveryOrderSubmissionInput
        {
            RestaurantId = fixture.Restaurant.Id,
            CustomerName = "Maria",
            CustomerEmail = " maria.teste@example.com ",
            CustomerPhone = "11999999999",
            DeliveryAddress = "Rua Um, 123",
            Items = [new WhatsAppDeliveryOrderItemInput { ProductId = "prod-risoto", Quantity = 1 }]
        }, CancellationToken.None);

        Assert.Equal("maria.teste@example.com", fixture.MercadoPagoClient.LastPreferenceRequest?.Payer?.Email);
    }

    [Fact]
    public async Task TableOrder_DoesNotRequirePaymentToken()
    {
        await using var fixture = await PaymentFixture.CreateAsync();

        await fixture.PaymentService.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
        {
            RestaurantId = fixture.Restaurant.Id,
            TableId = fixture.Table.Id,
            Items = [new PublicOrderItemInput { MenuItemId = fixture.Risoto.Id, Quantity = 1 }]
        }, CancellationToken.None);

        var order = await fixture.Db.Orders.SingleAsync();
        Assert.Equal(OrderType.MESA, order.Type);
        Assert.Equal(PaymentStatus.NAO_APLICAVEL, order.PaymentStatus);
        Assert.Null(fixture.MercadoPagoClient.LastPreferenceRequest);
    }

    [Fact]
    public async Task MercadoPagoWebhook_ApprovedPayment_ApprovesOrder()
    {
        await using var fixture = await PaymentFixture.CreateAsync();
        await fixture.SaveMercadoPagoSettingsAsync("token-restaurante", "secret-restaurante");
        var order = await fixture.CreateDeliveryOrderAsync();
        fixture.MercadoPagoClient.Payment = fixture.CreatePayment("pay-1", order.Id, "approved");

        await fixture.ProcessWebhookAsync("pay-1", "secret-restaurante");

        await fixture.Db.Entry(order).ReloadAsync();
        Assert.Equal(PaymentStatus.PAGAMENTO_APROVADO, order.PaymentStatus);
        Assert.Equal("pay-1", order.PaymentId);
        Assert.NotNull(order.PaidAt);
    }

    [Fact]
    public async Task MercadoPagoWebhook_RejectedPayment_DeniesOrder()
    {
        await using var fixture = await PaymentFixture.CreateAsync();
        await fixture.SaveMercadoPagoSettingsAsync("token-restaurante", "secret-restaurante");
        var order = await fixture.CreateDeliveryOrderAsync();
        fixture.MercadoPagoClient.Payment = fixture.CreatePayment("pay-2", order.Id, "rejected");

        await fixture.ProcessWebhookAsync("pay-2", "secret-restaurante");

        await fixture.Db.Entry(order).ReloadAsync();
        Assert.Equal(PaymentStatus.PAGAMENTO_NEGADO, order.PaymentStatus);
        Assert.Equal("rejected", order.PaymentProviderStatus);
        Assert.Null(order.PaidAt);
    }

    [Fact]
    public async Task MercadoPagoWebhook_WithDifferentSeller_DoesNotApproveOrder()
    {
        await using var fixture = await PaymentFixture.CreateAsync();
        await fixture.SaveMercadoPagoSettingsAsync("token-restaurante", "secret-restaurante");
        var order = await fixture.CreateDeliveryOrderAsync();
        fixture.MercadoPagoClient.Payment = fixture.CreatePayment("pay-3", order.Id, "approved") with
        {
            CollectorId = "seller-2"
        };

        var result = await fixture.ProcessWebhookAsync("pay-3", "secret-restaurante");

        await fixture.Db.Entry(order).ReloadAsync();
        Assert.Equal("collector_mismatch", result.Status);
        Assert.Equal(PaymentStatus.AGUARDANDO_PAGAMENTO, order.PaymentStatus);
    }

    [Fact]
    public async Task MercadoPagoWebhook_DuplicateEvent_IsIgnored()
    {
        await using var fixture = await PaymentFixture.CreateAsync();
        await fixture.SaveMercadoPagoSettingsAsync("token-restaurante", "secret-restaurante");
        var order = await fixture.CreateDeliveryOrderAsync();
        fixture.MercadoPagoClient.Payment = fixture.CreatePayment("pay-4", order.Id, "approved");

        var first = await fixture.ProcessWebhookAsync("pay-4", "secret-restaurante");
        var second = await fixture.ProcessWebhookAsync("pay-4", "secret-restaurante");

        Assert.True(first.Updated);
        Assert.False(second.Updated);
        Assert.Equal("duplicate", second.Status);
        Assert.Equal(1, await fixture.Db.PaymentWebhookEvents.CountAsync());
    }


    private sealed class PaymentFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly string _dataProtectionPath;

        private PaymentFixture(
            SqliteConnection connection,
            string dataProtectionPath,
            ApplicationDbContext db,
            FakeMercadoPagoClient mercadoPagoClient,
            RestaurantPaymentSettingsService paymentSettingsService,
            PublicOrderPaymentService paymentService,
            MercadoPagoWebhookService webhookService,
            Restaurant restaurant,
            RestaurantTable table,
            MenuItem risoto)
        {
            _connection = connection;
            _dataProtectionPath = dataProtectionPath;
            Db = db;
            MercadoPagoClient = mercadoPagoClient;
            PaymentSettingsService = paymentSettingsService;
            PaymentService = paymentService;
            WebhookService = webhookService;
            Restaurant = restaurant;
            Table = table;
            Risoto = risoto;
        }

        public ApplicationDbContext Db { get; }
        public FakeMercadoPagoClient MercadoPagoClient { get; }
        public RestaurantPaymentSettingsService PaymentSettingsService { get; }
        public PublicOrderPaymentService PaymentService { get; }
        public MercadoPagoWebhookService WebhookService { get; }
        public Restaurant Restaurant { get; }
        public RestaurantTable Table { get; }
        public MenuItem Risoto { get; }

        public static async Task<PaymentFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var restaurant = new Restaurant { Name = "Teste", Slug = $"teste-{Guid.NewGuid():N}" };
            var table = new RestaurantTable { RestaurantId = restaurant.Id, TableNumber = "1" };
            var category = new MenuCategory { RestaurantId = restaurant.Id, Name = "Pratos" };
            var risoto = new MenuItem
            {
                RestaurantId = restaurant.Id,
                CategoryId = category.Id,
                Name = "Risoto",
                Description = "Teste",
                PriceCents = 4200,
                WhatsAppProductId = "prod-risoto"
            };
            db.Restaurants.Add(restaurant);
            db.RestaurantTables.Add(table);
            db.MenuCategories.Add(category);
            db.MenuItems.Add(risoto);
            await db.SaveChangesAsync();

            var dataProtectionPath = Path.Combine(Path.GetTempPath(), $"restaurantes-payment-tests-{Guid.NewGuid():N}");
            var dataProtectionProvider = DataProtectionProvider.Create(new DirectoryInfo(dataProtectionPath));
            var mercadoPagoClient = new FakeMercadoPagoClient();
            var paymentSettingsService = new RestaurantPaymentSettingsService(db, dataProtectionProvider, mercadoPagoClient);
            var restaurantService = new RestaurantService(db);
            var externalUrlResolver = new ExternalUrlResolver(
                new ConfigurationBuilder().Build(),
                new HttpContextAccessor
                {
                    HttpContext = new DefaultHttpContext
                    {
                        Request =
                        {
                            Scheme = "https",
                            Host = new HostString("app.example")
                        }
                    }
                });
            var paymentService = new PublicOrderPaymentService(
                restaurantService,
                paymentSettingsService,
                mercadoPagoClient,
                externalUrlResolver,
                Options.Create(new MercadoPagoOptions { UseSandboxCheckout = true }));
            var webhookService = new MercadoPagoWebhookService(
                db,
                paymentSettingsService,
                mercadoPagoClient,
                restaurantService);

            return new PaymentFixture(
                connection,
                dataProtectionPath,
                db,
                mercadoPagoClient,
                paymentSettingsService,
                paymentService,
                webhookService,
                restaurant,
                table,
                risoto);
        }

        public Task SaveMercadoPagoSettingsAsync(string accessToken, string webhookSecret)
        {
            return PaymentSettingsService.SaveMercadoPagoSettingsAsync(new RestaurantPaymentSettingsInput
            {
                RestaurantId = Restaurant.Id,
                AccessToken = accessToken,
                WebhookSecret = webhookSecret
            });
        }

        public async Task<Order> CreateDeliveryOrderAsync()
        {
            await new RestaurantService(Db).SubmitPublicOrderAsync(new PublicOrderSubmissionInput
            {
                RestaurantId = Restaurant.Id,
                CustomerName = "Maria",
                CustomerPhone = "11999999999",
                DeliveryAddress = "Rua Um, 123",
                Items = [new PublicOrderItemInput { MenuItemId = Risoto.Id, Quantity = 1 }]
            });

            return await Db.Orders.SingleAsync();
        }

        public MercadoPagoPaymentInfo CreatePayment(string paymentId, Guid orderId, string status)
        {
            return new MercadoPagoPaymentInfo(
                paymentId,
                status,
                "status-detail",
                orderId.ToString("D"),
                "seller-1",
                DateTimeOffset.UtcNow.AddMinutes(-2),
                DateTimeOffset.UtcNow,
                status == "approved" ? DateTimeOffset.UtcNow : null);
        }

        public async Task<MercadoPagoWebhookProcessResult> ProcessWebhookAsync(string paymentId, string webhookSecret)
        {
            var requestId = $"request-{paymentId}";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var signature = CreateSignature(paymentId, requestId, timestamp, webhookSecret);
            using var document = JsonDocument.Parse($$"""
                {
                  "id": "webhook-{{paymentId}}",
                  "type": "payment",
                  "action": "payment.updated",
                  "data": { "id": "{{paymentId}}" }
                }
                """);

            return await WebhookService.ProcessAsync(
                Restaurant.Id,
                document.RootElement,
                document.RootElement.GetRawText(),
                paymentId,
                signature,
                requestId,
                CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
            if (Directory.Exists(_dataProtectionPath))
            {
                try
                {
                    Directory.Delete(_dataProtectionPath, recursive: true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static string CreateSignature(string paymentId, string requestId, string timestamp, string secret)
        {
            var template = $"id:{paymentId};request-id:{requestId};ts:{timestamp};";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(template))).ToLowerInvariant();
            return $"ts={timestamp},v1={signature}";
        }
    }

    public sealed class FakeMercadoPagoClient : IMercadoPagoClient
    {
        public string? LastPreferenceAccessToken { get; private set; }
        public MercadoPagoPreferenceCreateRequest? LastPreferenceRequest { get; private set; }
        public MercadoPagoPaymentInfo? Payment { get; set; }

        public Task<MercadoPagoUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken)
        {
            return Task.FromResult(new MercadoPagoUserInfo("seller-1", "restaurante-teste"));
        }

        public Task<MercadoPagoPreferenceResult> CreatePreferenceAsync(
            string accessToken,
            MercadoPagoPreferenceCreateRequest request,
            CancellationToken cancellationToken)
        {
            LastPreferenceAccessToken = accessToken;
            LastPreferenceRequest = request;
            return Task.FromResult(new MercadoPagoPreferenceResult(
                "pref-1",
                "https://prod.example/checkout",
                "https://sandbox.example/checkout"));
        }

        public Task<MercadoPagoPaymentInfo> GetPaymentAsync(
            string accessToken,
            string paymentId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Payment ?? new MercadoPagoPaymentInfo(
                paymentId,
                "pending",
                null,
                null,
                "seller-1",
                null,
                null,
                null));
        }
    }
}
