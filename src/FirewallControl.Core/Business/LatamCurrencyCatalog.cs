using System.Globalization;
using FirewallControl.Core.Entities;

namespace FirewallControl.Core.Business;

public sealed record LatamCurrencyDefinition(
    string IsoCode,
    string Name,
    string PluralName,
    string Symbol,
    string Country,
    string FlagEmoji,
    int Decimals,
    decimal DefaultRateToUsd,
    string CultureName)
{
    public string DisplayLabel => $"{FlagEmoji} {IsoCode} — {Name} ({Symbol}) · {Country}";
}

/// <summary>
/// Catálogo completo de monedas de Latinoamérica (20 monedas LATAM + referencia EUR)
/// con formateo regional, conversión de referencia y generación de entidades iniciales de fábrica.
/// </summary>
public static class LatamCurrencyCatalog
{
    private static readonly List<LatamCurrencyDefinition> Catalog = new()
    {
        new("PYG", "Guaraní Paraguayo", "GUARANIES", "Gs.", "Paraguay", "🇵🇾", 0, 7750m, "es-PY"),
        new("ARS", "Peso Argentino", "PESOS ARGENTINOS", "$", "Argentina", "🇦🇷", 2, 1020m, "es-AR"),
        new("BOB", "Boliviano", "BOLIVIANOS", "Bs", "Bolivia", "🇧🇴", 2, 6.91m, "es-BO"),
        new("BRL", "Real Brasileño", "REALES", "R$", "Brasil", "🇧🇷", 2, 5.80m, "pt-BR"),
        new("CLP", "Peso Chileno", "PESOS CHILENOS", "$", "Chile", "🇨🇱", 0, 965m, "es-CL"),
        new("COP", "Peso Colombiano", "PESOS COLOMBIANOS", "$", "Colombia", "🇨🇴", 0, 4350m, "es-CO"),
        new("CRC", "Colón Costarricense", "COLONES", "₡", "Costa Rica", "🇨🇷", 2, 512m, "es-CR"),
        new("CUP", "Peso Cubano", "PESOS CUBANOS", "$", "Cuba", "🇨🇺", 2, 24m, "es-CU"),
        new("DOP", "Peso Dominicano", "PESOS DOMINICANOS", "RD$", "Rep. Dominicana", "🇩🇴", 2, 60.2m, "es-DO"),
        new("USD", "Dólar Estadounidense", "DOLARES AMERICANOS", "US$", "Ecuador / El Salvador / Panamá / Regional", "🇺🇸", 2, 1m, "en-US"),
        new("GTQ", "Quetzal Guatemalteco", "QUETZALES", "Q", "Guatemala", "🇬🇹", 2, 7.72m, "es-GT"),
        new("HNL", "Lempira Hondureño", "LEMPIRAS", "L", "Honduras", "🇭🇳", 2, 25.2m, "es-HN"),
        new("HTG", "Gourde Haitiano", "GOURDES", "G", "Haití", "🇭🇹", 2, 131m, "fr-HT"),
        new("MXN", "Peso Mexicano", "PESOS MEXICANOS", "$", "México", "🇲🇽", 2, 20.3m, "es-MX"),
        new("NIO", "Córdoba Nicaragüense", "CORDOBAS", "C$", "Nicaragua", "🇳🇮", 2, 36.8m, "es-NI"),
        new("PAB", "Balboa Panameño", "BALBOAS", "B/.", "Panamá", "🇵🇦", 2, 1m, "es-PA"),
        new("PEN", "Sol Peruano", "SOLES", "S/", "Perú", "🇵🇪", 2, 3.76m, "es-PE"),
        new("UYU", "Peso Uruguayo", "PESOS URUGUAYOS", "$U", "Uruguay", "🇺🇾", 2, 42.5m, "es-UY"),
        new("VES", "Bolívar Venezolano", "BOLIVARES", "Bs.", "Venezuela", "🇻🇪", 2, 46.5m, "es-VE"),
        new("BZD", "Dólar Beliceño", "DOLARES BELICEÑOS", "BZ$", "Belice", "🇧🇿", 2, 2m, "en-BZ"),
        new("EUR", "Euro", "EUROS", "€", "Referencia Internacional", "🇪🇺", 2, 0.94m, "es-ES")
    };

    public static IReadOnlyList<LatamCurrencyDefinition> All => Catalog;

    public static LatamCurrencyDefinition GetByCode(string? isoCode)
    {
        if (!string.IsNullOrWhiteSpace(isoCode))
        {
            string normalized = isoCode.Trim().ToUpperInvariant();
            foreach (var item in Catalog)
            {
                if (item.IsoCode == normalized)
                    return item;
            }
        }
        return Catalog[0];
    }

    public static string FormatAmount(decimal amount, string? isoCode = null)
    {
        var cur = GetByCode(isoCode);
        string numFormat = cur.Decimals == 0 ? "#,##0" : "#,##0.00";
        decimal rounded = Math.Round(amount, cur.Decimals, MidpointRounding.AwayFromZero);
        string formatted = rounded.ToString(numFormat, CultureInfo.InvariantCulture);

        return cur.IsoCode == "PYG"
            ? $"{formatted} Gs."
            : $"{cur.Symbol} {formatted}";
    }

    public static decimal Convert(decimal amount, string? fromIsoCode, string? toIsoCode)
    {
        if (amount == 0m)
            return 0m;

        var from = GetByCode(fromIsoCode);
        var to = GetByCode(toIsoCode);
        if (from.IsoCode == to.IsoCode)
            return Math.Round(amount, to.Decimals, MidpointRounding.AwayFromZero);

        decimal inUsd = amount / (from.DefaultRateToUsd > 0m ? from.DefaultRateToUsd : 1m);
        decimal converted = inUsd * (to.DefaultRateToUsd > 0m ? to.DefaultRateToUsd : 1m);
        return Math.Round(converted, Math.Max(to.Decimals, 2), MidpointRounding.AwayFromZero);
    }

    public static List<CurrencyEntity> ToCurrencyEntities(string? baseIsoCode = "PYG")
    {
        var baseCur = GetByCode(baseIsoCode);
        var list = new List<CurrencyEntity>(Catalog.Count);

        for (int i = 0; i < Catalog.Count; i++)
        {
            var c = Catalog[i];
            decimal unitValueInBase = c.IsoCode == baseCur.IsoCode
                ? 1m
                : Math.Round((baseCur.DefaultRateToUsd / c.DefaultRateToUsd), baseCur.Decimals == 0 ? 2 : 4, MidpointRounding.AwayFromZero);

            list.Add(new CurrencyEntity
            {
                IdMoneda = i + 1,
                Descripcion = $"{c.IsoCode} - {c.Name} ({c.Country})",
                Compra = unitValueInBase,
                Venta = c.IsoCode == baseCur.IsoCode ? 1m : Math.Round(unitValueInBase * 1.01m, baseCur.Decimals == 0 ? 2 : 4, MidpointRounding.AwayFromZero),
                Decimales = c.Decimals,
                Tronco = c.IsoCode == baseCur.IsoCode ? 1 : 0,
                Operacion = "*",
                Tipo = i == 0 ? 1 : 0
            });
        }

        return list;
    }
}
