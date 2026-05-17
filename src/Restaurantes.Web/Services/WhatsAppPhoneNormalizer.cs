namespace Restaurantes.Web.Services;

public static class WhatsAppPhoneNormalizer
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Informe o telefone de WhatsApp.");
        }

        var phone = value.Trim();
        if (phone.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase))
        {
            phone = phone["whatsapp:".Length..].Trim();
        }

        var hasCountryPrefix = phone.StartsWith('+');
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            throw new InvalidOperationException("Informe um telefone de WhatsApp valido.");
        }

        if (!hasCountryPrefix)
        {
            digits = digits.StartsWith("55", StringComparison.Ordinal) && digits.Length is 12 or 13
                ? digits
                : $"55{digits}";
        }

        if (digits.Length is < 10 or > 15)
        {
            throw new InvalidOperationException("Informe um telefone de WhatsApp valido.");
        }

        return $"whatsapp:+{digits}";
    }
}
