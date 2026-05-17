using Microsoft.AspNetCore.Mvc;
using Restaurantes.Web.Auth;

namespace Restaurantes.Web.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Redirect("/login");
        }

        if (User.IsInRole(AppRoles.Master))
        {
            return Redirect("/master");
        }
        if (User.IsInRole(AppRoles.AdminRestaurante))
        {
            return Redirect(RestaurantPortalAccess.GetPreferredAdminPath(RestaurantPortalAccess.GetAccessMode(User)));
        }
        if (User.IsInRole(AppRoles.Garcom))
        {
            return Redirect("/garcom");
        }

        return Redirect("/login");
    }
}
