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

public sealed class WaiterLoginServiceTests
{
    [Fact]
    public async Task SaveWaiterLogin_CreatesAndUpdatesGarcomUserForRestaurant()
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
        db.Roles.Add(new IdentityRole(AppRoles.Garcom) { NormalizedName = AppRoles.Garcom });
        await db.SaveChangesAsync();

        using var userManager = CreateUserManager(db);
        var service = new WaiterLoginService(userManager);

        await service.SaveWaiterLoginAsync(restaurant.Id, new WaiterLoginInput
        {
            Email = "garcom1@teste.local",
            FullName = "Garçom Um",
            Password = "Senha123"
        });

        var created = await userManager.FindByEmailAsync("garcom1@teste.local");
        Assert.NotNull(created);
        Assert.Equal(restaurant.Id, created.RestaurantId);
        Assert.Equal("Garçom Um", created.FullName);
        Assert.True(await userManager.IsInRoleAsync(created, AppRoles.Garcom));
        Assert.True(await userManager.CheckPasswordAsync(created, "Senha123"));

        await service.SaveWaiterLoginAsync(restaurant.Id, new WaiterLoginInput
        {
            Email = "garcom2@teste.local",
            FullName = "Garçom Dois",
            Password = "OutraSenha123"
        });

        Assert.Null(await userManager.FindByEmailAsync("garcom1@teste.local"));
        var updated = await userManager.FindByEmailAsync("garcom2@teste.local");
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Garçom Dois", updated.FullName);
        Assert.True(await userManager.CheckPasswordAsync(updated, "OutraSenha123"));
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
