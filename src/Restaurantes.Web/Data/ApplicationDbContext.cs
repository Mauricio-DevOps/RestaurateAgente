using System.Globalization;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Restaurantes.Web.Models;

namespace Restaurantes.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private static readonly ValueConverter<Guid, string> GuidToStringConverter = new(
        value => value.ToString().ToUpperInvariant(),
        value => Guid.Parse(value));

    private static readonly ValueConverter<Guid?, string?> NullableGuidToStringConverter = new(
        value => value.HasValue ? value.Value.ToString().ToUpperInvariant() : null,
        value => string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value));

    private static readonly ValueConverter<DateTimeOffset, string> DateTimeOffsetToStringConverter = new(
        value => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        value => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private static readonly ValueConverter<DateTimeOffset?, string?> NullableDateTimeOffsetToStringConverter = new(
        value => value.HasValue ? value.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) : null,
        value => string.IsNullOrWhiteSpace(value) ? null : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private static readonly ValueConverter<bool, int> BoolToIntegerConverter = new(
        value => value ? 1 : 0,
        value => value != 0);

    private static readonly ValueConverter<bool?, int?> NullableBoolToIntegerConverter = new(
        value => value.HasValue ? value.Value ? 1 : 0 : null,
        value => value.HasValue ? value.Value != 0 : null);

    private static readonly ValueConverter<decimal, string> DecimalToStringConverter = new(
        value => value.ToString(CultureInfo.InvariantCulture),
        value => decimal.Parse(value, CultureInfo.InvariantCulture));

    private static readonly ValueConverter<decimal?, string?> NullableDecimalToStringConverter = new(
        value => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : null,
        value => string.IsNullOrWhiteSpace(value) ? null : decimal.Parse(value, CultureInfo.InvariantCulture));

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<RestaurantWaiter> RestaurantWaiters => Set<RestaurantWaiter>();
    public DbSet<RestaurantTable> RestaurantTables => Set<RestaurantTable>();
    public DbSet<DiscountCoupon> DiscountCoupons => Set<DiscountCoupon>();
    public DbSet<RestaurantTab> RestaurantTabs => Set<RestaurantTab>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderFeedback> OrderFeedbacks => Set<OrderFeedback>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.FullName).HasMaxLength(120).IsRequired();
            entity.Property(user => user.ProfileStatus).HasConversion<string>().HasMaxLength(16);
            entity.HasOne(user => user.Restaurant)
                .WithMany(restaurant => restaurant.Users)
                .HasForeignKey(user => user.RestaurantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Restaurant>(entity =>
        {
            entity.Property(restaurant => restaurant.Name).HasMaxLength(120).IsRequired();
            entity.Property(restaurant => restaurant.Slug).HasMaxLength(140).IsRequired();
            entity.Property(restaurant => restaurant.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(restaurant => restaurant.AccessMode)
                .HasConversion<string>()
                .HasMaxLength(24)
                .HasDefaultValue(RestaurantAccessMode.Ambos);
            entity.Property(restaurant => restaurant.PublicDescription).HasMaxLength(280);
            entity.Property(restaurant => restaurant.LogoDataUrl);
            entity.Property(restaurant => restaurant.PrimaryColor).HasMaxLength(7);
            entity.Property(restaurant => restaurant.SecondaryColor).HasMaxLength(7);
            entity.Property(restaurant => restaurant.BackgroundColor).HasMaxLength(7);
            entity.Property(restaurant => restaurant.MenuTheme).HasMaxLength(32);
            entity.Property(restaurant => restaurant.MenuMode).HasMaxLength(16);
            entity.Property(restaurant => restaurant.WhatsAppPhone).HasMaxLength(32);
            entity.HasIndex(restaurant => restaurant.Slug).IsUnique();
            entity.HasIndex(restaurant => restaurant.WhatsAppPhone).IsUnique();
        });

        builder.Entity<Cliente>(entity =>
        {
            entity.ToTable("RestaurantClientes");
            entity.HasKey(cliente => cliente.Id);
            entity.Property(cliente => cliente.Id).HasColumnName("ID_Cliente");
            entity.Property(cliente => cliente.Nome).HasColumnName("CLIENTE_NOME").HasMaxLength(120);
            entity.Property(cliente => cliente.CpfCnpj).HasColumnName("CPF_CNPJ").HasMaxLength(32);
            entity.Property(cliente => cliente.Email).HasColumnName("CLIENTE_EMAIL").HasMaxLength(160);
            entity.Property(cliente => cliente.TelefoneCelular)
                .HasColumnName("CLIENTE_TELEFONE_CELULAR")
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(cliente => cliente.DataCriacao).HasColumnName("CLIENTE_DATA_CRIACAO");
            entity.HasIndex(cliente => cliente.TelefoneCelular).IsUnique();
            entity.HasIndex(cliente => cliente.CpfCnpj)
                .IsUnique()
                .HasFilter("\"cpf_cnpj\" IS NOT NULL AND \"cpf_cnpj\" <> ''");
            entity.HasIndex(cliente => cliente.Email);
        });

        builder.Entity<MenuCategory>(entity =>
        {
            entity.Property(category => category.Name).HasMaxLength(120).IsRequired();
            entity.Property(category => category.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(category => new { category.RestaurantId, category.SortOrder });
        });

        builder.Entity<MenuItem>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500).IsRequired();
            entity.Property(item => item.WhatsAppProductId).HasMaxLength(64);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(item => new { item.RestaurantId, item.IsPromotion });
            entity.HasIndex(item => new { item.CategoryId, item.SortOrder });
            entity.HasIndex(item => new { item.RestaurantId, item.WhatsAppProductId })
                .IsUnique()
                .HasFilter("\"whatsappproductid\" IS NOT NULL");
            entity.HasOne(item => item.Category)
                .WithMany(category => category.Items)
                .HasForeignKey(item => item.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RestaurantWaiter>(entity =>
        {
            entity.Property(waiter => waiter.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(waiter => new { waiter.RestaurantId, waiter.Name }).IsUnique();
        });

        builder.Entity<RestaurantTable>(entity =>
        {
            entity.Property(table => table.TableNumber).HasMaxLength(40).IsRequired();
            entity.HasIndex(table => new { table.RestaurantId, table.TableNumber }).IsUnique();
            entity.HasOne(table => table.AssignedWaiter)
                .WithMany(waiter => waiter.Tables)
                .HasForeignKey(table => table.AssignedWaiterId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<DiscountCoupon>(entity =>
        {
            entity.Property(coupon => coupon.Code).HasMaxLength(40).IsRequired();
            entity.Property(coupon => coupon.Type).HasConversion<string>().HasMaxLength(24);
            entity.Property(coupon => coupon.Value).HasPrecision(10, 2);
            entity.Property(coupon => coupon.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(coupon => new { coupon.RestaurantId, coupon.Code }).IsUnique();
            entity.HasOne(coupon => coupon.Restaurant)
                .WithMany(restaurant => restaurant.DiscountCoupons)
                .HasForeignKey(coupon => coupon.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RestaurantTab>(entity =>
        {
            entity.Property(tab => tab.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(tab => new { tab.RestaurantId, tab.TableId, tab.Status });
        });

        builder.Entity<Order>(entity =>
        {
            entity.ToTable("RestaurantOrders");
            entity.Property(order => order.Type).HasConversion<string>().HasMaxLength(16);
            entity.Property(order => order.CustomerName).HasMaxLength(120);
            entity.Property(order => order.CustomerPhone).HasMaxLength(32);
            entity.Property(order => order.DeliveryAddress).HasMaxLength(500);
            entity.Property(order => order.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(order => order.CouponCodeSnapshot).HasMaxLength(40);
            entity.Property(order => order.CouponTypeSnapshot).HasMaxLength(24);
            entity.Property(order => order.CouponValueSnapshot).HasPrecision(10, 2);
            entity.HasIndex(order => new { order.RestaurantId, order.CreatedAt });
            entity.HasIndex(order => new { order.RestaurantId, order.DiscountCouponId });
            entity.HasIndex(order => new { order.RestaurantId, order.Type, order.Status, order.CreatedAt });
            entity.HasOne(order => order.Tab)
                .WithMany(tab => tab.Orders)
                .HasForeignKey(order => order.TabId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(order => order.Table)
                .WithMany()
                .HasForeignKey(order => order.TableId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(order => order.AssignedWaiter)
                .WithMany()
                .HasForeignKey(order => order.AssignedWaiterId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(order => order.HandledByWaiter)
                .WithMany()
                .HasForeignKey(order => order.HandledByWaiterId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(order => order.DiscountCoupon)
                .WithMany(coupon => coupon.Orders)
                .HasForeignKey(order => order.DiscountCouponId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("RestaurantOrderItems");
            entity.Property(item => item.ItemNameSnapshot).HasMaxLength(120).IsRequired();
        });

        builder.Entity<OrderFeedback>(entity =>
        {
            entity.Property(feedback => feedback.Comment).HasMaxLength(600);
            entity.HasIndex(feedback => feedback.OrderId).IsUnique();
            entity.HasIndex(feedback => new { feedback.RestaurantId, feedback.Rating });
            entity.HasIndex(feedback => new { feedback.RestaurantId, feedback.CreatedAt });
            entity.HasOne(feedback => feedback.Restaurant)
                .WithMany(restaurant => restaurant.Feedbacks)
                .HasForeignKey(feedback => feedback.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(feedback => feedback.Order)
                .WithOne(order => order.Feedback)
                .HasForeignKey<OrderFeedback>(feedback => feedback.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(feedback => feedback.Table)
                .WithMany()
                .HasForeignKey(feedback => feedback.TableId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ServiceRequest>(entity =>
        {
            entity.Property(request => request.Type).HasConversion<string>().HasMaxLength(32);
            entity.Property(request => request.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(request => new { request.RestaurantId, request.CreatedAt });
            entity.HasOne(request => request.Tab)
                .WithMany(tab => tab.ServiceRequests)
                .HasForeignKey(request => request.TabId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(request => request.AssignedWaiter)
                .WithMany()
                .HasForeignKey(request => request.AssignedWaiterId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(request => request.HandledByWaiter)
                .WithMany()
                .HasForeignKey(request => request.HandledByWaiterId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        ApplySqliteImportMappings(builder);
    }

    private static void ApplySqliteImportMappings(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                entityType.SetTableName(tableName.ToLowerInvariant());
            }

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName();
                if (!string.IsNullOrWhiteSpace(columnName))
                {
                    property.SetColumnName(columnName.ToLowerInvariant());
                }

                if (property.ClrType == typeof(Guid))
                {
                    property.SetValueConverter(GuidToStringConverter);
                    property.SetColumnType("text");
                }
                else if (property.ClrType == typeof(Guid?))
                {
                    property.SetValueConverter(NullableGuidToStringConverter);
                    property.SetColumnType("text");
                }
                else if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(DateTimeOffsetToStringConverter);
                    property.SetColumnType("text");
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(NullableDateTimeOffsetToStringConverter);
                    property.SetColumnType("text");
                }
                else if (property.ClrType == typeof(bool))
                {
                    property.SetValueConverter(BoolToIntegerConverter);
                    property.SetColumnType("integer");
                }
                else if (property.ClrType == typeof(bool?))
                {
                    property.SetValueConverter(NullableBoolToIntegerConverter);
                    property.SetColumnType("integer");
                }
                else if (property.ClrType == typeof(decimal))
                {
                    property.SetValueConverter(DecimalToStringConverter);
                    property.SetColumnType("text");
                }
                else if (property.ClrType == typeof(decimal?))
                {
                    property.SetValueConverter(NullableDecimalToStringConverter);
                    property.SetColumnType("text");
                }
            }
        }
    }
}
