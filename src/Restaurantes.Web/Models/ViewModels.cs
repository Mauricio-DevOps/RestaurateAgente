using System.ComponentModel.DataAnnotations;

namespace Restaurantes.Web.Models;

public sealed class LoginInput
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, MinLength(8)]
    public string Password { get; set; } = "";

    public string? Next { get; set; }
}

public sealed class RestaurantAdminView
{
    public string? UserId { get; set; }
    public bool HasAdmin { get; set; }
    public Guid RestaurantId { get; set; }
    public string RestaurantName { get; set; } = "";
    public string AdminName { get; set; } = "";
    public string Email { get; set; } = "";
    public EntityStatus Status { get; set; }
    public RestaurantAccessMode AccessMode { get; set; }
    public string AccessModeLabel { get; set; } = "";
}

public sealed class MasterDashboardView
{
    public IReadOnlyList<RestaurantAdminView> Admins { get; set; } = [];
    public CreateRestaurantAdminInput CreateInput { get; set; } = new();
}

public sealed class CreateRestaurantAdminInput
{
    [Required, MinLength(2), MaxLength(120)]
    public string RestaurantName { get; set; } = "";

    [Required, MinLength(2), MaxLength(120)]
    public string AdminName { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, MinLength(8), MaxLength(72)]
    public string Password { get; set; } = "";

    public RestaurantAccessMode AccessMode { get; set; } = RestaurantAccessMode.Ambos;
}

public sealed class UpdateRestaurantAccessModeInput
{
    public RestaurantAccessMode AccessMode { get; set; } = RestaurantAccessMode.Ambos;
}

public sealed class RestaurantOverviewView
{
    public Guid RestaurantId { get; set; }
    public string RestaurantName { get; set; } = "";
    public string PublicMenuUrl { get; set; } = "";
    public int CategoryCount { get; set; }
    public int ActiveItemCount { get; set; }
    public int WaiterCount { get; set; }
    public int TableCount { get; set; }
    public RestaurantFeedbackDashboardView Feedback { get; set; } = new();
}

public sealed class RestaurantFeedbackDashboardView
{
    public int TotalFeedbacks { get; set; }
    public decimal? AverageRating { get; set; }
    public string AverageRatingLabel { get; set; } = "Sem notas";
    public IReadOnlyList<RestaurantFeedbackRatingView> Ratings { get; set; } = [];
    public IReadOnlyList<RestaurantFeedbackCommentView> Comments { get; set; } = [];
}

public sealed class RestaurantFeedbackRatingView
{
    public int Rating { get; set; }
    public int Count { get; set; }
    public int Percentage { get; set; }
    public string Label { get; set; } = "";
}

public sealed class RestaurantFeedbackCommentView
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public int Rating { get; set; }
    public string RatingLabel { get; set; } = "";
    public string Comment { get; set; } = "";
    public string TableNumber { get; set; } = "";
    public string CreatedAtLabel { get; set; } = "";
    public string TotalLabel { get; set; } = "";
}

public sealed class RestaurantMenuEditorView
{
    public Guid RestaurantId { get; set; }
    public string RestaurantName { get; set; } = "";
    public string? PublicDescription { get; set; }
    public string? CoverImageUrl { get; set; }
    public string PrimaryColor { get; set; } = "#B14623";
    public string SecondaryColor { get; set; } = "#F2D0B8";
    public string BackgroundColor { get; set; } = "#F6F3EF";
    public string MenuTheme { get; set; } = "ELEGANTE";
    public string MenuMode { get; set; } = "CLARO";
    public IReadOnlyList<RestaurantTableView> Tables { get; set; } = [];
    public IReadOnlyList<DiscountCouponView> Coupons { get; set; } = [];
    public IReadOnlyList<MenuCategoryView> Categories { get; set; } = [];
}

public sealed class DiscountCouponView
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public DiscountCouponType Type { get; set; }
    public string TypeLabel { get; set; } = "";
    public decimal Value { get; set; }
    public string ValueInput { get; set; } = "";
    public string ValueLabel { get; set; } = "";
    public EntityStatus Status { get; set; }
    public int UsageCount { get; set; }
    public string TotalDiscountLabel { get; set; } = "";
    public string LastUsedAtLabel { get; set; } = "";
}

public sealed class DiscountCouponInput
{
    [Required, MinLength(3), MaxLength(40)]
    public string Code { get; set; } = "";

    public DiscountCouponType Type { get; set; }

    [Required, MaxLength(32)]
    public string ValueInput { get; set; } = "";
}

public sealed class MenuCategoryView
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public EntityStatus Status { get; set; }
    public IReadOnlyList<MenuItemView> Items { get; set; } = [];
}

public sealed class MenuItemView
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int PriceCents { get; set; }
    public string PriceLabel { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? WhatsAppProductId { get; set; }
    public EntityStatus Status { get; set; }
    public bool IsPromotion { get; set; }
    public bool IsPromotionActive { get; set; }
    public DateTimeOffset? PromotionStartsAt { get; set; }
    public DateTimeOffset? PromotionEndsAt { get; set; }
    public string? PromotionPeriodLabel { get; set; }
}

public sealed class MenuItemPromotionInput
{
    public bool IsPromotion { get; set; }
    public string? PromotionDuration { get; set; }
    public DateOnly? PromotionStartsOn { get; set; }
    public DateOnly? PromotionEndsOn { get; set; }
}

public sealed record MenuItemWhatsAppSyncContext(
    Guid RestaurantId,
    Guid MenuItemId,
    string? StoreId,
    string? WhatsAppProductId,
    string Name,
    string Description,
    decimal RetailPrice,
    bool IsActive);

public sealed record WhatsAppProductSyncRequest(
    string StoreId,
    string? ProductId,
    string Name,
    string? Description,
    decimal RetailPrice,
    bool IsActive);

public sealed record WhatsAppProductSyncResponse(
    string Id,
    string StoreId,
    string Name,
    string? Description,
    decimal RetailPrice,
    decimal WholesalePrice,
    IReadOnlyList<string> Aliases,
    bool IsActive);

public sealed record MenuItemSyncFromProductRequest(
    string StoreId,
    string ProductId,
    string Name,
    string? Description,
    decimal RetailPrice);

public sealed record MenuItemSyncFromProductResponse(
    Guid Id,
    Guid RestaurantId,
    Guid CategoryId,
    string WhatsAppProductId,
    string Name,
    EntityStatus Status,
    bool Created);

public sealed class RestaurantOperationsView
{
    public Guid RestaurantId { get; set; }
    public string RestaurantName { get; set; } = "";
    public string WhatsAppPhone { get; set; } = "";
    public bool CanManageRestaurantOperations { get; set; }
    public bool CanManageWhatsApp { get; set; }
    public int? PendingSlaMinutes { get; set; }
    public int? AttendanceSlaMinutes { get; set; }
    public IReadOnlyList<RestaurantWaiterView> Waiters { get; set; } = [];
    public IReadOnlyList<RestaurantTableView> Tables { get; set; } = [];
    public WaiterLoginView WaiterLogin { get; set; } = new();
    public RestaurantOperationalBottlenecksView Bottlenecks { get; set; } = new();
}

public sealed class WhatsAppContactInput
{
    [Required, MaxLength(32)]
    public string Phone { get; set; } = "";
}

public sealed class RestaurantSlaInput
{
    [Range(1, 1440)]
    public int? PendingSlaMinutes { get; set; }

    [Range(1, 1440)]
    public int? AttendanceSlaMinutes { get; set; }
}

public sealed class RestaurantOperationalBottlenecksView
{
    public int OpenOrderCount { get; set; }
    public int PendingOrderCount { get; set; }
    public int AttendanceOrderCount { get; set; }
    public int OverSlaOrderCount { get; set; }
    public IReadOnlyList<RestaurantOperationalBottleneckOrderView> DelayedOrders { get; set; } = [];
}

public sealed class RestaurantOperationalBottleneckOrderView
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public string StageLabel { get; set; } = "";
    public string ElapsedLabel { get; set; } = "";
    public string SlaLabel { get; set; } = "";
    public string DelayLabel { get; set; } = "";
    public bool IsOverSla { get; set; }
}

public sealed record RestaurantWhatsAppPhoneUpdate(
    Guid RestaurantId,
    string RestaurantName,
    string? PreviousPhone,
    string NewPhone);

public sealed record RestaurantWhatsAppSsoContext(
    Guid RestaurantId,
    string RestaurantName,
    string CompanyPhone,
    RestaurantAccessMode AccessMode);

public sealed class WaiterLoginView
{
    public bool HasLogin { get; set; }
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public EntityStatus Status { get; set; } = EntityStatus.ACTIVE;
}

public sealed class WaiterLoginInput
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, MinLength(2), MaxLength(120)]
    public string FullName { get; set; } = "";

    [MinLength(8), MaxLength(72)]
    public string? Password { get; set; }
}

public sealed class RestaurantWaiterView
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class RestaurantTableView
{
    public Guid Id { get; set; }
    public string TableNumber { get; set; } = "";
    public Guid? AssignedWaiterId { get; set; }
    public string? AssignedWaiterName { get; set; }
}

public sealed class PublicMenuView
{
    public Guid RestaurantId { get; set; }
    public string RestaurantName { get; set; } = "";
    public string? PublicDescription { get; set; }
    public string? CoverImageUrl { get; set; }
    public string PrimaryColor { get; set; } = "#B14623";
    public string SecondaryColor { get; set; } = "#F2D0B8";
    public string BackgroundColor { get; set; } = "#F6F3EF";
    public string MenuTheme { get; set; } = "ELEGANTE";
    public string MenuMode { get; set; } = "CLARO";
    public string MutedColor { get; set; } = "#66584F";
    public string SurfaceColor { get; set; } = "#FFFCF8";
    public string SurfaceStrongColor { get; set; } = "#FFF7EF";
    public string BorderColor { get; set; } = "#E6D8CF";
    public string HeroOverlayColor { get; set; } = "#160D10";
    public string AccentSoftColor { get; set; } = "#F5E8E1";
    public string ButtonTextColor { get; set; } = "#FFFFFF";
    public bool IsDelivery { get; set; }
    public bool HasInvalidTable { get; set; }
    public string? RequestedTableNumber { get; set; }
    public Guid? CurrentTableId { get; set; }
    public string? CurrentTableNumber { get; set; }
    public IReadOnlyList<RestaurantTableView> Tables { get; set; } = [];
    public IReadOnlyList<MenuItemView> PromotionalItems { get; set; } = [];
    public IReadOnlyList<MenuCategoryView> Categories { get; set; } = [];
}

public sealed class PublicRestaurantTableSession
{
    public Guid TableId { get; set; }
    public string TableNumber { get; set; } = "";
    public bool HasOpenTab { get; set; }
}

public sealed class PublicOrderSubmissionInput
{
    public Guid RestaurantId { get; set; }
    public Guid? TableId { get; set; }
    public string? CouponCode { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? DeliveryAddress { get; set; }
    public List<PublicOrderItemInput> Items { get; set; } = [];
}

public sealed class PublicOrderItemInput
{
    public Guid MenuItemId { get; set; }
    public int Quantity { get; set; }
}

public sealed class PublicServiceRequestInput
{
    public Guid RestaurantId { get; set; }
    public Guid TableId { get; set; }
    public ServiceRequestType Type { get; set; }
}

public sealed class PublicCouponValidationInput
{
    public Guid RestaurantId { get; set; }
    public string? CouponCode { get; set; }
    public List<PublicOrderItemInput> Items { get; set; } = [];
}

public sealed record PublicCouponValidationResponse(
    string CouponCode,
    string DiscountType,
    decimal DiscountValue,
    string DiscountDescription,
    int SubtotalCents,
    string SubtotalLabel,
    int DiscountCents,
    string DiscountLabel,
    int TotalCents,
    string TotalLabel);

public sealed class PublicOrderFeedbackInput
{
    public Guid OrderId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public sealed class UpdateOperationalEventStatusInput
{
    public string EventKind { get; set; } = "";
    public Guid EventId { get; set; }
    public OperationalEventStatus NextStatus { get; set; }
}

public sealed class WaiterDashboardView
{
    public Guid RestaurantId { get; set; }
    public string RestaurantName { get; set; } = "";
    public Guid? SelectedWaiterId { get; set; }
    public IReadOnlyList<RestaurantWaiterView> Waiters { get; set; } = [];
    public IReadOnlyList<WaiterQueueEventView> Queue { get; set; } = [];
}

public sealed class WaiterQueueEventView
{
    public Guid Id { get; set; }
    public string EventKind { get; set; } = "";
    public OperationalEventStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string TableNumber { get; set; } = "";
    public Guid? AssignedWaiterId { get; set; }
    public string? AssignedWaiterName { get; set; }
    public string OwnershipLabel { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public int? SubtotalCents { get; set; }
    public string? SubtotalLabel { get; set; }
    public int DiscountCents { get; set; }
    public string? DiscountLabel { get; set; }
    public string? CouponCode { get; set; }
    public string? CouponSummary { get; set; }
    public int? TotalCents { get; set; }
    public string? TotalLabel { get; set; }
    public IReadOnlyList<WaiterQueueOrderItemView> Items { get; set; } = [];
    public ServiceRequestType? RequestType { get; set; }
    public int? PendingSlaMinutes { get; set; }
    public int? AttendanceSlaMinutes { get; set; }
    public int? CurrentSlaMinutes { get; set; }
    public string? CurrentStageLabel { get; set; }
    public DateTimeOffset? CurrentStageStartedAt { get; set; }
    public DateTimeOffset? CurrentStageEndedAt { get; set; }
    public int CurrentStageElapsedSeconds { get; set; }
    public bool IsOverSla { get; set; }
    public string? SlaLabel { get; set; }
}

public sealed class WaiterQueueOrderItemView
{
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public string UnitPriceLabel { get; set; } = "";
    public string LineTotalLabel { get; set; } = "";
}

public sealed class RestaurantDeliveryDashboardView
{
    public Guid RestaurantId { get; set; }
    public string RestaurantName { get; set; } = "";
}

public sealed class DeliveryOrderView
{
    public Guid Id { get; set; }
    public OperationalEventStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public string DeliveryAddress { get; set; } = "";
    public string Summary { get; set; } = "";
    public int SubtotalCents { get; set; }
    public string SubtotalLabel { get; set; } = "";
    public int DiscountCents { get; set; }
    public string DiscountLabel { get; set; } = "";
    public string? CouponCode { get; set; }
    public string? CouponSummary { get; set; }
    public int TotalCents { get; set; }
    public string TotalLabel { get; set; } = "";
    public IReadOnlyList<WaiterQueueOrderItemView> Items { get; set; } = [];
}

public sealed class DeliveryOrderStatusInput
{
    public Guid OrderId { get; set; }
    public OperationalEventStatus NextStatus { get; set; }
}
