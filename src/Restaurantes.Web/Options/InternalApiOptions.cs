namespace Restaurantes.Web.Options;

public sealed class InternalApiOptions
{
    public const string SectionName = "InternalApi";

    public string BaseUrl { get; set; } = "http://localhost:5253";

    public string ServiceKey { get; set; } = "";
}
