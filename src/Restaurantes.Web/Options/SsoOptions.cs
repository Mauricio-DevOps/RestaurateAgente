namespace Restaurantes.Web.Options;

public sealed class SsoOptions
{
    public const string SectionName = "Sso";

    public string SigningKey { get; set; } = "";

    public int TokenLifetimeMinutes { get; set; } = 5;
}
