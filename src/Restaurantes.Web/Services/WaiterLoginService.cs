using Microsoft.AspNetCore.Identity;
using Restaurantes.Web.Auth;
using Restaurantes.Web.Models;

namespace Restaurantes.Web.Services;

public sealed class WaiterLoginService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public WaiterLoginService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task SaveWaiterLoginAsync(Guid restaurantId, WaiterLoginInput input)
    {
        var email = input.Email.Trim();
        var fullName = input.FullName.Trim();
        var password = string.IsNullOrWhiteSpace(input.Password) ? null : input.Password;
        var waiterLogin = await FindWaiterLoginAsync(restaurantId);

        if (waiterLogin is null)
        {
            if (password is null)
            {
                throw new InvalidOperationException("Informe uma senha para criar o login do garçom.");
            }

            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser is not null)
            {
                throw new InvalidOperationException("Esse email já está em uso por outro usuário.");
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                RestaurantId = restaurantId,
                ProfileStatus = EntityStatus.ACTIVE
            };

            var createResult = await _userManager.CreateAsync(user, password);
            EnsureSucceeded(createResult);
            EnsureSucceeded(await _userManager.AddToRoleAsync(user, AppRoles.Garcom));
            return;
        }

        var userWithRequestedEmail = await _userManager.FindByEmailAsync(email);
        if (userWithRequestedEmail is not null && userWithRequestedEmail.Id != waiterLogin.Id)
        {
            throw new InvalidOperationException("Esse email já está em uso por outro usuário.");
        }

        waiterLogin.UserName = email;
        waiterLogin.Email = email;
        waiterLogin.EmailConfirmed = true;
        waiterLogin.FullName = fullName;
        waiterLogin.RestaurantId = restaurantId;
        waiterLogin.ProfileStatus = EntityStatus.ACTIVE;

        EnsureSucceeded(await _userManager.UpdateAsync(waiterLogin));

        if (password is not null)
        {
            if (await _userManager.HasPasswordAsync(waiterLogin))
            {
                EnsureSucceeded(await _userManager.RemovePasswordAsync(waiterLogin));
            }

            EnsureSucceeded(await _userManager.AddPasswordAsync(waiterLogin, password));
        }

        if (!await _userManager.IsInRoleAsync(waiterLogin, AppRoles.Garcom))
        {
            EnsureSucceeded(await _userManager.AddToRoleAsync(waiterLogin, AppRoles.Garcom));
        }
    }

    private async Task<ApplicationUser?> FindWaiterLoginAsync(Guid restaurantId)
    {
        var waiters = await _userManager.GetUsersInRoleAsync(AppRoles.Garcom);
        return waiters
            .Where(user => user.RestaurantId == restaurantId)
            .OrderBy(user => user.Email)
            .FirstOrDefault();
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
