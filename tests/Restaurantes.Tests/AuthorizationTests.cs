using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Restaurantes.Web.Auth;
using Restaurantes.Web.Controllers;

namespace Restaurantes.Tests;

public sealed class AuthorizationTests
{
    [Fact]
    public void ProtectedAreaControllers_DeclareExpectedRoles()
    {
        Assert.Equal(AppRoles.Master, GetAuthorizeRoles<MasterController>());
        Assert.Equal(AppRoles.AdminRestaurante, GetAuthorizeRoles<RestauranteController>());
        Assert.Equal(AppRoles.AdminRestaurante, GetAuthorizeRoles<WhatsAppBridgeController>());
        Assert.Equal(AppRoles.Garcom, GetAuthorizeRoles<GarcomController>());
        Assert.Equal(AppRoles.Garcom, GetAuthorizeRoles<ApiGarcomController>());
    }

    [Fact]
    public void PublicMenuAndPublicApi_DoNotRequireAuthorization()
    {
        Assert.Null(typeof(CardapioController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(typeof(ApiPublicRestaurantsController).GetCustomAttribute<AuthorizeAttribute>());
    }

    private static string? GetAuthorizeRoles<T>()
    {
        return typeof(T).GetCustomAttribute<AuthorizeAttribute>()?.Roles;
    }
}
