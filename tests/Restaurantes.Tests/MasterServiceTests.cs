using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Restaurantes.Web.Auth;
using Restaurantes.Web.Data;
using Restaurantes.Web.Models;
using Restaurantes.Web.Services;

namespace Restaurantes.Tests;

public sealed class MasterServiceTests
{
    [Fact]
    public async Task CreateRestaurantAdminAsync_SavesSelectedAccessMode()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Roles.Add(new IdentityRole(AppRoles.AdminRestaurante) { NormalizedName = AppRoles.AdminRestaurante });
        await db.SaveChangesAsync();

        using var userManager = CreateUserManager(db);
        var service = new MasterService(db, userManager);

        await service.CreateRestaurantAdminAsync(new CreateRestaurantAdminInput
        {
            RestaurantName = "Acai Central",
            AdminName = "Admin WhatsApp",
            Email = "admin@acai.local",
            Password = "Senha123",
            AccessMode = RestaurantAccessMode.SoWhatsApp
        });

        var restaurant = await db.Restaurants.SingleAsync();
        var user = await userManager.FindByEmailAsync("admin@acai.local");

        Assert.Equal(RestaurantAccessMode.SoWhatsApp, restaurant.AccessMode);
        Assert.NotNull(user);
        Assert.Equal(restaurant.Id, user.RestaurantId);
        Assert.True(await userManager.IsInRoleAsync(user, AppRoles.AdminRestaurante));
    }

    [Fact]
    public async Task UpdateRestaurantAccessModeAsync_UpdatesAssociatedRestaurant()
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
            Name = "Bistro",
            Slug = "bistro",
            AccessMode = RestaurantAccessMode.Ambos
        };
        db.Restaurants.Add(restaurant);
        db.Roles.Add(new IdentityRole(AppRoles.AdminRestaurante) { NormalizedName = AppRoles.AdminRestaurante });
        await db.SaveChangesAsync();

        using var userManager = CreateUserManager(db);
        var user = new ApplicationUser
        {
            UserName = "admin@bistro.local",
            Email = "admin@bistro.local",
            EmailConfirmed = true,
            FullName = "Admin Bistro",
            RestaurantId = restaurant.Id
        };
        var created = await userManager.CreateAsync(user, "Senha123");
        Assert.True(created.Succeeded);
        await userManager.AddToRoleAsync(user, AppRoles.AdminRestaurante);

        var service = new MasterService(db, userManager);
        await service.UpdateRestaurantAccessModeAsync(restaurant.Id, RestaurantAccessMode.SoRestaurante);

        var updatedRestaurant = await db.Restaurants.SingleAsync();
        Assert.Equal(RestaurantAccessMode.SoRestaurante, updatedRestaurant.AccessMode);
    }

    [Fact]
    public async Task GetDashboardAsync_IncludesRestaurantsWithoutAdmin()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Restaurants.Add(new Restaurant
        {
            Name = "Bistro sem admin",
            Slug = "bistro-sem-admin",
            AccessMode = RestaurantAccessMode.SoWhatsApp
        });
        await db.SaveChangesAsync();

        using var userManager = CreateUserManager(db);
        var service = new MasterService(db, userManager);

        var dashboard = await service.GetDashboardAsync();

        var row = Assert.Single(dashboard.Admins);
        Assert.False(row.HasAdmin);
        Assert.Null(row.UserId);
        Assert.Equal("Bistro sem admin", row.RestaurantName);
        Assert.Equal("Sem admin vinculado", row.AdminName);
        Assert.Equal(RestaurantAccessMode.SoWhatsApp, row.AccessMode);
    }

    [Fact]
    public async Task DeleteRestaurantAsync_RemovesRestaurantAndAllScopedUsers()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Roles.AddRange(
            new IdentityRole(AppRoles.AdminRestaurante) { NormalizedName = AppRoles.AdminRestaurante },
            new IdentityRole(AppRoles.Garcom) { NormalizedName = AppRoles.Garcom });
        await db.SaveChangesAsync();

        var restaurant = new Restaurant
        {
            Name = "Bistro para excluir",
            Slug = "bistro-para-excluir"
        };
        db.Restaurants.Add(restaurant);
        await db.SaveChangesAsync();

        using var userManager = CreateUserManager(db);

        var admin = new ApplicationUser
        {
            UserName = "admin@delete.local",
            Email = "admin@delete.local",
            EmailConfirmed = true,
            FullName = "Admin Delete",
            RestaurantId = restaurant.Id
        };
        var adminCreated = await userManager.CreateAsync(admin, "Senha123");
        Assert.True(adminCreated.Succeeded);
        await userManager.AddToRoleAsync(admin, AppRoles.AdminRestaurante);

        var garcom = new ApplicationUser
        {
            UserName = "garcom@delete.local",
            Email = "garcom@delete.local",
            EmailConfirmed = true,
            FullName = "Garcom Delete",
            RestaurantId = restaurant.Id
        };
        var garcomCreated = await userManager.CreateAsync(garcom, "Senha123");
        Assert.True(garcomCreated.Succeeded);
        await userManager.AddToRoleAsync(garcom, AppRoles.Garcom);

        var service = new MasterService(db, userManager);
        await service.DeleteRestaurantAsync(restaurant.Id);

        Assert.False(await db.Restaurants.AnyAsync(item => item.Id == restaurant.Id));
        Assert.False(await db.Users.AnyAsync(item => item.RestaurantId == restaurant.Id));
    }

    private static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        var identityOptions = Options.Create(new IdentityOptions
        {
            User = { RequireUniqueEmail = true },
            Password =
            {
                RequiredLength = 8,
                RequireDigit = false,
                RequireLowercase = false,
                RequireNonAlphanumeric = false,
                RequireUppercase = false
            }
        });

        return new UserManager<ApplicationUser>(
            store,
            identityOptions,
            new PasswordHasher<ApplicationUser>(),
            [new UserValidator<ApplicationUser>()],
            [new PasswordValidator<ApplicationUser>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance);
    }
}
