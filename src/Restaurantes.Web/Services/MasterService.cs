using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Restaurantes.Web.Auth;
using Restaurantes.Web.Data;
using Restaurantes.Web.Models;

namespace Restaurantes.Web.Services;

public sealed class MasterService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public MasterService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<MasterDashboardView> GetDashboardAsync()
    {
        var admins = await _userManager.GetUsersInRoleAsync(AppRoles.AdminRestaurante);
        var adminByRestaurantId = admins
            .Where(user => user.RestaurantId.HasValue)
            .GroupBy(user => user.RestaurantId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(user => user.Email).First());
        var restaurants = await _db.Restaurants
            .OrderBy(restaurant => restaurant.Name)
            .ToListAsync();

        return new MasterDashboardView
        {
            Admins = restaurants
                .Select(restaurant =>
                {
                    adminByRestaurantId.TryGetValue(restaurant.Id, out var user);
                    return new RestaurantAdminView
                    {
                        UserId = user?.Id,
                        HasAdmin = user is not null,
                        RestaurantId = restaurant.Id,
                        RestaurantName = restaurant.Name,
                        AdminName = user?.FullName ?? "Sem admin vinculado",
                        Email = user?.Email ?? "",
                        Status = user?.ProfileStatus ?? restaurant.Status,
                        AccessMode = restaurant.AccessMode,
                        AccessModeLabel = RestaurantPortalAccess.ToDisplayLabel(restaurant.AccessMode)
                    };
                })
                .ToList()
        };
    }

    public async Task CreateRestaurantAdminAsync(CreateRestaurantAdminInput input)
    {
        var slug = RestaurantText.Slugify(input.RestaurantName);
        var uniqueSlug = await BuildUniqueSlugAsync(slug);
        var restaurant = new Restaurant
        {
            Name = input.RestaurantName.Trim(),
            Slug = uniqueSlug,
            AccessMode = input.AccessMode
        };

        _db.Restaurants.Add(restaurant);
        await _db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            UserName = input.Email.Trim(),
            Email = input.Email.Trim(),
            EmailConfirmed = true,
            FullName = input.AdminName.Trim(),
            RestaurantId = restaurant.Id
        };

        var result = await _userManager.CreateAsync(user, input.Password);
        if (!result.Succeeded)
        {
            _db.Restaurants.Remove(restaurant);
            await _db.SaveChangesAsync();
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        await _userManager.AddToRoleAsync(user, AppRoles.AdminRestaurante);
    }

    public async Task UpdateRestaurantAccessModeAsync(Guid restaurantId, RestaurantAccessMode accessMode)
    {
        var restaurant = await _db.Restaurants.FindAsync(restaurantId)
            ?? throw new InvalidOperationException("Restaurante nao encontrado.");
        restaurant.AccessMode = accessMode;
        restaurant.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task UpdateRestaurantAccessModeAsync(string userId, RestaurantAccessMode accessMode)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("Administrador nao encontrado.");
        if (!user.RestaurantId.HasValue)
        {
            throw new InvalidOperationException("Administrador sem restaurante vinculado.");
        }

        await UpdateRestaurantAccessModeAsync(user.RestaurantId.Value, accessMode);
    }

    public async Task ToggleAdminStatusAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("Administrador nao encontrado.");
        user.ProfileStatus = user.ProfileStatus == EntityStatus.ACTIVE ? EntityStatus.INACTIVE : EntityStatus.ACTIVE;
        await _userManager.UpdateAsync(user);
    }

    public async Task ResetAdminPasswordAsync(string userId, string password)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("Administrador nao encontrado.");
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }

    public async Task DeleteAdminAsync(string userId, Guid? fallbackRestaurantId = null)
    {
        var user = await _userManager.FindByIdAsync(userId);
        var restaurantId = user?.RestaurantId ?? fallbackRestaurantId;
        if (restaurantId.HasValue)
        {
            await DeleteRestaurantAsync(restaurantId.Value);
            return;
        }

        if (user is null)
        {
            throw new InvalidOperationException("Administrador nao encontrado.");
        }

        var deleteResult = await _userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", deleteResult.Errors.Select(error => error.Description)));
        }
    }

    public async Task DeleteRestaurantAsync(Guid restaurantId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var users = await _db.Users
                .Where(user => user.RestaurantId == restaurantId)
                .OrderBy(user => user.Email)
                .ToListAsync();

            foreach (var user in users)
            {
                var deleteResult = await _userManager.DeleteAsync(user);
                if (!deleteResult.Succeeded)
                {
                    throw new InvalidOperationException(string.Join("; ", deleteResult.Errors.Select(error => error.Description)));
                }
            }

            var restaurant = await _db.Restaurants.FindAsync(restaurantId);
            if (restaurant is not null)
            {
                _db.Restaurants.Remove(restaurant);
                await _db.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<string> BuildUniqueSlugAsync(string baseSlug)
    {
        var slug = baseSlug;
        var counter = 2;
        while (await _db.Restaurants.AnyAsync(restaurant => restaurant.Slug == slug))
        {
            slug = $"{baseSlug}-{counter}";
            counter++;
        }

        return slug;
    }
}
