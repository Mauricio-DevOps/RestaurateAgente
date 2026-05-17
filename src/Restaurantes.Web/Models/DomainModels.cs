using Microsoft.AspNetCore.Identity;

namespace Restaurantes.Web.Models;

public enum EntityStatus
{
    ACTIVE,
    INACTIVE
}

public enum RestaurantAccessMode
{
    Ambos,
    SoRestaurante,
    SoWhatsApp
}

public enum RestaurantTabStatus
{
    ABERTA,
    FECHADA
}

public enum OperationalEventStatus
{
    PENDENTE,
    EM_ATENDIMENTO,
    RESOLVIDO
}

public enum ServiceRequestType
{
    CHAMAR_GARCOM,
    PEDIR_CONTA
}

public enum DiscountCouponType
{
    PERCENTUAL,
    VALOR_FIXO
}

public enum OrderType
{
    MESA,
    DELIVERY
}

public sealed class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = "";
    public EntityStatus ProfileStatus { get; set; } = EntityStatus.ACTIVE;
    public Guid? RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }
}

public sealed class Restaurant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public EntityStatus Status { get; set; } = EntityStatus.ACTIVE;
    public RestaurantAccessMode AccessMode { get; set; } = RestaurantAccessMode.Ambos;
    public string? PublicDescription { get; set; }
    public string? CoverImageUrl { get; set; }
    public string PrimaryColor { get; set; } = "#B14623";
    public string SecondaryColor { get; set; } = "#F2D0B8";
    public string BackgroundColor { get; set; } = "#F6F3EF";
    public string MenuTheme { get; set; } = "ELEGANTE";
    public string MenuMode { get; set; } = "CLARO";
    public string? WhatsAppPhone { get; set; }
    public int? PendingSlaMinutes { get; set; }
    public int? AttendanceSlaMinutes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ApplicationUser> Users { get; set; } = [];
    public ICollection<MenuCategory> Categories { get; set; } = [];
    public ICollection<RestaurantWaiter> Waiters { get; set; } = [];
    public ICollection<RestaurantTable> Tables { get; set; } = [];
    public ICollection<DiscountCoupon> DiscountCoupons { get; set; } = [];
    public ICollection<OrderFeedback> Feedbacks { get; set; } = [];
}

public sealed class Cliente
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Nome { get; set; }
    public string? CpfCnpj { get; set; }
    public string? Email { get; set; }
    public string TelefoneCelular { get; set; } = "";
    public DateTimeOffset DataCriacao { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MenuCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }
    public string Name { get; set; } = "";
    public EntityStatus Status { get; set; } = EntityStatus.ACTIVE;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<MenuItem> Items { get; set; } = [];
}

public sealed class MenuItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }
    public Guid CategoryId { get; set; }
    public MenuCategory? Category { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int PriceCents { get; set; }
    public string? ImageUrl { get; set; }
    public string? WhatsAppProductId { get; set; }
    public bool IsPromotion { get; set; }
    public DateTimeOffset? PromotionStartsAt { get; set; }
    public DateTimeOffset? PromotionEndsAt { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.ACTIVE;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RestaurantWaiter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<RestaurantTable> Tables { get; set; } = [];
}

public sealed class RestaurantTable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }
    public string TableNumber { get; set; } = "";
    public Guid? AssignedWaiterId { get; set; }
    public RestaurantWaiter? AssignedWaiter { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DiscountCoupon
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }
    public string Code { get; set; } = "";
    public DiscountCouponType Type { get; set; }
    public decimal Value { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.ACTIVE;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Order> Orders { get; set; } = [];
}

public sealed class RestaurantTab
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }
    public Guid TableId { get; set; }
    public RestaurantTable? Table { get; set; }
    public RestaurantTabStatus Status { get; set; } = RestaurantTabStatus.ABERTA;
    public int TotalCents { get; set; }
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = [];
}

public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TabId { get; set; }
    public RestaurantTab? Tab { get; set; }
    public Guid RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }
    public Guid? TableId { get; set; }
    public RestaurantTable? Table { get; set; }
    public OrderType Type { get; set; } = OrderType.MESA;
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? DeliveryAddress { get; set; }
    public Guid? AssignedWaiterId { get; set; }
    public RestaurantWaiter? AssignedWaiter { get; set; }
    public Guid? HandledByWaiterId { get; set; }
    public RestaurantWaiter? HandledByWaiter { get; set; }
    public Guid? DiscountCouponId { get; set; }
    public DiscountCoupon? DiscountCoupon { get; set; }
    public OperationalEventStatus Status { get; set; } = OperationalEventStatus.PENDENTE;
    public int SubtotalCents { get; set; }
    public int DiscountCents { get; set; }
    public string? CouponCodeSnapshot { get; set; }
    public string? CouponTypeSnapshot { get; set; }
    public decimal? CouponValueSnapshot { get; set; }
    public int TotalCents { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<OrderItem> Items { get; set; } = [];
    public OrderFeedback? Feedback { get; set; }
}

public sealed class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public Guid? MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }
    public int Quantity { get; set; }
    public string ItemNameSnapshot { get; set; } = "";
    public int ItemPriceCentsSnapshot { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class OrderFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public Guid? TableId { get; set; }
    public RestaurantTable? Table { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ServiceRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TabId { get; set; }
    public RestaurantTab? Tab { get; set; }
    public Guid RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }
    public Guid TableId { get; set; }
    public RestaurantTable? Table { get; set; }
    public Guid? AssignedWaiterId { get; set; }
    public RestaurantWaiter? AssignedWaiter { get; set; }
    public Guid? HandledByWaiterId { get; set; }
    public RestaurantWaiter? HandledByWaiter { get; set; }
    public ServiceRequestType Type { get; set; }
    public OperationalEventStatus Status { get; set; } = OperationalEventStatus.PENDENTE;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
