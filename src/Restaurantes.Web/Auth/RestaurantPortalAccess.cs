using System.Security.Claims;
using Microsoft.AspNetCore.WebUtilities;
using Restaurantes.Web.Models;

namespace Restaurantes.Web.Auth;

public enum RestaurantPortalArea
{
    Restaurante,
    WhatsApp
}

public static class RestaurantPortalAccess
{
    public const string AccessModeClaimType = "restaurant_access_mode";

    private static readonly HashSet<string> AllowedWhatsAppTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Dashboard",
        "/Products",
        "/Orders",
        "/Agent"
    };

    public static RestaurantAccessMode Parse(string? value)
    {
        return Enum.TryParse<RestaurantAccessMode>(value, ignoreCase: true, out var accessMode)
            ? accessMode
            : RestaurantAccessMode.Ambos;
    }

    public static RestaurantAccessMode GetAccessMode(ClaimsPrincipal user)
    {
        return Parse(user.FindFirst(AccessModeClaimType)?.Value);
    }

    public static bool HasRestaurantAccess(ClaimsPrincipal user)
    {
        return HasRestaurantAccess(GetAccessMode(user));
    }

    public static bool HasRestaurantAccess(RestaurantAccessMode accessMode)
    {
        return accessMode != RestaurantAccessMode.SoWhatsApp;
    }

    public static bool HasWhatsAppAccess(ClaimsPrincipal user)
    {
        return HasWhatsAppAccess(GetAccessMode(user));
    }

    public static bool HasWhatsAppAccess(RestaurantAccessMode accessMode)
    {
        return accessMode != RestaurantAccessMode.SoRestaurante;
    }

    public static string ToDisplayLabel(RestaurantAccessMode accessMode)
    {
        return accessMode switch
        {
            RestaurantAccessMode.SoRestaurante => "So restaurante",
            RestaurantAccessMode.SoWhatsApp => "So WhatsApp",
            _ => "Ambos"
        };
    }

    public static string GetPreferredLocalPath(ClaimsPrincipal user)
    {
        if (user.IsInRole(AppRoles.Master))
        {
            return "/master";
        }

        if (user.IsInRole(AppRoles.AdminRestaurante))
        {
            return GetPreferredAdminPath(GetAccessMode(user));
        }

        if (user.IsInRole(AppRoles.Garcom))
        {
            return "/garcom";
        }

        return "/login";
    }

    public static string GetPreferredAdminPath(RestaurantAccessMode accessMode)
    {
        return HasRestaurantAccess(accessMode)
            ? "/restaurante"
            : BuildWhatsAppBridgePath("/Dashboard");
    }

    public static string BuildWhatsAppBridgePath(string? target)
    {
        var normalizedTarget = NormalizeWhatsAppTargetOrDefault(target);
        return $"/sso/whatsapp?target={Uri.EscapeDataString(normalizedTarget)}";
    }

    public static string NormalizeWhatsAppTargetOrDefault(string? target)
    {
        return TryNormalizeWhatsAppTarget(target, out var normalizedTarget)
            ? normalizedTarget
            : "/Dashboard";
    }

    public static bool TryNormalizeWhatsAppTarget(string? target, out string normalizedTarget)
    {
        normalizedTarget = string.Empty;
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        var trimmed = RemoveFragment(target.Trim());
        if (!trimmed.StartsWith("/", StringComparison.Ordinal) ||
            trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        var queryIndex = trimmed.IndexOf('?');
        var path = queryIndex >= 0 ? trimmed[..queryIndex] : trimmed;
        var query = queryIndex >= 0 ? trimmed[(queryIndex + 1)..] : string.Empty;
        if (!AllowedWhatsAppTargets.Contains(path))
        {
            return false;
        }

        normalizedTarget = string.IsNullOrEmpty(query)
            ? path
            : $"{path}?{query}";
        return true;
    }

    public static bool TryGetWhatsAppTargetFromLocalPath(string? localPath, out string normalizedTarget)
    {
        normalizedTarget = string.Empty;
        if (string.IsNullOrWhiteSpace(localPath))
        {
            return false;
        }

        var trimmed = RemoveFragment(localPath.Trim());
        if (!trimmed.StartsWith("/", StringComparison.Ordinal) ||
            trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        var queryIndex = trimmed.IndexOf('?');
        var path = queryIndex >= 0 ? trimmed[..queryIndex] : trimmed;
        var query = queryIndex >= 0 ? trimmed[(queryIndex + 1)..] : string.Empty;

        if (path.Equals("/sso/whatsapp", StringComparison.OrdinalIgnoreCase))
        {
            var values = QueryHelpers.ParseQuery(query);
            return TryNormalizeWhatsAppTarget(values["target"].ToString(), out normalizedTarget);
        }

        normalizedTarget = path.ToLowerInvariant() switch
        {
            "/restaurante/whatsapp/dashboard" => "/Dashboard",
            "/restaurante/whatsapp/products" => "/Products",
            "/restaurante/whatsapp/orders" => "/Orders",
            "/restaurante/whatsapp/agent" => "/Agent",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(normalizedTarget);
    }

    public static bool CanAccessPath(
        IList<string> roles,
        RestaurantAccessMode accessMode,
        string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = RemoveFragment(path.Trim());
        if (!trimmed.StartsWith("/", StringComparison.Ordinal) ||
            trimmed.StartsWith("//", StringComparison.Ordinal) ||
            trimmed.Equals("/login", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var queryIndex = trimmed.IndexOf('?');
        var localPath = queryIndex >= 0 ? trimmed[..queryIndex] : trimmed;

        if (localPath.StartsWith("/master", StringComparison.OrdinalIgnoreCase))
        {
            return roles.Contains(AppRoles.Master);
        }

        if (localPath.StartsWith("/garcom", StringComparison.OrdinalIgnoreCase))
        {
            return roles.Contains(AppRoles.Garcom);
        }

        if (localPath.StartsWith("/restaurante/whatsapp", StringComparison.OrdinalIgnoreCase) ||
            localPath.StartsWith("/sso/whatsapp", StringComparison.OrdinalIgnoreCase))
        {
            return roles.Contains(AppRoles.AdminRestaurante) &&
                HasWhatsAppAccess(accessMode) &&
                TryGetWhatsAppTargetFromLocalPath(trimmed, out _);
        }

        if (localPath.StartsWith("/restaurante/operacao", StringComparison.OrdinalIgnoreCase))
        {
            return roles.Contains(AppRoles.AdminRestaurante);
        }

        if (localPath.StartsWith("/restaurante", StringComparison.OrdinalIgnoreCase))
        {
            return roles.Contains(AppRoles.AdminRestaurante) &&
                HasRestaurantAccess(accessMode);
        }

        return true;
    }

    private static string RemoveFragment(string value)
    {
        var fragmentIndex = value.IndexOf('#');
        return fragmentIndex >= 0 ? value[..fragmentIndex] : value;
    }
}
