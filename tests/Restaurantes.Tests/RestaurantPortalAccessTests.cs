using Restaurantes.Web.Auth;
using Restaurantes.Web.Models;

namespace Restaurantes.Tests;

public sealed class RestaurantPortalAccessTests
{
    [Theory]
    [InlineData(RestaurantAccessMode.Ambos, "/restaurante", true)]
    [InlineData(RestaurantAccessMode.Ambos, "/restaurante/operacao", true)]
    [InlineData(RestaurantAccessMode.Ambos, "/sso/whatsapp?target=%2FDashboard", true)]
    [InlineData(RestaurantAccessMode.SoRestaurante, "/restaurante", true)]
    [InlineData(RestaurantAccessMode.SoRestaurante, "/restaurante/operacao?whatsappMissing=1", true)]
    [InlineData(RestaurantAccessMode.SoRestaurante, "/sso/whatsapp?target=%2FOrders", false)]
    [InlineData(RestaurantAccessMode.SoWhatsApp, "/restaurante", false)]
    [InlineData(RestaurantAccessMode.SoWhatsApp, "/restaurante/operacao", true)]
    [InlineData(RestaurantAccessMode.SoWhatsApp, "/sso/whatsapp?target=%2FProducts%3FeditProductId%3D1", true)]
    public void CanAccessPath_RespectsRestaurantMode(
        RestaurantAccessMode accessMode,
        string path,
        bool expected)
    {
        var roles = new[] { AppRoles.AdminRestaurante };

        var allowed = RestaurantPortalAccess.CanAccessPath(roles, accessMode, path);

        Assert.Equal(expected, allowed);
    }

    [Fact]
    public void TryNormalizeWhatsAppTarget_PreservesKnownQuery()
    {
        var valid = RestaurantPortalAccess.TryNormalizeWhatsAppTarget(
            "/Orders?status=PendingReview",
            out var normalized);

        Assert.True(valid);
        Assert.Equal("/Orders?status=PendingReview", normalized);
    }
}
