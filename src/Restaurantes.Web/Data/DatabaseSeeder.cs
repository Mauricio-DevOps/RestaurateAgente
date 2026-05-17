using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Restaurantes.Web.Auth;
using Restaurantes.Web.Models;
using Restaurantes.Web.Services;

namespace Restaurantes.Web.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var restaurant = await db.Restaurants.FirstOrDefaultAsync(item => item.Id == DefaultData.DemoRestaurantId);
        if (restaurant is null)
        {
            restaurant = new Restaurant
            {
                Id = DefaultData.DemoRestaurantId,
                Name = "Bistrô da Praça",
                Slug = "bistro-da-praca",
                PublicDescription = "Comida afetiva, pratos autorais e atendimento de salão integrado.",
                PrimaryColor = "#B14623",
                SecondaryColor = "#F2D0B8",
                BackgroundColor = "#F6F3EF",
                MenuTheme = "ELEGANTE",
                MenuMode = "CLARO"
            };
            db.Restaurants.Add(restaurant);
        }

        await db.SaveChangesAsync();

        await EnsureUserAsync(userManager, DefaultData.MasterEmail, "Master Operacional", AppRoles.Master, null);
        await EnsureUserAsync(userManager, DefaultData.AdminEmail, "Admin do Restaurante", AppRoles.AdminRestaurante, restaurant.Id);
        await EnsureUserAsync(userManager, DefaultData.GarcomEmail, "Garçom Operacional", AppRoles.Garcom, restaurant.Id);

        if (!await db.RestaurantWaiters.AnyAsync(item => item.RestaurantId == restaurant.Id))
        {
            var waiter = new RestaurantWaiter { RestaurantId = restaurant.Id, Name = "Garçom Principal" };
            db.RestaurantWaiters.Add(waiter);
            await db.SaveChangesAsync();

            db.RestaurantTables.AddRange(
                new RestaurantTable { RestaurantId = restaurant.Id, TableNumber = "1", AssignedWaiterId = waiter.Id },
                new RestaurantTable { RestaurantId = restaurant.Id, TableNumber = "2", AssignedWaiterId = waiter.Id },
                new RestaurantTable { RestaurantId = restaurant.Id, TableNumber = "3" });
        }

        if (!await db.MenuCategories.AnyAsync(item => item.RestaurantId == restaurant.Id))
        {
            var entradas = new MenuCategory { RestaurantId = restaurant.Id, Name = "Entradas", SortOrder = 1 };
            var principais = new MenuCategory { RestaurantId = restaurant.Id, Name = "Pratos principais", SortOrder = 2 };
            db.MenuCategories.AddRange(entradas, principais);
            await db.SaveChangesAsync();

            db.MenuItems.AddRange(
                new MenuItem
                {
                    RestaurantId = restaurant.Id,
                    CategoryId = entradas.Id,
                    Name = "Bruschetta da casa",
                    Description = "Pão artesanal, tomate marinado e manjericão.",
                    PriceCents = 2800,
                    SortOrder = 1
                },
                new MenuItem
                {
                    RestaurantId = restaurant.Id,
                    CategoryId = principais.Id,
                    Name = "Risoto de cogumelos",
                    Description = "Arroz arbóreo, mix de cogumelos e queijo curado.",
                    PriceCents = 6200,
                    SortOrder = 1
                });
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string role,
        Guid? restaurantId)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                RestaurantId = restaurantId,
                ProfileStatus = EntityStatus.ACTIVE
            };

            var createResult = await userManager.CreateAsync(user, DefaultData.DevPassword);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(error => error.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
