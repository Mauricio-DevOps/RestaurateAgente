namespace Restaurantes.Web.Auth;

public static class AppRoles
{
    public const string Master = "MASTER";
    public const string AdminRestaurante = "ADMIN_RESTAURANTE";
    public const string Garcom = "GARCOM";

    public static readonly string[] All = [Master, AdminRestaurante, Garcom];
}
