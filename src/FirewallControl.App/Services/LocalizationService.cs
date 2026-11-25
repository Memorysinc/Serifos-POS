using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace FirewallControl.App.Services;

public sealed record LanguageOption(string Code, string NativeLabel, string FlagEmoji)
{
    public string DisplayLabel => $"{FlagEmoji} {NativeLabel} ({Code.ToUpperInvariant()})";
}

/// <summary>
/// Motor multi-idioma en caliente (Español / Português / English) basado en ResourceDictionaries XAML.
/// </summary>
public static class LocalizationService
{
    public static event Action? LanguageChanged;

    public static string CurrentLanguage { get; private set; } = "es";

    public static IReadOnlyList<LanguageOption> Languages { get; } = new List<LanguageOption>
    {
        new("es", "Español", "🇪🇸"),
        new("pt", "Português", "🇧🇷"),
        new("en", "English", "🇺🇸")
    };

    public static string NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "es";

        string trimmed = code.Trim().ToLowerInvariant();
        if (trimmed.StartsWith("pt", StringComparison.Ordinal))
            return "pt";
        if (trimmed.StartsWith("en", StringComparison.Ordinal))
            return "en";
        return "es";
    }

    public static void ApplyLanguage(string? languageCode)
    {
        string normalized = NormalizeCode(languageCode);
        CurrentLanguage = normalized;

        var app = Application.Current;
        if (app == null)
        {
            LanguageChanged?.Invoke();
            return;
        }

        var merged = app.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d =>
            d.Source != null &&
            d.Source.OriginalString.Contains("Themes/Strings.", StringComparison.OrdinalIgnoreCase));

        var targetUri = new Uri($"pack://application:,,,/Sefiros.App;component/Themes/Strings.{normalized}.xaml", UriKind.Absolute);
        var newDict = new ResourceDictionary { Source = targetUri };

        if (existing != null)
        {
            int idx = merged.IndexOf(existing);
            merged[idx] = newDict;
        }
        else
        {
            merged.Add(newDict);
        }

        LanguageChanged?.Invoke();
    }

    public static string Get(string key, string fallback = "")
    {
        if (Application.Current?.TryFindResource(key) is string val && !string.IsNullOrWhiteSpace(val))
            return val;
        return fallback;
    }
}
