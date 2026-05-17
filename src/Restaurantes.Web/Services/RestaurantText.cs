using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Restaurantes.Web.Services;

public static partial class RestaurantText
{
    public static string NormalizeWaiterName(string value) => CollapseWhitespace(value);

    public static string NormalizeTableNumber(string value) => CollapseWhitespace(value);

    public static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        var slug = NonSlugCharacterRegex()
            .Replace(builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant(), "-")
            .Trim('-');

        return string.IsNullOrWhiteSpace(slug) ? "restaurante" : DuplicateDashRegex().Replace(slug, "-");
    }

    public static string FormatPrice(int cents) => (cents / 100m).ToString("C", new CultureInfo("pt-BR"));

    public static int ParsePriceToCents(string value)
    {
        var normalized = value.Trim().Replace("R$", "", StringComparison.OrdinalIgnoreCase);
        if (!decimal.TryParse(normalized, NumberStyles.Number, new CultureInfo("pt-BR"), out var amount) &&
            !decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            throw new InvalidOperationException("Informe um preço válido.");
        }

        return (int)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
    }

    public static void EnsureNoDuplicateWaiters(IEnumerable<string> names)
    {
        EnsureNoDuplicates(names.Select(NormalizeWaiterName), "Já existe um garçom com esse nome.");
    }

    public static void EnsureNoDuplicateTables(IEnumerable<string> tableNumbers)
    {
        EnsureNoDuplicates(tableNumbers.Select(NormalizeTableNumber), "Já existe uma mesa com esse número.");
    }

    private static string CollapseWhitespace(string value) => WhitespaceRegex().Replace(value.Trim(), " ");

    private static void EnsureNoDuplicates(IEnumerable<string> values, string message)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (!seen.Add(value))
            {
                throw new InvalidOperationException(message);
            }
        }
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacterRegex();

    [GeneratedRegex("-+")]
    private static partial Regex DuplicateDashRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
