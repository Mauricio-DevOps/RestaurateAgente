namespace Restaurantes.Web.Options;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    public string PublicBaseUrl { get; set; } = "";
    public bool UseSandboxCheckout { get; set; }
}
