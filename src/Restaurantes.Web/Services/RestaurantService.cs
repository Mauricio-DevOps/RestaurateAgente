using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Restaurantes.Web.Auth;
using Restaurantes.Web.Data;
using Restaurantes.Web.Models;

namespace Restaurantes.Web.Services;

public sealed class RestaurantService
{
    private readonly ApplicationDbContext _db;

    public RestaurantService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<RestaurantOverviewView> GetOverviewAsync(Guid restaurantId)
    {
        var restaurant = await RequireRestaurantAsync(restaurantId);
        var feedbacks = (await _db.OrderFeedbacks
            .Include(feedback => feedback.Order)
            .Include(feedback => feedback.Table)
            .Where(feedback => feedback.RestaurantId == restaurantId)
            .ToListAsync())
            .OrderByDescending(feedback => feedback.CreatedAt)
            .ToList();

        return new RestaurantOverviewView
        {
            RestaurantId = restaurant.Id,
            RestaurantName = restaurant.Name,
            PublicMenuUrl = $"/cardapio/{restaurant.Id}",
            CategoryCount = await _db.MenuCategories.CountAsync(category => category.RestaurantId == restaurantId),
            ActiveItemCount = await _db.MenuItems.CountAsync(item => item.RestaurantId == restaurantId && item.Status == EntityStatus.ACTIVE),
            WaiterCount = await _db.RestaurantWaiters.CountAsync(waiter => waiter.RestaurantId == restaurantId),
            TableCount = await _db.RestaurantTables.CountAsync(table => table.RestaurantId == restaurantId),
            Feedback = BuildFeedbackDashboard(feedbacks)
        };
    }

    public async Task<RestaurantMenuEditorView> GetMenuEditorAsync(Guid restaurantId)
    {
        var restaurant = await RequireRestaurantAsync(restaurantId);
        var categories = await _db.MenuCategories
            .Include(category => category.Items)
            .Where(category => category.RestaurantId == restaurantId)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .ToListAsync();
        var coupons = await _db.DiscountCoupons
            .Where(coupon => coupon.RestaurantId == restaurantId)
            .OrderBy(coupon => coupon.Code)
            .ToListAsync();
        var tables = await _db.RestaurantTables
            .Where(table => table.RestaurantId == restaurantId)
            .OrderBy(table => table.TableNumber)
            .ToListAsync();
        var couponIds = coupons.Select(coupon => coupon.Id).ToArray();
        List<CouponOrderUsage> couponOrders = [];
        if (couponIds.Length > 0)
        {
            couponOrders = await _db.Orders
                .Where(order => order.RestaurantId == restaurantId &&
                    order.DiscountCouponId.HasValue &&
                    couponIds.Contains(order.DiscountCouponId.Value))
                .Select(order => new CouponOrderUsage(
                    order.DiscountCouponId!.Value,
                    order.DiscountCents,
                    order.CreatedAt))
                .ToListAsync();
        }
        var couponUsageById = couponOrders
            .GroupBy(order => order.CouponId)
            .ToDictionary(
                group => group.Key,
                group => new CouponUsageStats(
                    group.Count(),
                    group.Sum(order => order.DiscountCents),
                    group.Max(order => order.CreatedAt)));
        var brandColor = NormalizeBrandColor(restaurant.PrimaryColor, "#B14623");
        var menuTheme = NormalizeMenuTheme(restaurant.MenuTheme);
        var menuMode = NormalizeMenuMode(restaurant.MenuMode);
        var palette = BuildMenuPalette(brandColor, menuTheme, menuMode);
        var now = DateTimeOffset.Now;

        return new RestaurantMenuEditorView
        {
            RestaurantId = restaurant.Id,
            RestaurantName = restaurant.Name,
            PublicDescription = restaurant.PublicDescription,
            CoverImageUrl = restaurant.CoverImageUrl,
            PrimaryColor = brandColor,
            SecondaryColor = palette.TextColor,
            BackgroundColor = palette.BackgroundColor,
            MenuTheme = menuTheme,
            MenuMode = menuMode,
            Tables = tables.Select(table => new RestaurantTableView
            {
                Id = table.Id,
                TableNumber = table.TableNumber
            }).ToList(),
            Coupons = coupons.Select(coupon =>
                MapDiscountCoupon(coupon, couponUsageById.GetValueOrDefault(coupon.Id))).ToList(),
            Categories = categories.Select(category => MapCategory(category, now)).ToList()
        };
    }

    public async Task UpdateRestaurantBrandAsync(Guid restaurantId, string name, string? description, string primaryColor, string menuTheme, string menuMode, string? coverImageUrl)
    {
        var restaurant = await RequireRestaurantAsync(restaurantId);
        var brandColor = NormalizeBrandColor(primaryColor, "#B14623");
        var normalizedTheme = NormalizeMenuTheme(menuTheme);
        var normalizedMode = NormalizeMenuMode(menuMode);
        var palette = BuildMenuPalette(brandColor, normalizedTheme, normalizedMode);

        restaurant.Name = name.Trim();
        restaurant.PublicDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        restaurant.PrimaryColor = brandColor;
        restaurant.SecondaryColor = palette.TextColor;
        restaurant.BackgroundColor = palette.BackgroundColor;
        restaurant.MenuTheme = normalizedTheme;
        restaurant.MenuMode = normalizedMode;
        if (!string.IsNullOrWhiteSpace(coverImageUrl))
        {
            restaurant.CoverImageUrl = coverImageUrl;
        }
        restaurant.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task AddCategoryAsync(Guid restaurantId, string name)
    {
        var sortOrder = await _db.MenuCategories.Where(category => category.RestaurantId == restaurantId).CountAsync() + 1;
        _db.MenuCategories.Add(new MenuCategory
        {
            RestaurantId = restaurantId,
            Name = name.Trim(),
            SortOrder = sortOrder
        });
        await _db.SaveChangesAsync();
    }

    public async Task ToggleCategoryAsync(Guid restaurantId, Guid categoryId)
    {
        var category = await RequireCategoryAsync(restaurantId, categoryId);
        category.Status = category.Status == EntityStatus.ACTIVE ? EntityStatus.INACTIVE : EntityStatus.ACTIVE;
        category.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<MenuItemWhatsAppSyncContext> AddMenuItemAsync(Guid restaurantId, Guid categoryId, string name, string description, string priceInput, string? imageUrl, MenuItemPromotionInput promotion)
    {
        var restaurant = await RequireRestaurantAsync(restaurantId);
        _ = await RequireCategoryAsync(restaurantId, categoryId);
        var sortOrder = await _db.MenuItems.Where(item => item.CategoryId == categoryId).CountAsync() + 1;
        var item = new MenuItem
        {
            RestaurantId = restaurantId,
            CategoryId = categoryId,
            Name = name.Trim(),
            Description = description.Trim(),
            PriceCents = RestaurantText.ParsePriceToCents(priceInput),
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl,
            SortOrder = sortOrder
        };
        ApplyPromotion(item, promotion);
        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync();

        return BuildWhatsAppSyncContext(restaurant, item);
    }

    public async Task<MenuItemWhatsAppSyncContext> UpdateMenuItemAsync(Guid restaurantId, Guid itemId, string name, string description, string priceInput, string? imageUrl, MenuItemPromotionInput promotion)
    {
        var restaurant = await RequireRestaurantAsync(restaurantId);
        var item = await _db.MenuItems.FirstOrDefaultAsync(menuItem => menuItem.RestaurantId == restaurantId && menuItem.Id == itemId)
            ?? throw new InvalidOperationException("Item nÃ£o encontrado.");

        item.Name = name.Trim();
        item.Description = description.Trim();
        item.PriceCents = RestaurantText.ParsePriceToCents(priceInput);
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            item.ImageUrl = imageUrl;
        }
        ApplyPromotion(item, promotion);
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return BuildWhatsAppSyncContext(restaurant, item);
    }

    public async Task LinkMenuItemToWhatsAppProductAsync(Guid restaurantId, Guid itemId, string productId)
    {
        var item = await _db.MenuItems.FirstOrDefaultAsync(menuItem => menuItem.RestaurantId == restaurantId && menuItem.Id == itemId)
            ?? throw new InvalidOperationException("Item nÃ£o encontrado.");

        item.WhatsAppProductId = productId.Trim();
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<MenuItemSyncFromProductResponse> SyncMenuItemFromProductAsync(MenuItemSyncFromProductRequest request)
    {
        var storeId = request.StoreId.Trim();
        var productId = request.ProductId.Trim();
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(storeId) || string.IsNullOrWhiteSpace(productId) || string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("StoreId, ProductId e Name sÃ£o obrigatÃ³rios.");
        }

        var restaurant = await _db.Restaurants.FirstOrDefaultAsync(item => item.WhatsAppPhone == storeId)
            ?? throw new InvalidOperationException("Restaurante nÃ£o encontrado para este WhatsApp.");
        var priceCents = ToPriceCents(request.RetailPrice);
        var description = string.IsNullOrWhiteSpace(request.Description) ? "" : request.Description.Trim();

        var item = await _db.MenuItems.FirstOrDefaultAsync(menuItem =>
            menuItem.RestaurantId == restaurant.Id &&
            menuItem.WhatsAppProductId == productId);
        if (item is null)
        {
            var normalizedName = NormalizeMenuItemName(name);
            var menuItems = await _db.MenuItems
                .Where(menuItem => menuItem.RestaurantId == restaurant.Id)
                .ToListAsync();
            item = menuItems.FirstOrDefault(menuItem =>
                string.Equals(NormalizeMenuItemName(menuItem.Name), normalizedName, StringComparison.Ordinal));
        }

        var created = false;
        if (item is null)
        {
            var category = await GetOrCreateImportedWhatsAppCategoryAsync(restaurant.Id);
            var sortOrder = await _db.MenuItems.Where(menuItem => menuItem.CategoryId == category.Id).CountAsync() + 1;
            item = new MenuItem
            {
                RestaurantId = restaurant.Id,
                CategoryId = category.Id,
                Name = name,
                Description = description,
                PriceCents = priceCents,
                WhatsAppProductId = productId,
                Status = EntityStatus.INACTIVE,
                SortOrder = sortOrder
            };
            _db.MenuItems.Add(item);
            created = true;
        }
        else
        {
            item.Name = name;
            item.Description = description;
            item.PriceCents = priceCents;
            item.WhatsAppProductId = productId;
            item.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();

        return new MenuItemSyncFromProductResponse(
            item.Id,
            item.RestaurantId,
            item.CategoryId,
            productId,
            item.Name,
            item.Status,
            created);
    }

    public async Task ToggleMenuItemAsync(Guid restaurantId, Guid itemId)
    {
        var item = await _db.MenuItems.FirstOrDefaultAsync(menuItem => menuItem.RestaurantId == restaurantId && menuItem.Id == itemId)
            ?? throw new InvalidOperationException("Item não encontrado.");
        item.Status = item.Status == EntityStatus.ACTIVE ? EntityStatus.INACTIVE : EntityStatus.ACTIVE;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteMenuItemAsync(Guid restaurantId, Guid itemId)
    {
        var item = await _db.MenuItems.FirstOrDefaultAsync(menuItem => menuItem.RestaurantId == restaurantId && menuItem.Id == itemId)
            ?? throw new InvalidOperationException("Item não encontrado.");
        await _db.OrderItems
            .Where(orderItem => orderItem.MenuItemId == item.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(orderItem => orderItem.MenuItemId, (Guid?)null));

        _db.MenuItems.Remove(item);
        await _db.SaveChangesAsync();
    }

    public async Task AddDiscountCouponAsync(Guid restaurantId, DiscountCouponInput input)
    {
        _ = await RequireRestaurantAsync(restaurantId);
        var normalized = NormalizeDiscountCouponInput(input);
        var exists = await _db.DiscountCoupons.AnyAsync(coupon =>
            coupon.RestaurantId == restaurantId &&
            coupon.Code == normalized.Code);
        if (exists)
        {
            throw new InvalidOperationException("Ja existe um cupom com esse codigo.");
        }

        _db.DiscountCoupons.Add(new DiscountCoupon
        {
            RestaurantId = restaurantId,
            Code = normalized.Code,
            Type = normalized.Type,
            Value = normalized.Value
        });
        await _db.SaveChangesAsync();
    }

    public async Task UpdateDiscountCouponAsync(Guid restaurantId, Guid couponId, DiscountCouponInput input)
    {
        var coupon = await RequireDiscountCouponAsync(restaurantId, couponId);
        var normalized = NormalizeDiscountCouponInput(input);
        var exists = await _db.DiscountCoupons.AnyAsync(item =>
            item.RestaurantId == restaurantId &&
            item.Id != couponId &&
            item.Code == normalized.Code);
        if (exists)
        {
            throw new InvalidOperationException("Ja existe um cupom com esse codigo.");
        }

        coupon.Code = normalized.Code;
        coupon.Type = normalized.Type;
        coupon.Value = normalized.Value;
        coupon.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task ToggleDiscountCouponAsync(Guid restaurantId, Guid couponId)
    {
        var coupon = await RequireDiscountCouponAsync(restaurantId, couponId);
        coupon.Status = coupon.Status == EntityStatus.ACTIVE ? EntityStatus.INACTIVE : EntityStatus.ACTIVE;
        coupon.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<RestaurantOperationsView> GetOperationsAsync(Guid restaurantId)
    {
        var restaurant = await RequireRestaurantAsync(restaurantId);
        var waiters = await _db.RestaurantWaiters
            .Where(waiter => waiter.RestaurantId == restaurantId)
            .OrderBy(waiter => waiter.Name)
            .ToListAsync();
        var tables = await _db.RestaurantTables
            .Include(table => table.AssignedWaiter)
            .Where(table => table.RestaurantId == restaurantId)
            .OrderBy(table => table.TableNumber)
            .ToListAsync();
        var orders = await _db.Orders
            .Include(order => order.Table)
            .Where(order => order.RestaurantId == restaurantId && order.Type == OrderType.MESA)
            .ToListAsync();
        var waiterLogin = await (
            from user in _db.Users
            join userRole in _db.UserRoles on user.Id equals userRole.UserId
            join role in _db.Roles on userRole.RoleId equals role.Id
            where user.RestaurantId == restaurantId && role.Name == AppRoles.Garcom
            orderby user.Email
            select new WaiterLoginView
            {
                HasLogin = true,
                Email = user.Email ?? "",
                FullName = user.FullName,
                Status = user.ProfileStatus
            })
            .FirstOrDefaultAsync();
        var now = DateTimeOffset.UtcNow;

        return new RestaurantOperationsView
        {
            RestaurantId = restaurant.Id,
            RestaurantName = restaurant.Name,
            WhatsAppPhone = restaurant.WhatsAppPhone ?? "",
            PendingSlaMinutes = restaurant.PendingSlaMinutes,
            AttendanceSlaMinutes = restaurant.AttendanceSlaMinutes,
            WaiterLogin = waiterLogin ?? new WaiterLoginView(),
            Waiters = waiters.Select(waiter => new RestaurantWaiterView { Id = waiter.Id, Name = waiter.Name }).ToList(),
            Tables = tables.Select(table => new RestaurantTableView
            {
                Id = table.Id,
                TableNumber = table.TableNumber,
                AssignedWaiterId = table.AssignedWaiterId,
                AssignedWaiterName = table.AssignedWaiter?.Name
            }).ToList(),
            Bottlenecks = BuildOperationalBottlenecks(orders, restaurant.PendingSlaMinutes, restaurant.AttendanceSlaMinutes, now)
        };
    }

    public async Task SaveOperationalSlaAsync(Guid restaurantId, RestaurantSlaInput input)
    {
        var restaurant = await RequireRestaurantAsync(restaurantId);
        if (input.PendingSlaMinutes is <= 0 || input.AttendanceSlaMinutes is <= 0)
        {
            throw new InvalidOperationException("Informe tempos de SLA maiores que zero ou deixe o campo vazio.");
        }

        restaurant.PendingSlaMinutes = input.PendingSlaMinutes;
        restaurant.AttendanceSlaMinutes = input.AttendanceSlaMinutes;
        restaurant.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<RestaurantWhatsAppPhoneUpdate> SaveWhatsAppPhoneAsync(Guid restaurantId, string phone)
    {
        var restaurant = await RequireRestaurantAsync(restaurantId);
        var normalizedPhone = WhatsAppPhoneNormalizer.Normalize(phone);
        var isPhoneUsed = await _db.Restaurants.AnyAsync(item =>
            item.Id != restaurantId &&
            item.WhatsAppPhone == normalizedPhone);

        if (isPhoneUsed)
        {
            throw new InvalidOperationException("Esse telefone de WhatsApp ja esta vinculado a outro restaurante.");
        }

        var previousPhone = restaurant.WhatsAppPhone;
        restaurant.WhatsAppPhone = normalizedPhone;
        restaurant.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return new RestaurantWhatsAppPhoneUpdate(
            restaurant.Id,
            restaurant.Name,
            previousPhone,
            normalizedPhone);
    }

    public async Task<RestaurantWhatsAppSsoContext?> GetWhatsAppSsoContextAsync(Guid restaurantId)
    {
        var restaurant = await RequireRestaurantAsync(restaurantId);
        if (string.IsNullOrWhiteSpace(restaurant.WhatsAppPhone))
        {
            return null;
        }

        return new RestaurantWhatsAppSsoContext(
            restaurant.Id,
            restaurant.Name,
            restaurant.WhatsAppPhone,
            restaurant.AccessMode);
    }

    public async Task AddWaiterAsync(Guid restaurantId, string name)
    {
        var normalizedName = RestaurantText.NormalizeWaiterName(name);
        var currentNames = await _db.RestaurantWaiters.Where(waiter => waiter.RestaurantId == restaurantId).Select(waiter => waiter.Name).ToListAsync();
        RestaurantText.EnsureNoDuplicateWaiters(currentNames.Append(normalizedName));
        _db.RestaurantWaiters.Add(new RestaurantWaiter { RestaurantId = restaurantId, Name = normalizedName });
        await _db.SaveChangesAsync();
    }

    public async Task DeleteWaiterAsync(Guid restaurantId, Guid waiterId)
    {
        var waiter = await _db.RestaurantWaiters.FirstOrDefaultAsync(item => item.RestaurantId == restaurantId && item.Id == waiterId)
            ?? throw new InvalidOperationException("Garçom não encontrado.");
        var tables = await _db.RestaurantTables.Where(table => table.AssignedWaiterId == waiterId).ToListAsync();
        foreach (var table in tables)
        {
            table.AssignedWaiterId = null;
        }
        _db.RestaurantWaiters.Remove(waiter);
        await _db.SaveChangesAsync();
    }

    public async Task AddTableAsync(Guid restaurantId, string tableNumber, Guid? assignedWaiterId)
    {
        var normalizedNumber = RestaurantText.NormalizeTableNumber(tableNumber);
        var currentNumbers = await _db.RestaurantTables.Where(table => table.RestaurantId == restaurantId).Select(table => table.TableNumber).ToListAsync();
        RestaurantText.EnsureNoDuplicateTables(currentNumbers.Append(normalizedNumber));
        _db.RestaurantTables.Add(new RestaurantTable
        {
            RestaurantId = restaurantId,
            TableNumber = normalizedNumber,
            AssignedWaiterId = assignedWaiterId
        });
        await _db.SaveChangesAsync();
    }

    public async Task UpdateTableAsync(Guid restaurantId, Guid tableId, Guid? assignedWaiterId)
    {
        var table = await _db.RestaurantTables.FirstOrDefaultAsync(item => item.RestaurantId == restaurantId && item.Id == tableId)
            ?? throw new InvalidOperationException("Mesa não encontrada.");
        table.AssignedWaiterId = assignedWaiterId;
        table.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteTableAsync(Guid restaurantId, Guid tableId)
    {
        var table = await _db.RestaurantTables.FirstOrDefaultAsync(item => item.RestaurantId == restaurantId && item.Id == tableId)
            ?? throw new InvalidOperationException("Mesa não encontrada.");
        _db.RestaurantTables.Remove(table);
        await _db.SaveChangesAsync();
    }

    public async Task<PublicMenuView?> GetPublicMenuAsync(Guid restaurantId, string? tableNumber = null)
    {
        var restaurant = await _db.Restaurants.FirstOrDefaultAsync(item => item.Id == restaurantId && item.Status == EntityStatus.ACTIVE);
        if (restaurant is null)
        {
            return null;
        }

        var categories = await _db.MenuCategories
            .Include(category => category.Items.Where(item => item.Status == EntityStatus.ACTIVE))
            .Where(category => category.RestaurantId == restaurantId && category.Status == EntityStatus.ACTIVE)
            .OrderBy(category => category.SortOrder)
            .ToListAsync();
        var tables = await _db.RestaurantTables
            .Where(table => table.RestaurantId == restaurantId)
            .OrderBy(table => table.TableNumber)
            .ToListAsync();
        var brandColor = NormalizeBrandColor(restaurant.PrimaryColor, "#B14623");
        var menuTheme = NormalizeMenuTheme(restaurant.MenuTheme);
        var menuMode = NormalizeMenuMode(restaurant.MenuMode);
        var palette = BuildMenuPalette(brandColor, menuTheme, menuMode);
        var now = DateTimeOffset.Now;
        var mappedCategories = categories.Select(category => MapCategory(category, now)).ToList();
        var promotionalItems = mappedCategories
            .SelectMany(category => category.Items)
            .Where(item => item.IsPromotionActive)
            .OrderBy(item => item.PromotionEndsAt ?? DateTimeOffset.MaxValue)
            .ThenBy(item => item.Name)
            .ToList();
        var normalizedTableNumber = string.IsNullOrWhiteSpace(tableNumber) ? null : tableNumber.Trim();
        var currentTable = normalizedTableNumber is null
            ? null
            : tables.FirstOrDefault(table => string.Equals(table.TableNumber, normalizedTableNumber, StringComparison.OrdinalIgnoreCase));

        return new PublicMenuView
        {
            RestaurantId = restaurant.Id,
            RestaurantName = restaurant.Name,
            PublicDescription = restaurant.PublicDescription,
            CoverImageUrl = restaurant.CoverImageUrl,
            PrimaryColor = palette.PrimaryColor,
            SecondaryColor = palette.TextColor,
            BackgroundColor = palette.BackgroundColor,
            MenuTheme = menuTheme,
            MenuMode = menuMode,
            MutedColor = palette.MutedColor,
            SurfaceColor = palette.SurfaceColor,
            SurfaceStrongColor = palette.SurfaceStrongColor,
            BorderColor = palette.BorderColor,
            HeroOverlayColor = palette.HeroOverlayColor,
            AccentSoftColor = palette.AccentSoftColor,
            ButtonTextColor = palette.ButtonTextColor,
            IsDelivery = normalizedTableNumber is null,
            HasInvalidTable = normalizedTableNumber is not null && currentTable is null,
            RequestedTableNumber = normalizedTableNumber,
            CurrentTableId = currentTable?.Id,
            CurrentTableNumber = currentTable?.TableNumber,
            Tables = tables.Select(table => new RestaurantTableView { Id = table.Id, TableNumber = table.TableNumber }).ToList(),
            PromotionalItems = promotionalItems,
            Categories = mappedCategories
        };
    }

    public async Task<PublicRestaurantTableSession?> GetPublicRestaurantTableSessionAsync(Guid restaurantId, Guid tableId)
    {
        var table = await _db.RestaurantTables.FirstOrDefaultAsync(item => item.RestaurantId == restaurantId && item.Id == tableId);
        if (table is null)
        {
            return null;
        }

        var hasOpenTab = await _db.RestaurantTabs.AnyAsync(tab =>
            tab.RestaurantId == restaurantId &&
            tab.TableId == tableId &&
            tab.Status == RestaurantTabStatus.ABERTA);

        return new PublicRestaurantTableSession
        {
            TableId = table.Id,
            TableNumber = table.TableNumber,
            HasOpenTab = hasOpenTab
        };
    }

    public async Task<object> SubmitPublicOrderAsync(PublicOrderSubmissionInput input)
    {
        var groupedItems = NormalizeOrderItems(input.Items);
        var isTableOrder = input.TableId.HasValue;
        RestaurantTable? table = null;
        RestaurantTab? tab = null;
        DeliveryCustomerInput? deliveryCustomer = null;
        var itemIds = groupedItems.Select(item => item.MenuItemId).ToArray();
        var menuItems = await _db.MenuItems
            .Where(item => item.RestaurantId == input.RestaurantId && itemIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id);

        if (menuItems.Count != groupedItems.Count)
        {
            throw new InvalidOperationException("Um ou mais itens do pedido não existem mais neste cardápio.");
        }

        foreach (var menuItem in menuItems.Values)
        {
            if (menuItem.Status != EntityStatus.ACTIVE)
            {
                throw new InvalidOperationException($"O item \"{menuItem.Name}\" não está mais disponível.");
            }
        }

        if (isTableOrder)
        {
            var tableId = input.TableId.GetValueOrDefault();
            table = await _db.RestaurantTables.FirstOrDefaultAsync(item => item.RestaurantId == input.RestaurantId && item.Id == tableId)
                ?? throw new InvalidOperationException("A mesa informada nao existe neste restaurante.");
            tab = await GetOrCreateOpenTabAsync(input.RestaurantId, table.Id);
        }
        else
        {
            deliveryCustomer = NormalizeDeliveryCustomer(input);
        }

        var subtotalCents = groupedItems.Sum(item => menuItems[item.MenuItemId].PriceCents * item.Quantity);
        var couponPricing = await CalculateCouponPricingAsync(input.RestaurantId, input.CouponCode, subtotalCents);
        var totalCents = couponPricing.TotalCents;
        var order = new Order
        {
            Type = isTableOrder ? OrderType.MESA : OrderType.DELIVERY,
            TabId = tab?.Id,
            RestaurantId = input.RestaurantId,
            TableId = table?.Id,
            AssignedWaiterId = table?.AssignedWaiterId,
            CustomerName = deliveryCustomer?.Name,
            CustomerPhone = deliveryCustomer?.Phone,
            DeliveryAddress = deliveryCustomer?.Address,
            DiscountCouponId = couponPricing.Coupon?.Id,
            SubtotalCents = subtotalCents,
            DiscountCents = couponPricing.DiscountCents,
            CouponCodeSnapshot = couponPricing.Coupon?.Code,
            CouponTypeSnapshot = couponPricing.Coupon?.Type.ToString(),
            CouponValueSnapshot = couponPricing.Coupon?.Value,
            TotalCents = totalCents
        };

        foreach (var item in groupedItems)
        {
            var menuItem = menuItems[item.MenuItemId];
            order.Items.Add(new OrderItem
            {
                MenuItemId = menuItem.Id,
                Quantity = item.Quantity,
                ItemNameSnapshot = menuItem.Name,
                ItemPriceCentsSnapshot = menuItem.PriceCents
            });
        }

        if (tab is not null)
        {
            tab.TotalCents += totalCents;
        }
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        return new
        {
            orderId = order.Id,
            type = order.Type.ToString(),
            tableId = table?.Id,
            tableNumber = table?.TableNumber,
            customerName = order.CustomerName,
            customerPhone = order.CustomerPhone,
            deliveryAddress = order.DeliveryAddress,
            subtotalCents,
            subtotalLabel = RestaurantText.FormatPrice(subtotalCents),
            discountCents = couponPricing.DiscountCents,
            discountLabel = RestaurantText.FormatPrice(couponPricing.DiscountCents),
            couponCode = couponPricing.Coupon?.Code,
            totalCents,
            totalLabel = RestaurantText.FormatPrice(totalCents)
        };
    }

    public async Task<PublicCouponValidationResponse> ValidatePublicCouponAsync(PublicCouponValidationInput input)
    {
        var groupedItems = NormalizeOrderItems(input.Items);
        var subtotalCents = await CalculatePublicOrderSubtotalAsync(input.RestaurantId, groupedItems);
        var couponPricing = await CalculateCouponPricingAsync(input.RestaurantId, input.CouponCode, subtotalCents);
        if (couponPricing.Coupon is null)
        {
            throw new InvalidOperationException("Informe o cupom.");
        }

        return BuildCouponValidationResponse(subtotalCents, couponPricing);
    }

    public async Task<object> SubmitPublicOrderFeedbackAsync(Guid restaurantId, PublicOrderFeedbackInput input)
    {
        if (input.Rating is < 1 or > 5)
        {
            throw new InvalidOperationException("Escolha uma nota de 1 a 5 estrelas.");
        }

        var order = await _db.Orders.FirstOrDefaultAsync(item => item.RestaurantId == restaurantId && item.Id == input.OrderId)
            ?? throw new InvalidOperationException("Pedido nao encontrado para registrar feedback.");

        var hasFeedback = await _db.OrderFeedbacks.AnyAsync(feedback => feedback.OrderId == order.Id);
        if (hasFeedback)
        {
            throw new InvalidOperationException("Este pedido ja recebeu feedback.");
        }

        var comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim();
        if (comment?.Length > 600)
        {
            comment = comment[..600];
        }

        var feedback = new OrderFeedback
        {
            RestaurantId = restaurantId,
            OrderId = order.Id,
            TableId = order.TableId,
            Rating = input.Rating,
            Comment = comment
        };
        _db.OrderFeedbacks.Add(feedback);
        await _db.SaveChangesAsync();

        return new
        {
            feedbackId = feedback.Id,
            rating = feedback.Rating
        };
    }

    public async Task<object> CreatePublicServiceRequestAsync(PublicServiceRequestInput input)
    {
        var table = await _db.RestaurantTables.FirstOrDefaultAsync(item => item.RestaurantId == input.RestaurantId && item.Id == input.TableId)
            ?? throw new InvalidOperationException("A mesa informada não existe neste restaurante.");
        RestaurantTab? tab = null;
        if (input.Type == ServiceRequestType.PEDIR_CONTA)
        {
            tab = await FindOpenTabAsync(input.RestaurantId, table.Id)
                ?? throw new InvalidOperationException("Não há comanda aberta para esta mesa.");

            var hasOrders = await _db.Orders.AnyAsync(order => order.RestaurantId == input.RestaurantId && order.TabId == tab.Id);
            if (!hasOrders)
            {
                throw new InvalidOperationException("Não há itens na comanda desta mesa.");
            }
        }

        var request = new ServiceRequest
        {
            TabId = tab?.Id,
            RestaurantId = input.RestaurantId,
            TableId = table.Id,
            AssignedWaiterId = table.AssignedWaiterId,
            Type = input.Type
        };
        _db.ServiceRequests.Add(request);
        await _db.SaveChangesAsync();

        return new
        {
            requestId = request.Id,
            tabId = tab?.Id,
            tableId = table.Id,
            tableNumber = table.TableNumber,
            type = request.Type.ToString(),
            totalCents = tab?.TotalCents,
            totalLabel = tab is null ? null : RestaurantText.FormatPrice(tab.TotalCents)
        };
    }

    public async Task<WaiterDashboardView> GetWaiterDashboardAsync(Guid restaurantId, Guid? selectedWaiterId)
    {
        var restaurant = await RequireRestaurantAsync(restaurantId);
        var waiters = await _db.RestaurantWaiters.Where(waiter => waiter.RestaurantId == restaurantId).OrderBy(waiter => waiter.Name).ToListAsync();
        var orders = await _db.Orders
            .Include(order => order.Table)
            .Include(order => order.AssignedWaiter)
            .Include(order => order.Items)
            .Where(order => order.RestaurantId == restaurantId && order.Type == OrderType.MESA)
            .ToListAsync();
        var requests = await _db.ServiceRequests
            .Include(request => request.Table)
            .Include(request => request.AssignedWaiter)
            .Where(request => request.RestaurantId == restaurantId)
            .ToListAsync();
        var accountRequests = requests.Where(request => request.Type == ServiceRequestType.PEDIR_CONTA).ToList();
        var accountRequestTableIds = accountRequests
            .Where(request => !request.TabId.HasValue && request.Status != OperationalEventStatus.RESOLVIDO)
            .Select(request => request.TableId)
            .Distinct()
            .ToArray();
        List<RestaurantTab> openTabs = accountRequestTableIds.Length == 0
            ? []
            : await _db.RestaurantTabs
                .Where(tab => tab.RestaurantId == restaurantId &&
                    accountRequestTableIds.Contains(tab.TableId) &&
                    tab.Status == RestaurantTabStatus.ABERTA)
                .ToListAsync();
        var openTabsByTableId = openTabs
            .GroupBy(tab => tab.TableId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(tab => tab.OpenedAt).First());
        var accountTabIds = accountRequests
            .Select(request => ResolveAccountTabId(request, openTabsByTableId))
            .OfType<Guid>()
            .Distinct()
            .ToArray();
        List<Order> accountOrders = accountTabIds.Length == 0
            ? []
            : await _db.Orders
                .Include(order => order.Items)
                .Where(order => order.RestaurantId == restaurantId &&
                    order.TabId.HasValue &&
                    accountTabIds.Contains(order.TabId.Value))
                .ToListAsync();
        var accountOrdersByTabId = accountOrders
            .GroupBy(order => order.TabId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(order => order.CreatedAt).ToList());
        var now = DateTimeOffset.UtcNow;

        var queue = new List<WaiterQueueEventView>();
        queue.AddRange(orders.Select(order => MapOrderQueueEvent(order, selectedWaiterId, restaurant, now)));
        foreach (var request in requests)
        {
            var accountTabId = ResolveAccountTabId(request, openTabsByTableId);
            var requestOrders = accountTabId.HasValue && accountOrdersByTabId.TryGetValue(accountTabId.Value, out var foundOrders)
                ? foundOrders
                : new List<Order>();
            IReadOnlyList<WaiterQueueOrderItemView> accountItems = request.Type == ServiceRequestType.PEDIR_CONTA
                ? MapAccountItems(requestOrders)
                : [];
            var accountSubtotalCents = request.Type == ServiceRequestType.PEDIR_CONTA
                ? requestOrders.Sum(order => order.SubtotalCents > 0 || order.DiscountCents > 0 ? order.SubtotalCents : order.TotalCents)
                : 0;
            var accountDiscountCents = request.Type == ServiceRequestType.PEDIR_CONTA
                ? requestOrders.Sum(order => order.DiscountCents)
                : 0;
            var accountTotalCents = request.Type == ServiceRequestType.PEDIR_CONTA
                ? requestOrders.Sum(order => order.TotalCents)
                : 0;
            var accountItemCount = accountItems.Sum(item => item.Quantity);
            var accountTotalLabel = RestaurantText.FormatPrice(accountTotalCents);
            var accountCouponCodes = requestOrders
                .Select(order => order.CouponCodeSnapshot)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            queue.Add(new WaiterQueueEventView
            {
                Id = request.Id,
                EventKind = "SERVICE_REQUEST",
                Status = request.Status,
                CreatedAt = request.CreatedAt,
                TableNumber = request.Table?.TableNumber ?? "",
                AssignedWaiterId = request.AssignedWaiterId,
                AssignedWaiterName = request.AssignedWaiter?.Name,
                OwnershipLabel = QueueRules.BuildOwnershipLabel(selectedWaiterId, request.AssignedWaiterId, request.AssignedWaiter?.Name),
                Title = request.Type == ServiceRequestType.PEDIR_CONTA ? $"Conta - mesa {request.Table?.TableNumber}" : $"Chamado - mesa {request.Table?.TableNumber}",
                Summary = request.Type == ServiceRequestType.PEDIR_CONTA
                    ? accountItems.Count > 0
                        ? $"Cliente pediu a conta: {accountItemCount} item(ns), total {accountTotalLabel}."
                        : "Cliente pediu a conta. Nenhum item localizado na comanda."
                    : "Cliente chamou o garçom.",
                SubtotalCents = request.Type == ServiceRequestType.PEDIR_CONTA ? accountSubtotalCents : null,
                SubtotalLabel = request.Type == ServiceRequestType.PEDIR_CONTA ? RestaurantText.FormatPrice(accountSubtotalCents) : null,
                DiscountCents = accountDiscountCents,
                DiscountLabel = request.Type == ServiceRequestType.PEDIR_CONTA ? RestaurantText.FormatPrice(accountDiscountCents) : null,
                CouponSummary = accountCouponCodes.Count == 0 ? null : $"Cupons {string.Join(", ", accountCouponCodes)}: -{RestaurantText.FormatPrice(accountDiscountCents)}",
                TotalCents = request.Type == ServiceRequestType.PEDIR_CONTA ? accountTotalCents : null,
                TotalLabel = request.Type == ServiceRequestType.PEDIR_CONTA ? accountTotalLabel : null,
                Items = accountItems,
                RequestType = request.Type
            });
        }

        return new WaiterDashboardView
        {
            RestaurantId = restaurant.Id,
            RestaurantName = restaurant.Name,
            SelectedWaiterId = selectedWaiterId,
            Waiters = waiters.Select(waiter => new RestaurantWaiterView { Id = waiter.Id, Name = waiter.Name }).ToList(),
            Queue = queue
                .OrderBy(item => QueueRules.GetOperationalStatusRank(item.Status))
                .ThenBy(item => item.CreatedAt)
                .ToList()
        };
    }

    public async Task UpdateOperationalEventStatusAsync(Guid restaurantId, Guid selectedWaiterId, UpdateOperationalEventStatusInput input)
    {
        var now = DateTimeOffset.UtcNow;

        if (input.EventKind == "ORDER")
        {
            var order = await _db.Orders.FirstOrDefaultAsync(item => item.RestaurantId == restaurantId && item.Id == input.EventId && item.Type == OrderType.MESA)
                ?? throw new InvalidOperationException("Pedido não encontrado.");
            order.Status = input.NextStatus;
            order.HandledByWaiterId = selectedWaiterId;
            if (input.NextStatus == OperationalEventStatus.EM_ATENDIMENTO)
            {
                order.AcknowledgedAt ??= now;
                order.ResolvedAt = null;
            }
            if (input.NextStatus == OperationalEventStatus.RESOLVIDO)
            {
                order.AcknowledgedAt ??= now;
                order.ResolvedAt = now;
            }
            order.UpdatedAt = now;
            await _db.SaveChangesAsync();
            return;
        }

        var request = await _db.ServiceRequests.FirstOrDefaultAsync(item => item.RestaurantId == restaurantId && item.Id == input.EventId)
            ?? throw new InvalidOperationException("Solicitação não encontrada.");
        request.Status = input.NextStatus;
        request.HandledByWaiterId = selectedWaiterId;
        if (input.NextStatus == OperationalEventStatus.EM_ATENDIMENTO)
        {
            request.AcknowledgedAt ??= now;
            request.ResolvedAt = null;
        }
        if (input.NextStatus == OperationalEventStatus.RESOLVIDO)
        {
            request.AcknowledgedAt ??= now;
            request.ResolvedAt = now;
            if (request.Type == ServiceRequestType.PEDIR_CONTA)
            {
                await CloseAccountTabAsync(restaurantId, request, selectedWaiterId, now);
            }
        }
        request.UpdatedAt = now;
        await _db.SaveChangesAsync();
    }

    public async Task<RestaurantDeliveryDashboardView> GetDeliveryDashboardAsync(Guid restaurantId)
    {
        var restaurant = await RequireRestaurantAsync(restaurantId);
        return new RestaurantDeliveryDashboardView
        {
            RestaurantId = restaurant.Id,
            RestaurantName = restaurant.Name
        };
    }

    public async Task<IReadOnlyList<DeliveryOrderView>> GetDeliveryOrdersAsync(Guid restaurantId)
    {
        var orders = await _db.Orders
            .Include(order => order.Items)
            .Where(order => order.RestaurantId == restaurantId && order.Type == OrderType.DELIVERY)
            .ToListAsync();

        return orders
            .OrderBy(order => QueueRules.GetOperationalStatusRank(order.Status))
            .ThenBy(order => order.CreatedAt)
            .Select(MapDeliveryOrder)
            .ToList();
    }

    public async Task UpdateDeliveryOrderStatusAsync(Guid restaurantId, DeliveryOrderStatusInput input)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(item =>
                item.RestaurantId == restaurantId &&
                item.Id == input.OrderId &&
                item.Type == OrderType.DELIVERY)
            ?? throw new InvalidOperationException("Pedido delivery nao encontrado.");
        var now = DateTimeOffset.UtcNow;

        order.Status = input.NextStatus;
        if (input.NextStatus == OperationalEventStatus.EM_ATENDIMENTO)
        {
            order.AcknowledgedAt ??= now;
            order.ResolvedAt = null;
        }
        if (input.NextStatus == OperationalEventStatus.RESOLVIDO)
        {
            order.AcknowledgedAt ??= now;
            order.ResolvedAt = now;
        }
        order.UpdatedAt = now;
        await _db.SaveChangesAsync();
    }

    private async Task<Restaurant> RequireRestaurantAsync(Guid restaurantId)
    {
        return await _db.Restaurants.FirstOrDefaultAsync(restaurant => restaurant.Id == restaurantId)
            ?? throw new InvalidOperationException("Restaurante não encontrado.");
    }

    private static RestaurantFeedbackDashboardView BuildFeedbackDashboard(IReadOnlyList<OrderFeedback> feedbacks)
    {
        var total = feedbacks.Count;
        decimal? average = total == 0
            ? null
            : decimal.Round(feedbacks.Average(feedback => (decimal)feedback.Rating), 1, MidpointRounding.AwayFromZero);

        return new RestaurantFeedbackDashboardView
        {
            TotalFeedbacks = total,
            AverageRating = average,
            AverageRatingLabel = average.HasValue ? $"{average:0.0} / 5" : "Sem notas",
            Ratings = Enumerable.Range(1, 5)
                .Reverse()
                .Select(rating =>
                {
                    var count = feedbacks.Count(feedback => feedback.Rating == rating);
                    return new RestaurantFeedbackRatingView
                    {
                        Rating = rating,
                        Count = count,
                        Percentage = total == 0 ? 0 : (int)Math.Round(count * 100m / total, MidpointRounding.AwayFromZero),
                        Label = $"{rating} estrela{(rating == 1 ? "" : "s")}"
                    };
                })
                .ToList(),
            Comments = feedbacks
                .Select(feedback => new RestaurantFeedbackCommentView
                {
                    Id = feedback.Id,
                    OrderId = feedback.OrderId,
                    Rating = feedback.Rating,
                    RatingLabel = $"{feedback.Rating} estrela{(feedback.Rating == 1 ? "" : "s")}",
                    Comment = string.IsNullOrWhiteSpace(feedback.Comment) ? "Cliente nao deixou comentario." : feedback.Comment,
                    TableNumber = feedback.Table?.TableNumber ?? "",
                    CreatedAtLabel = feedback.CreatedAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm"),
                    TotalLabel = feedback.Order is null ? "" : RestaurantText.FormatPrice(feedback.Order.TotalCents)
                })
                .ToList()
        };
    }

    private async Task<MenuCategory> RequireCategoryAsync(Guid restaurantId, Guid categoryId)
    {
        return await _db.MenuCategories.FirstOrDefaultAsync(category => category.RestaurantId == restaurantId && category.Id == categoryId)
            ?? throw new InvalidOperationException("Categoria não encontrada.");
    }

    private async Task<DiscountCoupon> RequireDiscountCouponAsync(Guid restaurantId, Guid couponId)
    {
        return await _db.DiscountCoupons.FirstOrDefaultAsync(coupon => coupon.RestaurantId == restaurantId && coupon.Id == couponId)
            ?? throw new InvalidOperationException("Cupom nao encontrado.");
    }

    private async Task<MenuCategory> GetOrCreateImportedWhatsAppCategoryAsync(Guid restaurantId)
    {
        const string categoryName = "Importados WhatsApp";
        var category = await _db.MenuCategories.FirstOrDefaultAsync(item =>
            item.RestaurantId == restaurantId &&
            item.Name == categoryName);
        if (category is not null)
        {
            return category;
        }

        var sortOrder = await _db.MenuCategories.Where(item => item.RestaurantId == restaurantId).CountAsync() + 1;
        category = new MenuCategory
        {
            RestaurantId = restaurantId,
            Name = categoryName,
            SortOrder = sortOrder
        };
        _db.MenuCategories.Add(category);
        return category;
    }

    private static MenuItemWhatsAppSyncContext BuildWhatsAppSyncContext(Restaurant restaurant, MenuItem item)
    {
        return new MenuItemWhatsAppSyncContext(
            restaurant.Id,
            item.Id,
            restaurant.WhatsAppPhone,
            item.WhatsAppProductId,
            item.Name,
            item.Description,
            item.PriceCents / 100m,
            item.Status == EntityStatus.ACTIVE);
    }

    private static int ToPriceCents(decimal value)
    {
        return decimal.ToInt32(decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero));
    }

    private async Task<int> CalculatePublicOrderSubtotalAsync(Guid restaurantId, IReadOnlyList<PublicOrderItemInput> groupedItems)
    {
        var itemIds = groupedItems.Select(item => item.MenuItemId).ToArray();
        var menuItems = await _db.MenuItems
            .Where(item => item.RestaurantId == restaurantId && itemIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id);

        if (menuItems.Count != groupedItems.Count)
        {
            throw new InvalidOperationException("Um ou mais itens do pedido nao existem mais neste cardapio.");
        }

        foreach (var menuItem in menuItems.Values)
        {
            if (menuItem.Status != EntityStatus.ACTIVE)
            {
                throw new InvalidOperationException($"O item \"{menuItem.Name}\" nao esta mais disponivel.");
            }
        }

        return groupedItems.Sum(item => menuItems[item.MenuItemId].PriceCents * item.Quantity);
    }

    private async Task<CouponPricing> CalculateCouponPricingAsync(Guid restaurantId, string? couponCode, int subtotalCents)
    {
        var normalizedCode = NormalizeCouponCodeOrNull(couponCode);
        if (normalizedCode is null)
        {
            return new CouponPricing(null, 0, subtotalCents);
        }

        var coupon = await _db.DiscountCoupons.FirstOrDefaultAsync(item =>
            item.RestaurantId == restaurantId &&
            item.Code == normalizedCode);
        if (coupon is null)
        {
            throw new InvalidOperationException("Cupom nao encontrado.");
        }

        if (coupon.Status != EntityStatus.ACTIVE)
        {
            throw new InvalidOperationException("Cupom inativo.");
        }

        var discountCents = CalculateDiscountCents(coupon, subtotalCents);
        return new CouponPricing(coupon, discountCents, Math.Max(0, subtotalCents - discountCents));
    }

    private static int CalculateDiscountCents(DiscountCoupon coupon, int subtotalCents)
    {
        if (subtotalCents <= 0)
        {
            return 0;
        }

        var discountCents = coupon.Type == DiscountCouponType.PERCENTUAL
            ? (int)decimal.Round(subtotalCents * coupon.Value / 100m, 0, MidpointRounding.AwayFromZero)
            : ToPriceCents(coupon.Value);

        return Math.Clamp(discountCents, 0, subtotalCents);
    }

    private static IReadOnlyList<PublicOrderItemInput> NormalizeOrderItems(IReadOnlyList<PublicOrderItemInput> items)
    {
        var groupedItems = items
            .Where(item => item.Quantity > 0)
            .GroupBy(item => item.MenuItemId)
            .Select(group => new PublicOrderItemInput { MenuItemId = group.Key, Quantity = group.Sum(item => item.Quantity) })
            .ToList();

        if (groupedItems.Count == 0)
        {
            throw new InvalidOperationException("Adicione ao menos um item.");
        }

        return groupedItems;
    }

    private static DeliveryCustomerInput NormalizeDeliveryCustomer(PublicOrderSubmissionInput input)
    {
        return new DeliveryCustomerInput(
            NormalizeRequiredText(input.CustomerName, "Informe seu nome.", 120),
            NormalizeRequiredText(input.CustomerPhone, "Informe seu telefone.", 32),
            NormalizeRequiredText(input.DeliveryAddress, "Informe o endereco de entrega.", 500));
    }

    private static string NormalizeRequiredText(string? value, string errorMessage, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        var normalized = value.Trim();
        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }

    private static PublicCouponValidationResponse BuildCouponValidationResponse(int subtotalCents, CouponPricing pricing)
    {
        var coupon = pricing.Coupon ?? throw new InvalidOperationException("Cupom nao encontrado.");
        return new PublicCouponValidationResponse(
            coupon.Code,
            coupon.Type.ToString(),
            coupon.Value,
            FormatCouponValue(coupon),
            subtotalCents,
            RestaurantText.FormatPrice(subtotalCents),
            pricing.DiscountCents,
            RestaurantText.FormatPrice(pricing.DiscountCents),
            pricing.TotalCents,
            RestaurantText.FormatPrice(pricing.TotalCents));
    }

    private static DiscountCouponInputNormalized NormalizeDiscountCouponInput(DiscountCouponInput input)
    {
        var code = NormalizeCouponCode(input.Code);
        var value = input.Type == DiscountCouponType.VALOR_FIXO
            ? ToPriceCentsDecimal(input.ValueInput)
            : ParseCouponPercentage(input.ValueInput);

        if (value <= 0)
        {
            throw new InvalidOperationException("Informe um desconto maior que zero.");
        }

        if (input.Type == DiscountCouponType.PERCENTUAL && value > 100)
        {
            throw new InvalidOperationException("O desconto percentual nao pode passar de 100%.");
        }

        return new DiscountCouponInputNormalized(code, input.Type, decimal.Round(value, 2, MidpointRounding.AwayFromZero));
    }

    private static string NormalizeCouponCode(string value)
    {
        var code = value.Trim().ToUpperInvariant();
        if (code.Length is < 3 or > 40 || code.Any(char.IsWhiteSpace))
        {
            throw new InvalidOperationException("Informe um codigo de cupom entre 3 e 40 caracteres, sem espacos.");
        }

        return code;
    }

    private static string? NormalizeCouponCodeOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : NormalizeCouponCode(value);
    }

    private static decimal ParseCouponPercentage(string value)
    {
        var normalized = value.Trim().Replace("%", "", StringComparison.OrdinalIgnoreCase);
        if (!decimal.TryParse(normalized, NumberStyles.Number, new CultureInfo("pt-BR"), out var amount) &&
            !decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            throw new InvalidOperationException("Informe um percentual valido.");
        }

        return amount;
    }

    private static decimal ToPriceCentsDecimal(string value)
    {
        return RestaurantText.ParsePriceToCents(value) / 100m;
    }

    private static string FormatCouponValue(DiscountCoupon coupon)
    {
        return coupon.Type == DiscountCouponType.PERCENTUAL
            ? $"{coupon.Value:0.##}%"
            : RestaurantText.FormatPrice(ToPriceCents(coupon.Value));
    }

    private static string NormalizeMenuItemName(string value)
    {
        return RestaurantText.Slugify(value);
    }

    private async Task<RestaurantTab> GetOrCreateOpenTabAsync(Guid restaurantId, Guid tableId)
    {
        var tab = await FindOpenTabAsync(restaurantId, tableId);

        if (tab is not null)
        {
            return tab;
        }

        tab = new RestaurantTab
        {
            RestaurantId = restaurantId,
            TableId = tableId
        };
        _db.RestaurantTabs.Add(tab);
        await _db.SaveChangesAsync();
        return tab;
    }

    private async Task<RestaurantTab?> FindOpenTabAsync(Guid restaurantId, Guid tableId)
    {
        return await _db.RestaurantTabs.FirstOrDefaultAsync(item =>
            item.RestaurantId == restaurantId &&
            item.TableId == tableId &&
            item.Status == RestaurantTabStatus.ABERTA);
    }

    private async Task CloseAccountTabAsync(Guid restaurantId, ServiceRequest request, Guid selectedWaiterId, DateTimeOffset now)
    {
        var tab = request.TabId.HasValue
            ? await _db.RestaurantTabs.FirstOrDefaultAsync(item => item.RestaurantId == restaurantId && item.Id == request.TabId.Value)
            : await FindOpenTabAsync(restaurantId, request.TableId);

        if (tab is null)
        {
            return;
        }

        request.TabId ??= tab.Id;
        if (tab.Status != RestaurantTabStatus.ABERTA)
        {
            return;
        }

        tab.Status = RestaurantTabStatus.FECHADA;
        tab.ClosedAt = now;
        tab.UpdatedAt = now;

        var orders = await _db.Orders
            .Where(order => order.RestaurantId == restaurantId && order.TabId == tab.Id)
            .ToListAsync();
        foreach (var order in orders)
        {
            order.Status = OperationalEventStatus.RESOLVIDO;
            order.HandledByWaiterId = selectedWaiterId;
            order.AcknowledgedAt ??= now;
            order.ResolvedAt = now;
            order.UpdatedAt = now;
        }
    }

    private static Guid? ResolveAccountTabId(ServiceRequest request, IReadOnlyDictionary<Guid, RestaurantTab> openTabsByTableId)
    {
        if (request.Type != ServiceRequestType.PEDIR_CONTA)
        {
            return null;
        }

        if (request.TabId.HasValue)
        {
            return request.TabId;
        }

        if (request.Status != OperationalEventStatus.RESOLVIDO &&
            openTabsByTableId.TryGetValue(request.TableId, out var openTab))
        {
            return openTab.Id;
        }

        return null;
    }

    private static WaiterQueueEventView MapOrderQueueEvent(Order order, Guid? selectedWaiterId, Restaurant restaurant, DateTimeOffset now)
    {
        var sla = BuildOrderSlaSnapshot(order, restaurant.PendingSlaMinutes, restaurant.AttendanceSlaMinutes, now);
        return new WaiterQueueEventView
        {
            Id = order.Id,
            EventKind = "ORDER",
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            AcknowledgedAt = order.AcknowledgedAt,
            ResolvedAt = order.ResolvedAt,
            TableNumber = order.Table?.TableNumber ?? "",
            AssignedWaiterId = order.AssignedWaiterId,
            AssignedWaiterName = order.AssignedWaiter?.Name,
            OwnershipLabel = QueueRules.BuildOwnershipLabel(selectedWaiterId, order.AssignedWaiterId, order.AssignedWaiter?.Name),
            Title = $"Novo pedido - mesa {order.Table?.TableNumber}",
            Summary = order.DiscountCents > 0 && !string.IsNullOrWhiteSpace(order.CouponCodeSnapshot)
                ? $"{order.Items.Count} item(ns) - total {RestaurantText.FormatPrice(order.TotalCents)} com cupom {order.CouponCodeSnapshot}"
                : $"{order.Items.Count} item(ns) - total {RestaurantText.FormatPrice(order.TotalCents)}",
            SubtotalCents = order.SubtotalCents > 0 || order.DiscountCents > 0 ? order.SubtotalCents : order.TotalCents,
            SubtotalLabel = RestaurantText.FormatPrice(order.SubtotalCents > 0 || order.DiscountCents > 0 ? order.SubtotalCents : order.TotalCents),
            DiscountCents = order.DiscountCents,
            DiscountLabel = RestaurantText.FormatPrice(order.DiscountCents),
            CouponCode = order.CouponCodeSnapshot,
            CouponSummary = order.DiscountCents > 0 && !string.IsNullOrWhiteSpace(order.CouponCodeSnapshot)
                ? $"Cupom {order.CouponCodeSnapshot}: -{RestaurantText.FormatPrice(order.DiscountCents)}"
                : null,
            TotalCents = order.TotalCents,
            TotalLabel = RestaurantText.FormatPrice(order.TotalCents),
            Items = order.Items.Select(item => new WaiterQueueOrderItemView
            {
                Name = item.ItemNameSnapshot,
                Quantity = item.Quantity,
                UnitPriceLabel = RestaurantText.FormatPrice(item.ItemPriceCentsSnapshot),
                LineTotalLabel = RestaurantText.FormatPrice(item.ItemPriceCentsSnapshot * item.Quantity)
            }).ToList(),
            PendingSlaMinutes = restaurant.PendingSlaMinutes,
            AttendanceSlaMinutes = restaurant.AttendanceSlaMinutes,
            CurrentSlaMinutes = sla.CurrentSlaMinutes,
            CurrentStageLabel = sla.StageLabel,
            CurrentStageStartedAt = sla.StageStartedAt,
            CurrentStageEndedAt = sla.StageEndedAt,
            CurrentStageElapsedSeconds = sla.ElapsedSeconds,
            IsOverSla = sla.IsOverSla,
            SlaLabel = sla.SlaLabel
        };
    }

    private static DeliveryOrderView MapDeliveryOrder(Order order)
    {
        return new DeliveryOrderView
        {
            Id = order.Id,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            AcknowledgedAt = order.AcknowledgedAt,
            ResolvedAt = order.ResolvedAt,
            CustomerName = order.CustomerName ?? "",
            CustomerPhone = order.CustomerPhone ?? "",
            DeliveryAddress = order.DeliveryAddress ?? "",
            Summary = $"{order.Items.Count} item(ns) - total {RestaurantText.FormatPrice(order.TotalCents)}",
            SubtotalCents = order.SubtotalCents > 0 || order.DiscountCents > 0 ? order.SubtotalCents : order.TotalCents,
            SubtotalLabel = RestaurantText.FormatPrice(order.SubtotalCents > 0 || order.DiscountCents > 0 ? order.SubtotalCents : order.TotalCents),
            DiscountCents = order.DiscountCents,
            DiscountLabel = RestaurantText.FormatPrice(order.DiscountCents),
            CouponCode = order.CouponCodeSnapshot,
            CouponSummary = order.DiscountCents > 0 && !string.IsNullOrWhiteSpace(order.CouponCodeSnapshot)
                ? $"Cupom {order.CouponCodeSnapshot}: -{RestaurantText.FormatPrice(order.DiscountCents)}"
                : null,
            TotalCents = order.TotalCents,
            TotalLabel = RestaurantText.FormatPrice(order.TotalCents),
            Items = order.Items.Select(item => new WaiterQueueOrderItemView
            {
                Name = item.ItemNameSnapshot,
                Quantity = item.Quantity,
                UnitPriceLabel = RestaurantText.FormatPrice(item.ItemPriceCentsSnapshot),
                LineTotalLabel = RestaurantText.FormatPrice(item.ItemPriceCentsSnapshot * item.Quantity)
            }).ToList()
        };
    }

    private static RestaurantOperationalBottlenecksView BuildOperationalBottlenecks(
        IEnumerable<Order> orders,
        int? pendingSlaMinutes,
        int? attendanceSlaMinutes,
        DateTimeOffset now)
    {
        var openOrders = orders.Where(order => order.Status != OperationalEventStatus.RESOLVIDO).ToList();
        var delayedSnapshots = openOrders
            .Select(order => new
            {
                Order = order,
                Sla = BuildOrderSlaSnapshot(order, pendingSlaMinutes, attendanceSlaMinutes, now)
            })
            .Where(item => item.Sla.IsOverSla)
            .OrderByDescending(item => item.Sla.DelaySeconds)
            .ToList();
        var delayedOrders = delayedSnapshots
            .Take(5)
            .Select(item => new RestaurantOperationalBottleneckOrderView
            {
                Id = item.Order.Id,
                Title = $"Pedido - mesa {item.Order.Table?.TableNumber}",
                StatusLabel = StatusLabel(item.Order.Status),
                StageLabel = item.Sla.StageLabel,
                ElapsedLabel = FormatDuration(item.Sla.ElapsedSeconds),
                SlaLabel = item.Sla.SlaLabel,
                DelayLabel = $"Atrasado {FormatDuration(item.Sla.DelaySeconds)}",
                IsOverSla = item.Sla.IsOverSla
            })
            .ToList();

        return new RestaurantOperationalBottlenecksView
        {
            OpenOrderCount = openOrders.Count,
            PendingOrderCount = openOrders.Count(order => order.Status == OperationalEventStatus.PENDENTE),
            AttendanceOrderCount = openOrders.Count(order => order.Status == OperationalEventStatus.EM_ATENDIMENTO),
            OverSlaOrderCount = delayedSnapshots.Count,
            DelayedOrders = delayedOrders
        };
    }

    private sealed record OrderSlaSnapshot(
        string StageLabel,
        DateTimeOffset StageStartedAt,
        DateTimeOffset? StageEndedAt,
        int ElapsedSeconds,
        int? CurrentSlaMinutes,
        bool IsOverSla,
        int DelaySeconds,
        string SlaLabel);

    private static OrderSlaSnapshot BuildOrderSlaSnapshot(Order order, int? pendingSlaMinutes, int? attendanceSlaMinutes, DateTimeOffset now)
    {
        var stageLabel = order.Status switch
        {
            OperationalEventStatus.EM_ATENDIMENTO => "Atendendo",
            OperationalEventStatus.RESOLVIDO => "Resolvido",
            _ => "Pendente"
        };
        var startedAt = order.Status switch
        {
            OperationalEventStatus.EM_ATENDIMENTO => order.AcknowledgedAt ?? order.CreatedAt,
            OperationalEventStatus.RESOLVIDO => order.AcknowledgedAt ?? order.CreatedAt,
            _ => order.CreatedAt
        };
        DateTimeOffset? endedAt = order.Status switch
        {
            OperationalEventStatus.PENDENTE => order.AcknowledgedAt,
            OperationalEventStatus.EM_ATENDIMENTO => order.ResolvedAt,
            OperationalEventStatus.RESOLVIDO => order.ResolvedAt,
            _ => null
        };
        var currentSlaMinutes = order.Status switch
        {
            OperationalEventStatus.PENDENTE => pendingSlaMinutes,
            OperationalEventStatus.EM_ATENDIMENTO => attendanceSlaMinutes,
            _ => null
        };
        var elapsedSeconds = SecondsBetween(startedAt, endedAt ?? now);
        var delaySeconds = currentSlaMinutes.HasValue
            ? Math.Max(0, elapsedSeconds - currentSlaMinutes.Value * 60)
            : 0;
        var isOverSla = order.Status != OperationalEventStatus.RESOLVIDO && delaySeconds > 0;

        return new OrderSlaSnapshot(
            stageLabel,
            startedAt,
            endedAt,
            elapsedSeconds,
            currentSlaMinutes,
            isOverSla,
            delaySeconds,
            currentSlaMinutes.HasValue ? $"{currentSlaMinutes.Value} min" : "Sem SLA");
    }

    private static int SecondsBetween(DateTimeOffset start, DateTimeOffset end)
    {
        return Math.Max(0, (int)Math.Floor((end - start).TotalSeconds));
    }

    private static string FormatDuration(int seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes:00}min";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{duration.Minutes}min {duration.Seconds:00}s";
        }

        return $"{duration.Seconds}s";
    }

    private static string StatusLabel(OperationalEventStatus status)
    {
        return status switch
        {
            OperationalEventStatus.EM_ATENDIMENTO => "Atendendo",
            OperationalEventStatus.RESOLVIDO => "Resolvido",
            _ => "Pendente"
        };
    }

    private static IReadOnlyList<WaiterQueueOrderItemView> MapAccountItems(IEnumerable<Order> orders)
    {
        return orders
            .SelectMany(order => order.Items)
            .GroupBy(item => new { item.ItemNameSnapshot, item.ItemPriceCentsSnapshot })
            .Select(group =>
            {
                var quantity = group.Sum(item => item.Quantity);
                return new WaiterQueueOrderItemView
                {
                    Name = group.Key.ItemNameSnapshot,
                    Quantity = quantity,
                    UnitPriceLabel = RestaurantText.FormatPrice(group.Key.ItemPriceCentsSnapshot),
                    LineTotalLabel = RestaurantText.FormatPrice(group.Key.ItemPriceCentsSnapshot * quantity)
                };
            })
            .OrderBy(item => item.Name)
            .ToList();
    }

    private static void ApplyPromotion(MenuItem item, MenuItemPromotionInput promotion)
    {
        if (!promotion.IsPromotion)
        {
            item.IsPromotion = false;
            item.PromotionStartsAt = null;
            item.PromotionEndsAt = null;
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var duration = (promotion.PromotionDuration ?? "TODAY").Trim().ToUpperInvariant();
        DateOnly startDate;
        DateOnly endExclusiveDate;

        if (duration == "WEEKEND")
        {
            startDate = GetCurrentOrNextSaturday(today);
            endExclusiveDate = startDate.AddDays(2);
        }
        else if (duration == "CUSTOM")
        {
            if (!promotion.PromotionStartsOn.HasValue || !promotion.PromotionEndsOn.HasValue)
            {
                throw new InvalidOperationException("Informe o periodo customizado da promocao.");
            }

            startDate = promotion.PromotionStartsOn.Value;
            var endDate = promotion.PromotionEndsOn.Value;
            if (endDate < startDate)
            {
                throw new InvalidOperationException("A data final da promocao deve ser igual ou posterior a data inicial.");
            }

            endExclusiveDate = endDate.AddDays(1);
        }
        else
        {
            startDate = today;
            endExclusiveDate = today.AddDays(1);
        }

        item.IsPromotion = true;
        item.PromotionStartsAt = ToLocalStart(startDate);
        item.PromotionEndsAt = ToLocalStart(endExclusiveDate);
    }

    private static DateOnly GetCurrentOrNextSaturday(DateOnly today)
    {
        if (today.DayOfWeek == DayOfWeek.Sunday)
        {
            return today.AddDays(-1);
        }

        var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(daysUntilSaturday);
    }

    private static DateTimeOffset ToLocalStart(DateOnly date)
    {
        var localDate = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(localDate, TimeZoneInfo.Local.GetUtcOffset(localDate));
    }

    private static bool IsPromotionActive(MenuItem item, DateTimeOffset now)
    {
        return item.IsPromotion &&
            item.Status == EntityStatus.ACTIVE &&
            (!item.PromotionStartsAt.HasValue || item.PromotionStartsAt.Value <= now) &&
            (!item.PromotionEndsAt.HasValue || item.PromotionEndsAt.Value > now);
    }

    private static string? BuildPromotionPeriodLabel(MenuItem item)
    {
        if (!item.IsPromotion)
        {
            return null;
        }

        if (!item.PromotionStartsAt.HasValue || !item.PromotionEndsAt.HasValue)
        {
            return "Promocional";
        }

        var start = DateOnly.FromDateTime(item.PromotionStartsAt.Value.LocalDateTime);
        var endExclusive = DateOnly.FromDateTime(item.PromotionEndsAt.Value.LocalDateTime);
        var endInclusive = endExclusive.AddDays(-1);
        var today = DateOnly.FromDateTime(DateTime.Now);

        if (start == today && endExclusive == today.AddDays(1))
        {
            return "Somente hoje";
        }

        if (start.DayOfWeek == DayOfWeek.Saturday && endExclusive == start.AddDays(2))
        {
            return "Fim de semana";
        }

        return start == endInclusive
            ? start.ToString("dd/MM")
            : $"{start:dd/MM} a {endInclusive:dd/MM}";
    }

    private static DiscountCouponView MapDiscountCoupon(DiscountCoupon coupon, CouponUsageStats? usage)
    {
        return new DiscountCouponView
        {
            Id = coupon.Id,
            Code = coupon.Code,
            Type = coupon.Type,
            TypeLabel = coupon.Type == DiscountCouponType.PERCENTUAL ? "Percentual" : "Valor fixo",
            Value = coupon.Value,
            ValueInput = coupon.Type == DiscountCouponType.PERCENTUAL
                ? coupon.Value.ToString("0.##", CultureInfo.InvariantCulture)
                : coupon.Value.ToString("0.##", CultureInfo.InvariantCulture),
            ValueLabel = FormatCouponValue(coupon),
            Status = coupon.Status,
            UsageCount = usage?.UsageCount ?? 0,
            TotalDiscountLabel = RestaurantText.FormatPrice(usage?.TotalDiscountCents ?? 0),
            LastUsedAtLabel = usage is null ? "Nunca usado" : usage.LastUsedAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm")
        };
    }

    private static MenuCategoryView MapCategory(MenuCategory category, DateTimeOffset now)
    {
        return new MenuCategoryView
        {
            Id = category.Id,
            Name = category.Name,
            Status = category.Status,
            Items = category.Items
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(item => new MenuItemView
                {
                    Id = item.Id,
                    CategoryId = item.CategoryId,
                    Name = item.Name,
                    Description = item.Description,
                    PriceCents = item.PriceCents,
                    PriceLabel = RestaurantText.FormatPrice(item.PriceCents),
                    ImageUrl = item.ImageUrl,
                    WhatsAppProductId = item.WhatsAppProductId,
                    Status = item.Status,
                    IsPromotion = item.IsPromotion,
                    IsPromotionActive = IsPromotionActive(item, now),
                    PromotionStartsAt = item.PromotionStartsAt,
                    PromotionEndsAt = item.PromotionEndsAt,
                    PromotionPeriodLabel = BuildPromotionPeriodLabel(item)
                })
                .ToList()
        };
    }

    private static string NormalizeMenuTheme(string? value)
    {
        var normalized = (value ?? "").Trim().ToUpperInvariant();
        return normalized is "ELEGANTE" or "ACAI" or "CLASSICO" or "MINIMALISTA" or "PREMIUM"
            ? normalized
            : "ELEGANTE";
    }

    private static string NormalizeMenuMode(string? value)
    {
        var normalized = (value ?? "").Trim().ToUpperInvariant();
        return normalized == "ESCURO" ? "ESCURO" : "CLARO";
    }

    private static MenuPalette BuildMenuPalette(string brandColor, string menuTheme, string menuMode)
    {
        var template = GetThemeTemplate(menuTheme);
        var isDark = menuMode == "ESCURO";
        var backgroundColor = isDark
            ? MixHex(brandColor, template.DarkBase, template.DarkBrandWeight)
            : MixHex(brandColor, template.LightBase, template.LightBrandWeight);
        var textColor = isDark ? "#FFF8F0" : template.TextLight;
        var surfaceColor = isDark
            ? MixHex("#FFFFFF", backgroundColor, 0.08)
            : MixHex(brandColor, "#FFFFFF", 0.025);
        var surfaceStrongColor = isDark
            ? MixHex("#FFFFFF", backgroundColor, 0.13)
            : MixHex(brandColor, "#FFFFFF", 0.06);
        var borderColor = isDark
            ? MixHex(brandColor, surfaceColor, 0.42)
            : MixHex(brandColor, backgroundColor, 0.24);
        var mutedColor = MixHex(textColor, backgroundColor, isDark ? 0.72 : 0.68);
        var heroOverlayColor = isDark
            ? MixHex(brandColor, "#050306", 0.44)
            : MixHex(brandColor, "#12090A", 0.34);
        var accentSoftColor = isDark
            ? MixHex(brandColor, surfaceColor, 0.18)
            : MixHex(brandColor, "#FFFFFF", 0.12);

        return new MenuPalette(
            brandColor,
            textColor,
            backgroundColor,
            mutedColor,
            surfaceColor,
            surfaceStrongColor,
            borderColor,
            heroOverlayColor,
            accentSoftColor,
            GetReadableTextColor(brandColor));
    }

    private static ThemeTemplate GetThemeTemplate(string menuTheme)
    {
        return menuTheme switch
        {
            "ACAI" => new ThemeTemplate("#FFF5FA", "#160817", "#24101E", 0.075, 0.20),
            "CLASSICO" => new ThemeTemplate("#FBF3EA", "#18100C", "#271A12", 0.05, 0.15),
            "MINIMALISTA" => new ThemeTemplate("#F8F8F7", "#111315", "#191919", 0.035, 0.12),
            "PREMIUM" => new ThemeTemplate("#FAF5EA", "#11100D", "#231A10", 0.055, 0.18),
            _ => new ThemeTemplate("#FBF7F2", "#14100F", "#241914", 0.055, 0.16)
        };
    }

    private static string MixHex(string firstHex, string secondHex, double firstWeight)
    {
        var first = ParseHex(firstHex);
        var second = ParseHex(secondHex);
        var weight = Math.Clamp(firstWeight, 0, 1);
        var red = (int)Math.Round(first.Red * weight + second.Red * (1 - weight));
        var green = (int)Math.Round(first.Green * weight + second.Green * (1 - weight));
        var blue = (int)Math.Round(first.Blue * weight + second.Blue * (1 - weight));
        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    private static string GetReadableTextColor(string backgroundHex)
    {
        var color = ParseHex(backgroundHex);
        var luminance = GetLinearChannel(color.Red) * 0.2126 +
            GetLinearChannel(color.Green) * 0.7152 +
            GetLinearChannel(color.Blue) * 0.0722;
        return luminance > 0.48 ? "#241914" : "#FFFFFF";
    }

    private static double GetLinearChannel(int value)
    {
        var channel = value / 255d;
        return channel <= 0.03928
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }

    private static Rgb ParseHex(string hex)
    {
        var value = hex.TrimStart('#');
        return new Rgb(
            Convert.ToInt32(value[..2], 16),
            Convert.ToInt32(value.Substring(2, 2), 16),
            Convert.ToInt32(value.Substring(4, 2), 16));
    }

    private static string NormalizeBrandColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var color = value.Trim();
        if (!color.StartsWith('#'))
        {
            color = $"#{color}";
        }

        return color.Length == 7 && color.Skip(1).All(Uri.IsHexDigit)
            ? color.ToUpperInvariant()
            : fallback;
    }

    private sealed record CouponOrderUsage(Guid CouponId, int DiscountCents, DateTimeOffset CreatedAt);

    private sealed record CouponUsageStats(int UsageCount, int TotalDiscountCents, DateTimeOffset LastUsedAt);

    private sealed record CouponPricing(DiscountCoupon? Coupon, int DiscountCents, int TotalCents);

    private sealed record DeliveryCustomerInput(string Name, string Phone, string Address);

    private sealed record DiscountCouponInputNormalized(string Code, DiscountCouponType Type, decimal Value);

    private sealed record ThemeTemplate(string LightBase, string DarkBase, string TextLight, double LightBrandWeight, double DarkBrandWeight);

    private sealed record MenuPalette(
        string PrimaryColor,
        string TextColor,
        string BackgroundColor,
        string MutedColor,
        string SurfaceColor,
        string SurfaceStrongColor,
        string BorderColor,
        string HeroOverlayColor,
        string AccentSoftColor,
        string ButtonTextColor);

    private sealed record Rgb(int Red, int Green, int Blue);
}
