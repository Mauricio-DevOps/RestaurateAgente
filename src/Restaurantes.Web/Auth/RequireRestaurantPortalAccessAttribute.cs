using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Restaurantes.Web.Auth;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireRestaurantPortalAccessAttribute : ActionFilterAttribute
{
    private readonly RestaurantPortalArea _area;

    public RequireRestaurantPortalAccessAttribute(RestaurantPortalArea area)
    {
        _area = area;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var hasAccess = _area switch
        {
            RestaurantPortalArea.Restaurante => RestaurantPortalAccess.HasRestaurantAccess(user),
            RestaurantPortalArea.WhatsApp => RestaurantPortalAccess.HasWhatsAppAccess(user),
            _ => false
        };

        if (hasAccess)
        {
            return;
        }

        context.Result = new RedirectResult(RestaurantPortalAccess.GetPreferredLocalPath(user));
    }
}
