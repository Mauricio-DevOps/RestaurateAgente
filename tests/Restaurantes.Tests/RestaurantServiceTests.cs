using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Restaurantes.Web.Data;
using Restaurantes.Web.Models;
using Restaurantes.Web.Services;

namespace Restaurantes.Tests;

public sealed class RestaurantServiceTests
{
    [Theory]
    [InlineData("(11) 99999-9999", "whatsapp:+5511999999999")]
    [InlineData("+55 11 99999-9999", "whatsapp:+5511999999999")]
    [InlineData("whatsapp:+5511999999999", "whatsapp:+5511999999999")]
    [InlineData("+1 415 523 8886", "whatsapp:+14155238886")]
    public void WhatsAppPhoneNormalizer_CreatesCanonicalStoreId(string input, string expected)
    {
        Assert.Equal(expected, WhatsAppPhoneNormalizer.Normalize(input));
    }

    [Fact]
    public async Task SaveWhatsAppPhone_NormalizesAndRejectsDuplicateRestaurantPhone()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var first = new Restaurant { Name = "Primeiro", Slug = "primeiro" };
        var second = new Restaurant { Name = "Segundo", Slug = "segundo" };
        db.Restaurants.AddRange(first, second);
        await db.SaveChangesAsync();

        var service = new RestaurantService(db);
        var update = await service.SaveWhatsAppPhoneAsync(first.Id, "(11) 99999-9999");

        Assert.Equal("whatsapp:+5511999999999", update.NewPhone);
        Assert.Equal("whatsapp:+5511999999999", await db.Restaurants.Where(item => item.Id == first.Id).Select(item => item.WhatsAppPhone).SingleAsync());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveWhatsAppPhoneAsync(second.Id, "+55 11 99999-9999"));
        Assert.Contains("outro restaurante", error.Message);
    }

    [Fact]
    public async Task SaveOperationalSla_StoresNullableStageLimits()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var restaurant = new Restaurant { Name = "Teste", Slug = "teste" };
        db.Restaurants.Add(restaurant);
        await db.SaveChangesAsync();

        var service = new RestaurantService(db);
        await service.SaveOperationalSlaAsync(restaurant.Id, new RestaurantSlaInput
        {
            PendingSlaMinutes = 3,
            AttendanceSlaMinutes = 7
        });

        var updated = await db.Restaurants.SingleAsync();
        Assert.Equal(3, updated.PendingSlaMinutes);
        Assert.Equal(7, updated.AttendanceSlaMinutes);

        await service.SaveOperationalSlaAsync(restaurant.Id, new RestaurantSlaInput());
        Assert.Null(updated.PendingSlaMinutes);
        Assert.Null(updated.AttendanceSlaMinutes);
    }

    [Fact]
    public async Task SyncMenuItemFromProduct_CreatesDraftInImportedCategory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var restaurant = new Restaurant
        {
            Name = "Teste",
            Slug = "teste",
            WhatsAppPhone = "whatsapp:+5511999999999"
        };
        db.Restaurants.Add(restaurant);
        await db.SaveChangesAsync();

        var service = new RestaurantService(db);
        var result = await service.SyncMenuItemFromProductAsync(new MenuItemSyncFromProductRequest(
            restaurant.WhatsAppPhone,
            "product-1",
            "Acai 500ml",
            "Copo de acai",
            18.5m));

        var item = await db.MenuItems.Include(menuItem => menuItem.Category).SingleAsync();
        Assert.True(result.Created);
        Assert.Equal(EntityStatus.INACTIVE, item.Status);
        Assert.Equal("Importados WhatsApp", item.Category!.Name);
        Assert.Equal("product-1", item.WhatsAppProductId);
        Assert.Equal(1850, item.PriceCents);
        Assert.Null(item.ImageUrl);

        db.ChangeTracker.Clear();
        var publicMenu = await service.GetPublicMenuAsync(restaurant.Id);
        Assert.Empty(publicMenu!.Categories.SelectMany(category => category.Items));
    }

    [Fact]
    public async Task SyncMenuItemFromProduct_ReusesProductLinkWithoutDuplicating()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var restaurant = new Restaurant
        {
            Name = "Teste",
            Slug = "teste",
            WhatsAppPhone = "whatsapp:+5511999999999"
        };
        db.Restaurants.Add(restaurant);
        await db.SaveChangesAsync();

        var service = new RestaurantService(db);
        await service.SyncMenuItemFromProductAsync(new MenuItemSyncFromProductRequest(
            restaurant.WhatsAppPhone,
            "product-1",
            "Pizza",
            "Broto",
            22m));
        var second = await service.SyncMenuItemFromProductAsync(new MenuItemSyncFromProductRequest(
            restaurant.WhatsAppPhone,
            "product-1",
            "Pizza Grande",
            "Grande",
            42m));

        var item = await db.MenuItems.SingleAsync();
        Assert.False(second.Created);
        Assert.Equal("Pizza Grande", item.Name);
        Assert.Equal("Grande", item.Description);
        Assert.Equal(4200, item.PriceCents);
        Assert.Equal(1, await db.MenuCategories.CountAsync(category => category.Name == "Importados WhatsApp"));
    }

    [Fact]
    public async Task SyncMenuItemFromProduct_PreservesExistingImageCategoryAndStatus()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var restaurant = new Restaurant
        {
            Name = "Teste",
            Slug = "teste",
            WhatsAppPhone = "whatsapp:+5511999999999"
        };
        var category = new MenuCategory { RestaurantId = restaurant.Id, Name = "Pratos" };
        var item = new MenuItem
        {
            RestaurantId = restaurant.Id,
            CategoryId = category.Id,
            Name = "Hamburguer",
            Description = "Antigo",
            PriceCents = 3000,
            ImageUrl = "/uploads/hamburguer.webp",
            WhatsAppProductId = "product-1",
            Status = EntityStatus.ACTIVE
        };
        db.Restaurants.Add(restaurant);
        db.MenuCategories.Add(category);
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();

        var service = new RestaurantService(db);
        await service.SyncMenuItemFromProductAsync(new MenuItemSyncFromProductRequest(
            restaurant.WhatsAppPhone,
            "product-1",
            "Hamburguer Especial",
            "Novo",
            36m));

        var updated = await db.MenuItems.SingleAsync();
        Assert.Equal(category.Id, updated.CategoryId);
        Assert.Equal(EntityStatus.ACTIVE, updated.Status);
        Assert.Equal("/uploads/hamburguer.webp", updated.ImageUrl);
        Assert.Equal("Hamburguer Especial", updated.Name);
        Assert.Equal("Novo", updated.Description);
        Assert.Equal(3600, updated.PriceCents);
    }

    [Fact]
    public async Task SubmitPublicOrder_CreatesOrderAndReusesOpenTab()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var restaurant = new Restaurant { Name = "Teste", Slug = "teste" };
        var category = new MenuCategory { RestaurantId = restaurant.Id, Name = "Pratos" };
        var table = new RestaurantTable { RestaurantId = restaurant.Id, TableNumber = "1" };
        var item = new MenuItem
        {
            RestaurantId = restaurant.Id,
            CategoryId = category.Id,
            Name = "Risoto",
            Description = "Teste",
            PriceCents = 4200
        };
        db.Restaurants.Add(restaurant);
        db.MenuCategories.Add(category);
        db.RestaurantTables.Add(table);
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();

        var service = new RestaurantService(db);
        var input = new PublicOrderSubmissionInput
        {
            RestaurantId = restaurant.Id,
            TableId = table.Id,
            Items = [new PublicOrderItemInput { MenuItemId = item.Id, Quantity = 2 }]
        };

        await service.SubmitPublicOrderAsync(input);
        await service.SubmitPublicOrderAsync(input);

        Assert.Equal(2, await db.Orders.CountAsync());
        Assert.Equal(1, await db.RestaurantTabs.CountAsync(tab => tab.Status == RestaurantTabStatus.ABERTA));
        Assert.Equal(16800, await db.RestaurantTabs.Select(tab => tab.TotalCents).SingleAsync());
    }

    [Fact]
    public async Task SubmitPublicOrder_AppliesPercentCouponAndTracksUsage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var fixture = await SeedOperationalFixtureAsync(db);
        var coupon = new DiscountCoupon
        {
            RestaurantId = fixture.Restaurant.Id,
            Code = "SABADOU",
            Type = DiscountCouponType.PERCENTUAL,
            Value = 10m
        };
        db.DiscountCoupons.Add(coupon);
        await db.SaveChangesAsync();

        var service = new RestaurantService(db);
        var validation = await service.ValidatePublicCouponAsync(new PublicCouponValidationInput
        {
            RestaurantId = fixture.Restaurant.Id,
            CouponCode = "sabadou",
            Items = [new PublicOrderItemInput { MenuItemId = fixture.Risoto.Id, Quantity = 2 }]
        });

        Assert.Equal("SABADOU", validation.CouponCode);
        Assert.Equal(8400, validation.SubtotalCents);
        Assert.Equal(840, validation.DiscountCents);
        Assert.Equal(7560, validation.TotalCents);

        await service.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
        {
            RestaurantId = fixture.Restaurant.Id,
            TableId = fixture.Table.Id,
            CouponCode = "sabadou",
            Items = [new PublicOrderItemInput { MenuItemId = fixture.Risoto.Id, Quantity = 2 }]
        });

        var order = await db.Orders.SingleAsync();
        Assert.Equal(8400, order.SubtotalCents);
        Assert.Equal(840, order.DiscountCents);
        Assert.Equal(7560, order.TotalCents);
        Assert.Equal("SABADOU", order.CouponCodeSnapshot);
        Assert.Equal(coupon.Id, order.DiscountCouponId);
        Assert.Equal(7560, await db.RestaurantTabs.Select(tab => tab.TotalCents).SingleAsync());

        var dashboard = await service.GetWaiterDashboardAsync(fixture.Restaurant.Id, fixture.Waiter.Id);
        var orderEvent = Assert.Single(dashboard.Queue.Where(item => item.EventKind == "ORDER"));
        Assert.Equal(840, orderEvent.DiscountCents);
        Assert.Contains("SABADOU", orderEvent.CouponSummary);
        Assert.Equal("R$ 75,60", orderEvent.TotalLabel);

        var editor = await service.GetMenuEditorAsync(fixture.Restaurant.Id);
        var couponView = Assert.Single(editor.Coupons);
        Assert.Equal(1, couponView.UsageCount);
        Assert.Equal("R$ 8,40", couponView.TotalDiscountLabel);
        Assert.NotEqual("Nunca usado", couponView.LastUsedAtLabel);
    }

    [Fact]
    public async Task GetPublicMenu_ResolvesTableNumberAndDeliveryMode()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var fixture = await SeedOperationalFixtureAsync(db);
        var service = new RestaurantService(db);

        var tableMenu = await service.GetPublicMenuAsync(fixture.Restaurant.Id, "1");
        var deliveryMenu = await service.GetPublicMenuAsync(fixture.Restaurant.Id);
        var invalidMenu = await service.GetPublicMenuAsync(fixture.Restaurant.Id, "99");

        Assert.NotNull(tableMenu);
        Assert.False(tableMenu.IsDelivery);
        Assert.False(tableMenu.HasInvalidTable);
        Assert.Equal(fixture.Table.Id, tableMenu.CurrentTableId);
        Assert.Equal("1", tableMenu.CurrentTableNumber);

        Assert.NotNull(deliveryMenu);
        Assert.True(deliveryMenu.IsDelivery);
        Assert.Null(deliveryMenu.CurrentTableId);

        Assert.NotNull(invalidMenu);
        Assert.False(invalidMenu.IsDelivery);
        Assert.True(invalidMenu.HasInvalidTable);
        Assert.Equal("99", invalidMenu.RequestedTableNumber);
    }

    [Fact]
    public async Task SubmitPublicOrder_CreatesDeliveryOrderWithoutTableOrTab()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var fixture = await SeedOperationalFixtureAsync(db);
        var service = new RestaurantService(db);

        await service.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
        {
            RestaurantId = fixture.Restaurant.Id,
            CustomerName = "Maria",
            CustomerPhone = "11999999999",
            DeliveryAddress = "Rua Um, 123",
            Items = [new PublicOrderItemInput { MenuItemId = fixture.Risoto.Id, Quantity = 1 }]
        });

        var order = await db.Orders.Include(item => item.Items).SingleAsync();
        Assert.Equal(OrderType.DELIVERY, order.Type);
        Assert.Null(order.TableId);
        Assert.Null(order.TabId);
        Assert.Equal("Maria", order.CustomerName);
        Assert.Equal("11999999999", order.CustomerPhone);
        Assert.Equal("Rua Um, 123", order.DeliveryAddress);
        Assert.Empty(await db.RestaurantTabs.ToListAsync());

        var deliveryOrders = await service.GetDeliveryOrdersAsync(fixture.Restaurant.Id);
        var deliveryOrder = Assert.Single(deliveryOrders);
        Assert.Equal("Maria", deliveryOrder.CustomerName);
        Assert.Equal("R$ 42,00", deliveryOrder.TotalLabel);
        Assert.Single(deliveryOrder.Items);
    }

    [Fact]
    public async Task SubmitPublicOrder_RejectsDeliveryWithoutRequiredCustomerFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var fixture = await SeedOperationalFixtureAsync(db);
        var service = new RestaurantService(db);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
            {
                RestaurantId = fixture.Restaurant.Id,
                CustomerName = "Maria",
                CustomerPhone = "11999999999",
                Items = [new PublicOrderItemInput { MenuItemId = fixture.Risoto.Id, Quantity = 1 }]
            }));

        Assert.Contains("endereco", error.Message);
        Assert.Empty(await db.Orders.ToListAsync());
    }

    [Fact]
    public async Task UpdateDeliveryOrderStatus_OnlyUpdatesDeliveryOrders()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var fixture = await SeedOperationalFixtureAsync(db);
        var service = new RestaurantService(db);

        await service.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
        {
            RestaurantId = fixture.Restaurant.Id,
            CustomerName = "Maria",
            CustomerPhone = "11999999999",
            DeliveryAddress = "Rua Um, 123",
            Items = [new PublicOrderItemInput { MenuItemId = fixture.Risoto.Id, Quantity = 1 }]
        });
        var order = await db.Orders.SingleAsync();

        await service.UpdateDeliveryOrderStatusAsync(fixture.Restaurant.Id, new DeliveryOrderStatusInput
        {
            OrderId = order.Id,
            NextStatus = OperationalEventStatus.RESOLVIDO
        });

        Assert.Equal(OperationalEventStatus.RESOLVIDO, order.Status);
        Assert.NotNull(order.AcknowledgedAt);
        Assert.NotNull(order.ResolvedAt);

        await service.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
        {
            RestaurantId = fixture.Restaurant.Id,
            TableId = fixture.Table.Id,
            Items = [new PublicOrderItemInput { MenuItemId = fixture.Suco.Id, Quantity = 1 }]
        });
        var tableOrder = await db.Orders.SingleAsync(item => item.Type == OrderType.MESA);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateDeliveryOrderStatusAsync(fixture.Restaurant.Id, new DeliveryOrderStatusInput
            {
                OrderId = tableOrder.Id,
                NextStatus = OperationalEventStatus.RESOLVIDO
            }));

        Assert.Contains("delivery", error.Message);
    }

    [Fact]
    public async Task ValidatePublicCoupon_RejectsInactiveCoupon()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var fixture = await SeedOperationalFixtureAsync(db);
        db.DiscountCoupons.Add(new DiscountCoupon
        {
            RestaurantId = fixture.Restaurant.Id,
            Code = "OFF10",
            Type = DiscountCouponType.VALOR_FIXO,
            Value = 10m,
            Status = EntityStatus.INACTIVE
        });
        await db.SaveChangesAsync();

        var service = new RestaurantService(db);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ValidatePublicCouponAsync(new PublicCouponValidationInput
            {
                RestaurantId = fixture.Restaurant.Id,
                CouponCode = "OFF10",
                Items = [new PublicOrderItemInput { MenuItemId = fixture.Risoto.Id, Quantity = 1 }]
            }));

        Assert.Contains("inativo", error.Message);
    }

    [Fact]
    public async Task SubmitPublicOrderFeedback_SavesFeedbackLinkedToOrder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var fixture = await SeedOperationalFixtureAsync(db);
        var service = new RestaurantService(db);

        await service.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
        {
            RestaurantId = fixture.Restaurant.Id,
            TableId = fixture.Table.Id,
            Items = [new PublicOrderItemInput { MenuItemId = fixture.Risoto.Id, Quantity = 1 }]
        });
        var order = await db.Orders.SingleAsync();

        await service.SubmitPublicOrderFeedbackAsync(fixture.Restaurant.Id, new PublicOrderFeedbackInput
        {
            OrderId = order.Id,
            Rating = 5,
            Comment = "Muito bom"
        });

        var feedback = await db.OrderFeedbacks.SingleAsync();
        Assert.Equal(fixture.Restaurant.Id, feedback.RestaurantId);
        Assert.Equal(fixture.Table.Id, feedback.TableId);
        Assert.Equal(order.Id, feedback.OrderId);
        Assert.Equal(5, feedback.Rating);
        Assert.Equal("Muito bom", feedback.Comment);
    }

    [Fact]
    public async Task SubmitPublicOrderFeedback_RejectsInvalidRatingAndDuplicateOrderFeedback()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var fixture = await SeedOperationalFixtureAsync(db);
        var service = new RestaurantService(db);

        await service.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
        {
            RestaurantId = fixture.Restaurant.Id,
            TableId = fixture.Table.Id,
            Items = [new PublicOrderItemInput { MenuItemId = fixture.Risoto.Id, Quantity = 1 }]
        });
        var order = await db.Orders.SingleAsync();

        var invalidRating = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitPublicOrderFeedbackAsync(fixture.Restaurant.Id, new PublicOrderFeedbackInput
            {
                OrderId = order.Id,
                Rating = 6
            }));
        Assert.Contains("1 a 5", invalidRating.Message);

        await service.SubmitPublicOrderFeedbackAsync(fixture.Restaurant.Id, new PublicOrderFeedbackInput
        {
            OrderId = order.Id,
            Rating = 4
        });

        var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitPublicOrderFeedbackAsync(fixture.Restaurant.Id, new PublicOrderFeedbackInput
            {
                OrderId = order.Id,
                Rating = 5
            }));
        Assert.Contains("ja recebeu", duplicate.Message);
    }

    [Fact]
    public async Task GetOverview_BuildsFeedbackAverageDistributionAndComments()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var fixture = await SeedOperationalFixtureAsync(db);
        var service = new RestaurantService(db);

        await service.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
        {
            RestaurantId = fixture.Restaurant.Id,
            TableId = fixture.Table.Id,
            Items = [new PublicOrderItemInput { MenuItemId = fixture.Risoto.Id, Quantity = 1 }]
        });
        await service.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
        {
            RestaurantId = fixture.Restaurant.Id,
            TableId = fixture.Table.Id,
            Items = [new PublicOrderItemInput { MenuItemId = fixture.Suco.Id, Quantity = 1 }]
        });
        var risotoOrder = await db.Orders.SingleAsync(order => order.TotalCents == 4200);
        var sucoOrder = await db.Orders.SingleAsync(order => order.TotalCents == 2100);

        await service.SubmitPublicOrderFeedbackAsync(fixture.Restaurant.Id, new PublicOrderFeedbackInput
        {
            OrderId = risotoOrder.Id,
            Rating = 5,
            Comment = "Excelente"
        });
        await service.SubmitPublicOrderFeedbackAsync(fixture.Restaurant.Id, new PublicOrderFeedbackInput
        {
            OrderId = sucoOrder.Id,
            Rating = 3
        });

        var overview = await service.GetOverviewAsync(fixture.Restaurant.Id);

        Assert.Equal(2, overview.Feedback.TotalFeedbacks);
        Assert.Equal(4.0m, overview.Feedback.AverageRating);
        Assert.Equal("4,0 / 5", overview.Feedback.AverageRatingLabel);
        Assert.Equal(1, overview.Feedback.Ratings.Single(item => item.Rating == 5).Count);
        Assert.Equal(1, overview.Feedback.Ratings.Single(item => item.Rating == 3).Count);
        Assert.Equal(50, overview.Feedback.Ratings.Single(item => item.Rating == 5).Percentage);
        Assert.Contains(overview.Feedback.Comments, item =>
            item.Rating == 5 &&
            item.Comment == "Excelente" &&
            item.TableNumber == fixture.Table.TableNumber &&
            item.TotalLabel == "R$ 42,00");
        Assert.Contains(overview.Feedback.Comments, item =>
            item.Rating == 3 &&
            item.Comment == "Cliente nao deixou comentario.");
    }

    [Fact]
    public async Task DeleteMenuItem_RemovesMenuItemAndPreservesOrderHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var restaurant = new Restaurant { Name = "Teste", Slug = "teste" };
        var category = new MenuCategory { RestaurantId = restaurant.Id, Name = "Pratos" };
        var table = new RestaurantTable { RestaurantId = restaurant.Id, TableNumber = "1" };
        var item = new MenuItem
        {
            RestaurantId = restaurant.Id,
            CategoryId = category.Id,
            Name = "Acai de 500",
            Description = "Teste",
            PriceCents = 3000
        };
        db.Restaurants.Add(restaurant);
        db.MenuCategories.Add(category);
        db.RestaurantTables.Add(table);
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();

        var service = new RestaurantService(db);
        await service.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
        {
            RestaurantId = restaurant.Id,
            TableId = table.Id,
            Items = [new PublicOrderItemInput { MenuItemId = item.Id, Quantity = 1 }]
        });

        await service.DeleteMenuItemAsync(restaurant.Id, item.Id);

        Assert.False(await db.MenuItems.AnyAsync(menuItem => menuItem.Id == item.Id));
        var orderItem = await db.OrderItems.SingleAsync();
        Assert.Null(orderItem.MenuItemId);
        Assert.Equal("Acai de 500", orderItem.ItemNameSnapshot);
        Assert.Equal(3000, orderItem.ItemPriceCentsSnapshot);
    }

    [Fact]
    public async Task GetPublicRestaurantTableSession_ReturnsValidityAndOpenTabState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var restaurant = new Restaurant { Name = "Teste", Slug = "teste" };
        var table = new RestaurantTable { RestaurantId = restaurant.Id, TableNumber = "9" };
        db.Restaurants.Add(restaurant);
        db.RestaurantTables.Add(table);
        db.RestaurantTabs.Add(new RestaurantTab { RestaurantId = restaurant.Id, TableId = table.Id });
        await db.SaveChangesAsync();

        var service = new RestaurantService(db);
        var session = await service.GetPublicRestaurantTableSessionAsync(restaurant.Id, table.Id);

        Assert.NotNull(session);
        Assert.True(session.HasOpenTab);
        Assert.Equal("9", session.TableNumber);
    }

    [Fact]
    public async Task GetPublicMenu_GeneratesSafePaletteFromBrandColor()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var restaurant = new Restaurant
        {
            Name = "Acai",
            Slug = "acai",
            PrimaryColor = "#5B4FC8",
            MenuTheme = "ELEGANTE",
            MenuMode = "CLARO"
        };
        db.Restaurants.Add(restaurant);
        db.MenuCategories.Add(new MenuCategory { RestaurantId = restaurant.Id, Name = "Acai" });
        await db.SaveChangesAsync();

        var service = new RestaurantService(db);
        var menu = await service.GetPublicMenuAsync(restaurant.Id);

        Assert.NotNull(menu);
        Assert.Equal("#5B4FC8", menu.PrimaryColor);
        Assert.Equal("ELEGANTE", menu.MenuTheme);
        Assert.Equal("CLARO", menu.MenuMode);
        Assert.NotEqual(menu.PrimaryColor, menu.BackgroundColor);
        Assert.NotEqual(menu.PrimaryColor, menu.SurfaceColor);
        Assert.StartsWith("#", menu.BackgroundColor);
        Assert.StartsWith("#", menu.BorderColor);
    }

    [Fact]
    public async Task ServiceRequest_AppearsInWaiterQueue()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var fixture = await SeedOperationalFixtureAsync(db);
        var service = new RestaurantService(db);

        await service.CreatePublicServiceRequestAsync(new PublicServiceRequestInput
        {
            RestaurantId = fixture.Restaurant.Id,
            TableId = fixture.Table.Id,
            Type = ServiceRequestType.CHAMAR_GARCOM
        });

        var dashboard = await service.GetWaiterDashboardAsync(fixture.Restaurant.Id, fixture.Waiter.Id);
        var request = Assert.Single(dashboard.Queue.Where(item => item.RequestType == ServiceRequestType.CHAMAR_GARCOM));

        Assert.Equal("SERVICE_REQUEST", request.EventKind);
        Assert.Equal(OperationalEventStatus.PENDENTE, request.Status);
        Assert.Contains("Chamado - mesa 1", request.Title);
        Assert.Equal("Sua mesa", request.OwnershipLabel);
    }

    [Fact]
    public async Task GetWaiterDashboard_MarksOnlyOrdersOutsideConfiguredSla()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var fixture = await SeedOperationalFixtureAsync(db);
        fixture.Restaurant.PendingSlaMinutes = 1;
        fixture.Restaurant.AttendanceSlaMinutes = 1;
        var service = new RestaurantService(db);

        await service.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
        {
            RestaurantId = fixture.Restaurant.Id,
            TableId = fixture.Table.Id,
            Items = [new PublicOrderItemInput { MenuItemId = fixture.Risoto.Id, Quantity = 1 }]
        });
        await service.CreatePublicServiceRequestAsync(new PublicServiceRequestInput
        {
            RestaurantId = fixture.Restaurant.Id,
            TableId = fixture.Table.Id,
            Type = ServiceRequestType.CHAMAR_GARCOM
        });

        var oldCreatedAt = DateTimeOffset.UtcNow.AddMinutes(-3);
        foreach (var order in await db.Orders.ToListAsync())
        {
            order.CreatedAt = oldCreatedAt;
        }
        foreach (var request in await db.ServiceRequests.ToListAsync())
        {
            request.CreatedAt = oldCreatedAt;
        }
        await db.SaveChangesAsync();

        var dashboard = await service.GetWaiterDashboardAsync(fixture.Restaurant.Id, fixture.Waiter.Id);
        var orderEvent = Assert.Single(dashboard.Queue.Where(item => item.EventKind == "ORDER"));
        var requestEvent = Assert.Single(dashboard.Queue.Where(item => item.EventKind == "SERVICE_REQUEST"));

        Assert.True(orderEvent.IsOverSla);
        Assert.Equal("Pendente", orderEvent.CurrentStageLabel);
        Assert.Equal(1, orderEvent.CurrentSlaMinutes);
        Assert.False(requestEvent.IsOverSla);
        Assert.Null(requestEvent.CurrentSlaMinutes);
    }

    [Fact]
    public async Task AccountRequest_ShowsOpenTabItemsAndTotalInWaiterQueue()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var fixture = await SeedOperationalFixtureAsync(db);
        var service = new RestaurantService(db);

        await service.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
        {
            RestaurantId = fixture.Restaurant.Id,
            TableId = fixture.Table.Id,
            Items = [new PublicOrderItemInput { MenuItemId = fixture.Risoto.Id, Quantity = 1 }]
        });
        await service.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
        {
            RestaurantId = fixture.Restaurant.Id,
            TableId = fixture.Table.Id,
            Items =
            [
                new PublicOrderItemInput { MenuItemId = fixture.Risoto.Id, Quantity = 2 },
                new PublicOrderItemInput { MenuItemId = fixture.Suco.Id, Quantity = 1 }
            ]
        });
        await service.CreatePublicServiceRequestAsync(new PublicServiceRequestInput
        {
            RestaurantId = fixture.Restaurant.Id,
            TableId = fixture.Table.Id,
            Type = ServiceRequestType.PEDIR_CONTA
        });

        var dashboard = await service.GetWaiterDashboardAsync(fixture.Restaurant.Id, fixture.Waiter.Id);
        var account = Assert.Single(dashboard.Queue.Where(item => item.RequestType == ServiceRequestType.PEDIR_CONTA));
        var linkedRequest = await db.ServiceRequests.SingleAsync(request => request.Type == ServiceRequestType.PEDIR_CONTA);

        Assert.NotNull(linkedRequest.TabId);
        Assert.Equal(14700, account.TotalCents);
        Assert.Equal("R$ 147,00", account.TotalLabel);
        Assert.Contains("total R$ 147,00", account.Summary);
        Assert.Equal(2, account.Items.Count);
        Assert.Contains(account.Items, item => item.Name == "Risoto" && item.Quantity == 3 && item.LineTotalLabel == "R$ 126,00");
        Assert.Contains(account.Items, item => item.Name == "Suco" && item.Quantity == 1 && item.LineTotalLabel == "R$ 21,00");
    }

    [Fact]
    public async Task ResolvingAccountRequest_ClosesTabResolvesOrdersAndNextOrderCreatesNewTab()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var fixture = await SeedOperationalFixtureAsync(db);
        var service = new RestaurantService(db);

        await service.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
        {
            RestaurantId = fixture.Restaurant.Id,
            TableId = fixture.Table.Id,
            Items = [new PublicOrderItemInput { MenuItemId = fixture.Risoto.Id, Quantity = 1 }]
        });
        var originalTab = await db.RestaurantTabs.SingleAsync();
        await service.CreatePublicServiceRequestAsync(new PublicServiceRequestInput
        {
            RestaurantId = fixture.Restaurant.Id,
            TableId = fixture.Table.Id,
            Type = ServiceRequestType.PEDIR_CONTA
        });
        var accountRequest = await db.ServiceRequests.SingleAsync(request => request.Type == ServiceRequestType.PEDIR_CONTA);

        await service.UpdateOperationalEventStatusAsync(fixture.Restaurant.Id, fixture.Waiter.Id, new UpdateOperationalEventStatusInput
        {
            EventKind = "SERVICE_REQUEST",
            EventId = accountRequest.Id,
            NextStatus = OperationalEventStatus.RESOLVIDO
        });

        var sessionAfterClose = await service.GetPublicRestaurantTableSessionAsync(fixture.Restaurant.Id, fixture.Table.Id);
        var closedTab = await db.RestaurantTabs.SingleAsync(tab => tab.Id == originalTab.Id);
        var resolvedOrder = await db.Orders.SingleAsync();

        Assert.False(sessionAfterClose!.HasOpenTab);
        Assert.Equal(RestaurantTabStatus.FECHADA, closedTab.Status);
        Assert.NotNull(closedTab.ClosedAt);
        Assert.Equal(OperationalEventStatus.RESOLVIDO, resolvedOrder.Status);
        Assert.Equal(fixture.Waiter.Id, resolvedOrder.HandledByWaiterId);

        await service.SubmitPublicOrderAsync(new PublicOrderSubmissionInput
        {
            RestaurantId = fixture.Restaurant.Id,
            TableId = fixture.Table.Id,
            Items = [new PublicOrderItemInput { MenuItemId = fixture.Suco.Id, Quantity = 1 }]
        });

        Assert.Equal(2, await db.RestaurantTabs.CountAsync());
        Assert.Equal(1, await db.RestaurantTabs.CountAsync(tab => tab.Status == RestaurantTabStatus.ABERTA));
        Assert.NotEqual(originalTab.Id, await db.RestaurantTabs.Where(tab => tab.Status == RestaurantTabStatus.ABERTA).Select(tab => tab.Id).SingleAsync());
    }

    [Fact]
    public async Task AccountRequestWithoutOpenTab_IsRejected()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var fixture = await SeedOperationalFixtureAsync(db);
        var service = new RestaurantService(db);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePublicServiceRequestAsync(new PublicServiceRequestInput
        {
            RestaurantId = fixture.Restaurant.Id,
            TableId = fixture.Table.Id,
            Type = ServiceRequestType.PEDIR_CONTA
        }));

        Assert.Contains("comanda aberta", error.Message);
    }

    private static async Task<OperationalFixture> SeedOperationalFixtureAsync(ApplicationDbContext db)
    {
        var restaurant = new Restaurant { Name = "Teste", Slug = $"teste-{Guid.NewGuid():N}" };
        var waiter = new RestaurantWaiter { RestaurantId = restaurant.Id, Name = "Ana" };
        var table = new RestaurantTable { RestaurantId = restaurant.Id, TableNumber = "1", AssignedWaiterId = waiter.Id };
        var category = new MenuCategory { RestaurantId = restaurant.Id, Name = "Pratos" };
        var risoto = new MenuItem
        {
            RestaurantId = restaurant.Id,
            CategoryId = category.Id,
            Name = "Risoto",
            Description = "Teste",
            PriceCents = 4200
        };
        var suco = new MenuItem
        {
            RestaurantId = restaurant.Id,
            CategoryId = category.Id,
            Name = "Suco",
            Description = "Teste",
            PriceCents = 2100
        };

        db.Restaurants.Add(restaurant);
        db.RestaurantWaiters.Add(waiter);
        db.RestaurantTables.Add(table);
        db.MenuCategories.Add(category);
        db.MenuItems.AddRange(risoto, suco);
        await db.SaveChangesAsync();

        return new OperationalFixture(restaurant, waiter, table, risoto, suco);
    }

    private sealed record OperationalFixture(
        Restaurant Restaurant,
        RestaurantWaiter Waiter,
        RestaurantTable Table,
        MenuItem Risoto,
        MenuItem Suco);
}
