using System;
using System.Globalization;

namespace FirewallControl.App.Services;

/// <summary>
/// Shared Guarani / quantity numeric parsers for both Classic (Sufer POS) and Renovado (C#) POS views.
/// </summary>
public static class PosNumericParser
{
    /// <summary>
    /// Parses a Guaraní integer amount where commas (e.g. "10,000") or dots (e.g. "10.000")
    /// are thousands separators from StringFormat=N0, preventing "10,000" from becoming 10.
    /// </summary>
    public static bool TryParseGuaraniAmount(string? rawText, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(rawText))
            return false;

        string s = rawText.Trim();
        if (s.EndsWith(".00", StringComparison.Ordinal) || s.EndsWith(",00", StringComparison.Ordinal))
        {
            s = s[..^3];
        }

        s = s.Replace(",", "").Replace(".", "").Replace(" ", "");
        return decimal.TryParse(s, NumberStyles.Integer | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryParseQuantity(string? rawText, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(rawText))
            return false;

        string s = rawText.Trim().Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}
