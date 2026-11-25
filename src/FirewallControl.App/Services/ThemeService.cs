using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace FirewallControl.App.Services;

public sealed record AtelierPalette(
    string Key,
    string Label,
    string SwatchHex,
    string PrimaryHex,
    string PrimaryLightHex,
    string PrimaryDarkHex,
    string ScreenBgHex,
    string SurfaceAltHex,
    string SurfaceAccentHex,
    string BorderHex,
    string InkHex,
    string InkSoftHex,
    string ButtonPrimaryBgHex,
    string KpiBgHex,
    string KpiValueHex);

/// <summary>
/// Centralized theme engine replicating the 7 Gissel Modas / Dressesfy palettes
/// plus the independent Color Palette mode ("Moderna" C# vs "ClasicaFox" Visual FoxPro / Sufer POS).
/// Updates Application.Current.Resources dynamically so all open windows refresh immediately.
/// </summary>
public static class ThemeService
{
    public static event Action? ThemeChanged;

    public static string CurrentThemeKey { get; private set; } = "neutro";
    public static bool CurrentIsDark { get; private set; }
    public static string CurrentColorPalette { get; private set; } = "Moderna";
    public static bool IsClassicFoxPalette => string.Equals(CurrentColorPalette, "ClasicaFox", StringComparison.OrdinalIgnoreCase);
    public static bool IsSefirosTheme => string.Equals(CurrentThemeKey, "sefiros", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<AtelierPalette> Palettes { get; } = new List<AtelierPalette>
    {
        new("neutro", "Neutro Carbón", "#1F1C1A",
            "#6F6A64", "#A7A19A", "#1F1C1A",
            "#F8F7F5", "#EFEDEA", "#E3DFDA", "#DED9D3",
            "#1F1C1A", "#5C5652", "#1F1C1A", "#1F1C1A", "#EAE6E0"),

        new("sefiros", "Tema Sefiros", "#2563EB",
            "#2563EB", "#14B8A6", "#1E293B",
            "#F7F8FA", "#F1F3F6", "#E8ECEF", "#D5DAE2",
            "#181C24", "#525B6B", "#2563EB", "#181C24", "#F8FAFC"),

        new("ocean", "Océano Azul", "#347F8F",
            "#347F8F", "#8CBCC8", "#255F6D",
            "#F4F8F9", "#E7F1F3", "#D5E6EA", "#C8DCE1",
            "#162A2F", "#4A656C", "#255F6D", "#255F6D", "#D5E6EA"),

        new("emerald", "Verde Esmeralda", "#2F8F6B",
            "#2F8F6B", "#86BEA9", "#216B50",
            "#F4F9F6", "#E7F2ED", "#D4E8DF", "#C5DED3",
            "#152B23", "#47665A", "#216B50", "#216B50", "#D4E8DF"),

        new("rose", "Rosa Palo", "#B76E79",
            "#B76E79", "#DDB0B8", "#8F4B56",
            "#FBF7F8", "#F6E7EA", "#EDD5DA", "#E4C5CB",
            "#2D1A1D", "#6B4C51", "#8F4B56", "#8F4B56", "#F6E7EA"),

        new("indigo", "Azul Índigo", "#63608F",
            "#63608F", "#AAA7CC", "#4D496F",
            "#F7F7FB", "#ECEBF5", "#DCDBE9", "#CECCE0",
            "#1E1D2B", "#56546E", "#4D496F", "#4D496F", "#DCDBE9"),

        new("sand", "Arena Suave", "#A8895A",
            "#A8895A", "#D2BB91", "#80683F",
            "#FAF7F2", "#F4EEE4", "#EADFC9", "#DFD2B8",
            "#262017", "#635644", "#80683F", "#80683F", "#F4EEE4"),

        new("atelier", "Dorado Atelier", "#9A7B4F",
            "#9A7B4F", "#C4A574", "#7C623F",
            "#FAF6F2", "#F3EBE4", "#E8DACD", "#DCCAB8",
            "#261F18", "#635446", "#7C623F", "#7C623F", "#F3EBE4")
    };

    public static AtelierPalette GetPalette(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            foreach (var p in Palettes)
            {
                if (string.Equals(p.Key, key.Trim(), StringComparison.OrdinalIgnoreCase))
                    return p;
            }
        }
        return Palettes[0];
    }

    public static Brush CreatePaletteSwatchBrush(AtelierPalette palette)
    {
        if (string.Equals(palette.Key, "sefiros", StringComparison.OrdinalIgnoreCase))
        {
            var rainbow = new LinearGradientBrush
            {
                StartPoint = new Point(0, 1),
                EndPoint = new Point(1, 0)
            };
            rainbow.GradientStops.Add(new GradientStop(ParseColor("#2563EB"), 0.0));
            rainbow.GradientStops.Add(new GradientStop(ParseColor("#0EA5E9"), 0.30));
            rainbow.GradientStops.Add(new GradientStop(ParseColor("#22C55E"), 0.55));
            rainbow.GradientStops.Add(new GradientStop(ParseColor("#F59E0B"), 0.78));
            rainbow.GradientStops.Add(new GradientStop(ParseColor("#F43F5E"), 1.0));
            rainbow.Freeze();
            return rainbow;
        }

        var solid = new SolidColorBrush(ParseColor(palette.SwatchHex));
        solid.Freeze();
        return solid;
    }

    public static void ApplyTheme(string? themeKey, bool isDark, string? colorPalette = null)
    {
        var p = GetPalette(themeKey);
        CurrentThemeKey = p.Key;
        CurrentIsDark = isDark;
        if (colorPalette != null)
        {
            CurrentColorPalette = string.Equals(colorPalette.Trim(), "ClasicaFox", StringComparison.OrdinalIgnoreCase)
                ? "ClasicaFox"
                : "Moderna";
        }

        var app = Application.Current;
        if (app == null)
            return;

        var res = app.Resources;

        Color primary = ParseColor(p.PrimaryHex);
        Color primaryLight = ParseColor(p.PrimaryLightHex);
        Color primaryDark = ParseColor(p.PrimaryDarkHex);

        Color screenBg;
        Color cardBg;
        Color surfaceAlt;
        Color surfaceAccent;
        Color border;
        Color divider;
        Color ink;
        Color inkSoft;
        Color textMuted;
        Color textInverse;
        Color btnPrimaryBg;
        Color btnPrimaryHover;
        Color btnPrimaryPressed;
        Color btnSecondaryBg;
        Color inputBg;
        Color inputReadOnlyBg;
        Color inputBorder;
        Color kpiBg;
        Color kpiText;
        Color kpiValue;
        Color heroStart;
        Color heroMid;
        Color heroEnd;
        Color gridAltRow;
        Color gridSelectedBg;
        Color gridSelectedFg;
        Color gridHoverBg;
        Color successBg;
        Color successText;
        Color successBorder;
        Color successSolidBg;
        Color successSolidHoverBg;
        Color warningBg;
        Color warningText;
        Color warningBorder;
        Color dangerBg;
        Color dangerText;
        Color dangerBorder;
        Color infoBg;
        Color infoText;
        Color infoBorder;
        Color shadowColor;

        bool sefiros = string.Equals(p.Key, "sefiros", StringComparison.OrdinalIgnoreCase);

        if (!isDark)
        {
            screenBg = ParseColor(p.ScreenBgHex);
            cardBg = Colors.White;
            surfaceAlt = ParseColor(p.SurfaceAltHex);
            surfaceAccent = ParseColor(p.SurfaceAccentHex);
            border = ParseColor(p.BorderHex);
            divider = Blend(border, cardBg, 0.25);
            ink = ParseColor(p.InkHex);
            inkSoft = ParseColor(p.InkSoftHex);
            textMuted = Blend(inkSoft, cardBg, 0.30);
            textInverse = Color.FromRgb(250, 248, 245);

            btnPrimaryBg = ParseColor(p.ButtonPrimaryBgHex);
            btnPrimaryHover = primary;
            btnPrimaryPressed = Darken(btnPrimaryBg, 0.14);
            btnSecondaryBg = cardBg;

            inputBg = sefiros ? Colors.White : Color.FromRgb(252, 251, 249);
            inputReadOnlyBg = surfaceAlt;
            inputBorder = border;

            kpiBg = ParseColor(p.KpiBgHex);
            kpiText = Color.FromRgb(248, 244, 239);
            kpiValue = ParseColor(p.KpiValueHex);

            heroStart = Colors.White;
            heroMid = surfaceAlt;
            heroEnd = surfaceAccent;

            gridAltRow = Blend(surfaceAlt, Colors.White, 0.55);
            gridSelectedBg = surfaceAccent;
            gridSelectedFg = ink;
            gridHoverBg = surfaceAlt;

            successBg = Color.FromRgb(236, 253, 245);
            successText = sefiros ? ParseColor("#15803D") : Color.FromRgb(21, 128, 61);
            successBorder = Color.FromRgb(167, 243, 208);
            successSolidBg = Color.FromRgb(22, 163, 74);
            successSolidHoverBg = Color.FromRgb(21, 128, 61);

            warningBg = Color.FromRgb(255, 251, 235);
            warningText = ParseColor("#B45309");
            warningBorder = Color.FromRgb(253, 230, 138);

            dangerBg = sefiros ? ParseColor("#FFF1F2") : Color.FromRgb(254, 242, 242);
            dangerText = sefiros ? ParseColor("#E11D48") : Color.FromRgb(220, 38, 38);
            dangerBorder = sefiros ? ParseColor("#FECDD3") : Color.FromRgb(254, 202, 202);

            infoBg = Color.FromRgb(239, 246, 255);
            infoText = sefiros ? ParseColor("#1D4ED8") : Color.FromRgb(29, 78, 216);
            infoBorder = Color.FromRgb(191, 219, 254);

            shadowColor = ink;
        }
        else if (sefiros)
        {
            // Tema Sefiros Obsidiana-Grafito neutro oscuro (sin exceso azul)
            screenBg = ParseColor("#111318");
            cardBg = ParseColor("#181B22");
            surfaceAlt = ParseColor("#21252F");
            surfaceAccent = ParseColor("#292F3D");
            border = ParseColor("#323846");
            divider = ParseColor("#262B37");
            ink = ParseColor("#F3F4F6");
            inkSoft = ParseColor("#9CA3AF");
            textMuted = ParseColor("#6B7280");
            textInverse = ParseColor("#F8FAFC");

            btnPrimaryBg = ParseColor("#2563EB");
            btnPrimaryHover = ParseColor("#3B82F6");
            btnPrimaryPressed = ParseColor("#1D4ED8");
            btnSecondaryBg = cardBg;

            inputBg = ParseColor("#14171F");
            inputReadOnlyBg = ParseColor("#1D212B");
            inputBorder = ParseColor("#3B4254");

            kpiBg = ParseColor("#1A1E28");
            kpiText = ParseColor("#E5E7EB");
            kpiValue = ParseColor("#F8FAFC");

            heroStart = ParseColor("#181B22");
            heroMid = ParseColor("#1C2029");
            heroEnd = ParseColor("#212630");

            gridAltRow = ParseColor("#1B1F28");
            gridSelectedBg = ParseColor("#2D3546");
            gridSelectedFg = Colors.White;
            gridHoverBg = surfaceAlt;

            successBg = ParseColor("#0F2E22");
            successText = ParseColor("#34D399");
            successBorder = ParseColor("#047857");
            successSolidBg = Color.FromRgb(22, 163, 74);
            successSolidHoverBg = Color.FromRgb(34, 197, 94);

            warningBg = ParseColor("#3A250C");
            warningText = ParseColor("#FBBF24");
            warningBorder = ParseColor("#B45309");

            dangerBg = ParseColor("#3F1522");
            dangerText = ParseColor("#FB7185");
            dangerBorder = ParseColor("#9F1239");

            infoBg = ParseColor("#14264C");
            infoText = ParseColor("#60A5FA");
            infoBorder = ParseColor("#1D4ED8");

            shadowColor = Colors.Black;
            primary = ParseColor("#38BDF8");
        }
        else
        {
            // Warm dark atelier theme tailored to the chosen accent
            screenBg = Color.FromRgb(22, 20, 18);
            cardBg = Color.FromRgb(32, 29, 27);
            surfaceAlt = Color.FromRgb(43, 39, 36);
            surfaceAccent = Blend(Color.FromRgb(54, 49, 45), primary, 0.22);
            border = Color.FromRgb(64, 58, 53);
            divider = Color.FromRgb(52, 47, 43);
            ink = Color.FromRgb(244, 240, 235);
            inkSoft = Color.FromRgb(184, 176, 168);
            textMuted = Color.FromRgb(140, 132, 125);
            textInverse = Color.FromRgb(250, 248, 245);

            Color accentActive = p.Key == "neutro" ? Color.FromRgb(185, 177, 167) : primaryLight;
            btnPrimaryBg = p.Key == "neutro" ? Color.FromRgb(74, 69, 64) : primary;
            btnPrimaryHover = p.Key == "neutro" ? Color.FromRgb(94, 88, 82) : Lighten(primary, 0.12);
            btnPrimaryPressed = Darken(btnPrimaryBg, 0.15);
            btnSecondaryBg = cardBg;

            inputBg = Color.FromRgb(26, 24, 22);
            inputReadOnlyBg = Color.FromRgb(38, 35, 32);
            inputBorder = Color.FromRgb(76, 69, 63);

            kpiBg = Blend(Color.FromRgb(36, 33, 30), primaryDark, 0.45);
            kpiText = Color.FromRgb(230, 224, 217);
            kpiValue = accentActive;

            heroStart = Color.FromRgb(34, 31, 29);
            heroMid = surfaceAlt;
            heroEnd = Blend(surfaceAlt, primary, 0.20);

            gridAltRow = Color.FromRgb(37, 34, 31);
            gridSelectedBg = Blend(surfaceAlt, primary, 0.35);
            gridSelectedFg = Colors.White;
            gridHoverBg = surfaceAlt;

            successBg = Color.FromRgb(16, 44, 32);
            successText = Color.FromRgb(74, 222, 128);
            successBorder = Color.FromRgb(22, 101, 52);
            successSolidBg = Color.FromRgb(22, 163, 74);
            successSolidHoverBg = Color.FromRgb(34, 197, 94);

            warningBg = Color.FromRgb(58, 42, 18);
            warningText = Color.FromRgb(251, 191, 36);
            warningBorder = Color.FromRgb(146, 64, 14);

            dangerBg = Color.FromRgb(64, 25, 27);
            dangerText = Color.FromRgb(248, 113, 113);
            dangerBorder = Color.FromRgb(153, 27, 27);

            infoBg = Color.FromRgb(21, 36, 66);
            infoText = Color.FromRgb(96, 165, 250);
            infoBorder = Color.FromRgb(30, 64, 175);

            shadowColor = Colors.Black;
            primary = accentActive;
        }

        SetColorAndBrush(res, "Primary", primary);
        SetColorAndBrush(res, "PrimaryLight", primaryLight);
        SetColorAndBrush(res, "PrimaryDark", primaryDark);
        SetColorAndBrush(res, "ScreenBg", screenBg);
        SetColorAndBrush(res, "CardBg", cardBg);
        SetColorAndBrush(res, "SurfaceAlt", surfaceAlt);
        SetColorAndBrush(res, "SurfaceAccent", surfaceAccent);
        SetColorAndBrush(res, "CardBorder", border);
        SetColorAndBrush(res, "Divider", divider);
        SetColorAndBrush(res, "TextPrimary", ink);
        SetColorAndBrush(res, "TextSecondary", inkSoft);
        SetColorAndBrush(res, "TextMuted", textMuted);
        SetColorAndBrush(res, "TextInverse", textInverse);

        SetColorAndBrush(res, "ButtonPrimaryBg", btnPrimaryBg);
        SetColorAndBrush(res, "ButtonPrimaryHoverBg", btnPrimaryHover);
        SetColorAndBrush(res, "ButtonPrimaryPressedBg", btnPrimaryPressed);
        if (sefiros)
        {
            SetHorizontalGradient(res, "ButtonPrimaryBgBrush", ("#2563EB", 0.0), ("#0E9F6E", 0.34), ("#D97706", 0.68), ("#E11D48", 1.0));
            SetHorizontalGradient(res, "ButtonPrimaryHoverBgBrush", ("#3B82F6", 0.0), ("#10B981", 0.34), ("#EA8C00", 0.68), ("#F43F5E", 1.0));
            SetHorizontalGradient(res, "ButtonPrimaryPressedBgBrush", ("#1D4ED8", 0.0), ("#047857", 0.34), ("#B45309", 0.68), ("#BE123C", 1.0));
        }
        SetColorAndBrush(res, "ButtonSecondaryBg", btnSecondaryBg);
        SetColorAndBrush(res, "ButtonSecondaryHoverBg", surfaceAlt);
        SetColorAndBrush(res, "ButtonSecondaryPressedBg", surfaceAccent);

        SetColorAndBrush(res, "InputBg", inputBg);
        SetColorAndBrush(res, "InputReadOnlyBg", inputReadOnlyBg);
        SetColorAndBrush(res, "InputBorder", inputBorder);
        SetColorAndBrush(res, "InputFocusBorder", primary);

        SetColorAndBrush(res, "KpiAccentBg", kpiBg);
        SetColorAndBrush(res, "KpiAccentText", kpiText);
        SetColorAndBrush(res, "KpiAccentValue", kpiValue);

        SetColorAndBrush(res, "DataGridAltRow", gridAltRow);
        SetColorAndBrush(res, "DataGridSelectedBg", gridSelectedBg);
        SetColorAndBrush(res, "DataGridSelectedFg", gridSelectedFg);
        SetColorAndBrush(res, "DataGridHoverBg", gridHoverBg);

        SetColorAndBrush(res, "SuccessBg", successBg);
        SetColorAndBrush(res, "SuccessText", successText);
        SetColorAndBrush(res, "SuccessBorder", successBorder);
        SetColorAndBrush(res, "SuccessSolidBg", successSolidBg);
        SetColorAndBrush(res, "SuccessSolidHoverBg", successSolidHoverBg);

        SetColorAndBrush(res, "WarningBg", warningBg);
        SetColorAndBrush(res, "WarningText", warningText);
        SetColorAndBrush(res, "WarningBorder", warningBorder);

        SetColorAndBrush(res, "DangerBg", dangerBg);
        SetColorAndBrush(res, "DangerText", dangerText);
        SetColorAndBrush(res, "DangerBorder", dangerBorder);
        SetColorAndBrush(res, "DangerSolidBg", Color.FromRgb(220, 38, 38));
        SetColorAndBrush(res, "DangerSolidHoverBg", Color.FromRgb(185, 28, 28));

        SetColorAndBrush(res, "InfoBg", infoBg);
        SetColorAndBrush(res, "InfoText", infoText);
        SetColorAndBrush(res, "InfoBorder", infoBorder);

        SetColorAndBrush(res, "ShadowColor", shadowColor);

        var heroBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        if (sefiros && !isDark)
        {
            heroBrush.GradientStops.Add(new GradientStop(ParseColor("#F0F6FF"), 0.0));
            heroBrush.GradientStops.Add(new GradientStop(ParseColor("#EDFBF4"), 0.35));
            heroBrush.GradientStops.Add(new GradientStop(ParseColor("#FFF9EB"), 0.68));
            heroBrush.GradientStops.Add(new GradientStop(ParseColor("#FFF0F4"), 1.0));
        }
        else
        {
            heroBrush.GradientStops.Add(new GradientStop(heroStart, 0.0));
            heroBrush.GradientStops.Add(new GradientStop(heroMid, 0.55));
            heroBrush.GradientStops.Add(new GradientStop(heroEnd, 1.0));
        }
        heroBrush.Freeze();
        res["HeroGradientBrush"] = heroBrush;

        ApplyPosSemanticBrushes(res, screenBg, cardBg, surfaceAlt, surfaceAccent, border, divider, ink, inkSoft, btnPrimaryBg, textInverse, inputBg, inputReadOnlyBg, inputBorder, kpiBg, kpiText, kpiValue, heroBrush, isDark);

        ThemeChanged?.Invoke();
    }

    private static void ApplyPosSemanticBrushes(
        ResourceDictionary res,
        Color screenBg, Color cardBg, Color surfaceAlt, Color surfaceAccent, Color border, Color divider,
        Color ink, Color inkSoft, Color btnPrimaryBg, Color textInverse,
        Color inputBg, Color inputReadOnlyBg, Color inputBorder,
        Color kpiBg, Color kpiText, Color kpiValue, LinearGradientBrush heroBrush, bool isDark)
    {
        if (IsSefirosTheme)
        {
            if (!isDark)
            {
                SetSolidBrush(res, "PosWindowBgBrush", "#F8F9FB");
                SetSolidBrush(res, "PosPanelBgBrush", "#FFFFFF");
                SetSolidBrush(res, "PosPanelBorderBrush", "#C5CCD8");
                SetHorizontalGradient(res, "PosTopBannerBrush", ("#DCEBFF", 0.0), ("#E0F9EE", 0.34), ("#FFF4D6", 0.68), ("#FFE4EC", 1.0));
                SetSolidBrush(res, "PosTopBannerBorderBrush", "#CBD2DE");
                SetSolidBrush(res, "PosTopBannerTitleBrush", "#1E293B");
                SetHorizontalGradient(res, "PosSectionBarBrush", ("#2563EB", 0.0), ("#0E9F6E", 0.34), ("#D97706", 0.68), ("#E11D48", 1.0));
                SetSolidBrush(res, "PosSectionBarBorderBrush", "#64748B");
                SetSolidBrush(res, "PosSectionBarTextBrush", "#FFFFFF");
                SetHorizontalGradient(res, "PosFooterBarBrush", ("#DCEBFF", 0.0), ("#E0F9EE", 0.34), ("#FFF4D6", 0.68), ("#FFE4EC", 1.0));
                SetSolidBrush(res, "PosFooterBorderBrush", "#C5CCD8");
                SetSolidBrush(res, "PosFooterTextBrush", "#1E293B");
                SetSolidBrush(res, "PosTextPrimaryBrush", "#181C24");
                SetSolidBrush(res, "PosTextSecondaryBrush", "#525B6B");
                SetSolidBrush(res, "PosSubTotalLabelBrush", "#475569");
                SetSolidBrush(res, "PosSubTotalValueBrush", "#181C24");
                SetSolidBrush(res, "PosInputBgBrush", "#FFFFFF");
                SetSolidBrush(res, "PosInputReadOnlyBgBrush", "#F2F4F7");
                SetSolidBrush(res, "PosInputBorderBrush", "#B0B8C5");
                SetSolidBrush(res, "PosAccentRedLabelBrush", "#E11D48");
                SetSolidBrush(res, "PosAccentBlueLabelBrush", "#0E9F6E");
                SetSolidBrush(res, "PosTotalAmountBrush", "#E11D48");
                SetSolidBrush(res, "PosBigTotalBgBrush", "#F4F5F8");
                SetSolidBrush(res, "PosBigTotalBorderBrush", "#C5CCD8");
                SetSolidBrush(res, "PosBigTotalFgBrush", "#E11D48");
                SetHorizontalGradient(res, "PosKpiTotalBgBrush", ("#181C24", 0.0), ("#242936", 1.0));
                SetSolidBrush(res, "PosKpiTotalLabelBrush", "#CBD5E1");
                SetSolidBrush(res, "PosKpiTotalFgBrush", "#FFFFFF");
                SetVerticalGradient(res, "PosButtonBgBrush", "#FFFFFF", "#F2F4F7");
                SetVerticalGradient(res, "PosButtonHoverBgBrush", "#F8FAFC", "#E8ECF2");
                SetSolidBrush(res, "PosButtonBorderBrush", "#B6BFCC");
                SetSolidBrush(res, "PosButtonFgBrush", "#181C24");
                SetHorizontalGradient(res, "PosButtonActiveBgBrush", ("#DCEBFF", 0.0), ("#E0F9EE", 0.50), ("#FFE4EC", 1.0));
                SetSolidBrush(res, "PosButtonActiveBorderBrush", "#A3B1C6");
                SetSolidBrush(res, "PosButtonActiveFgBrush", "#C81E45");
                SetHorizontalGradient(res, "PosButtonPrimaryBgBrush", ("#2563EB", 0.0), ("#0E9F6E", 0.35), ("#D97706", 0.68), ("#E11D48", 1.0));
                SetSolidBrush(res, "PosButtonPrimaryFgBrush", "#FFFFFF");
                SetVerticalGradient(res, "PosClassicGrabarBgBrush", "#FFFFFF", "#F2F4F7");
                SetSolidBrush(res, "PosClassicGrabarBorderBrush", "#B6BFCC");
                SetVerticalGradient(res, "PosGridHeaderBrush", "#FAFAFC", "#ECEFF4");
                SetSolidBrush(res, "PosGridHeaderFgBrush", "#181C24");
                SetSolidBrush(res, "PosGridLinesBrush", "#D8DDE6");
                SetSolidBrush(res, "PosIvaStripBgBrush", "#FFFFFF");
                SetSolidBrush(res, "PosIvaLabelBrush", "#334155");
                SetSolidBrush(res, "PosIvaBlueBrush", "#0E9F6E");
                SetSolidBrush(res, "PosCardBgBrush", "#FFFFFF");
                SetSolidBrush(res, "PosSurfaceAltBrush", "#F1F3F6");
                SetSolidBrush(res, "PosSurfaceAccentBrush", "#E5E9F0");

                var posHero = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                posHero.GradientStops.Add(new GradientStop(ParseColor("#F8F9FB"), 0.0));
                posHero.GradientStops.Add(new GradientStop(ParseColor("#F3F8F6"), 0.55));
                posHero.GradientStops.Add(new GradientStop(ParseColor("#FDF8F2"), 1.0));
                posHero.Freeze();
                res["PosHeroGradientBrush"] = posHero;
            }
            else
            {
                SetSolidBrush(res, "PosWindowBgBrush", "#111318");
                SetSolidBrush(res, "PosPanelBgBrush", "#181B22");
                SetSolidBrush(res, "PosPanelBorderBrush", "#323846");
                SetHorizontalGradient(res, "PosTopBannerBrush", ("#15233E", 0.0), ("#122B24", 0.34), ("#2E220F", 0.68), ("#301521", 1.0));
                SetSolidBrush(res, "PosTopBannerBorderBrush", "#363D4D");
                SetSolidBrush(res, "PosTopBannerTitleBrush", "#F3F4F6");
                SetHorizontalGradient(res, "PosSectionBarBrush", ("#1D4ED8", 0.0), ("#059669", 0.34), ("#B45309", 0.68), ("#BE123C", 1.0));
                SetSolidBrush(res, "PosSectionBarBorderBrush", "#475569");
                SetSolidBrush(res, "PosSectionBarTextBrush", "#FFFFFF");
                SetHorizontalGradient(res, "PosFooterBarBrush", ("#15233E", 0.0), ("#122B24", 0.34), ("#2E220F", 0.68), ("#301521", 1.0));
                SetSolidBrush(res, "PosFooterBorderBrush", "#363D4D");
                SetSolidBrush(res, "PosFooterTextBrush", "#E5E7EB");
                SetSolidBrush(res, "PosTextPrimaryBrush", "#F3F4F6");
                SetSolidBrush(res, "PosTextSecondaryBrush", "#9CA3AF");
                SetSolidBrush(res, "PosSubTotalLabelBrush", "#D1D5DB");
                SetSolidBrush(res, "PosSubTotalValueBrush", "#34D399");
                SetSolidBrush(res, "PosInputBgBrush", "#14171F");
                SetSolidBrush(res, "PosInputReadOnlyBgBrush", "#1D212B");
                SetSolidBrush(res, "PosInputBorderBrush", "#3B4254");
                SetSolidBrush(res, "PosAccentRedLabelBrush", "#FB7185");
                SetSolidBrush(res, "PosAccentBlueLabelBrush", "#34D399");
                SetSolidBrush(res, "PosTotalAmountBrush", "#FB7185");
                SetSolidBrush(res, "PosBigTotalBgBrush", "#161922");
                SetSolidBrush(res, "PosBigTotalBorderBrush", "#323846");
                SetSolidBrush(res, "PosBigTotalFgBrush", "#FB7185");
                SetHorizontalGradient(res, "PosKpiTotalBgBrush", ("#1A1E28", 0.0), ("#242A38", 1.0));
                SetSolidBrush(res, "PosKpiTotalLabelBrush", "#9CA3AF");
                SetSolidBrush(res, "PosKpiTotalFgBrush", "#F8FAFC");
                SetVerticalGradient(res, "PosButtonBgBrush", "#222733", "#1A1E28");
                SetVerticalGradient(res, "PosButtonHoverBgBrush", "#2B3140", "#222733");
                SetSolidBrush(res, "PosButtonBorderBrush", "#3D4556");
                SetSolidBrush(res, "PosButtonFgBrush", "#F3F4F6");
                SetHorizontalGradient(res, "PosButtonActiveBgBrush", ("#15233E", 0.0), ("#122B24", 0.50), ("#301521", 1.0));
                SetSolidBrush(res, "PosButtonActiveBorderBrush", "#64748B");
                SetSolidBrush(res, "PosButtonActiveFgBrush", "#F8FAFC");
                SetHorizontalGradient(res, "PosButtonPrimaryBgBrush", ("#2563EB", 0.0), ("#0E9F6E", 0.35), ("#D97706", 0.68), ("#E11D48", 1.0));
                SetSolidBrush(res, "PosButtonPrimaryFgBrush", "#FFFFFF");
                SetVerticalGradient(res, "PosClassicGrabarBgBrush", "#222733", "#1A1E28");
                SetSolidBrush(res, "PosClassicGrabarBorderBrush", "#3D4556");
                SetVerticalGradient(res, "PosGridHeaderBrush", "#202531", "#181C24");
                SetSolidBrush(res, "PosGridHeaderFgBrush", "#E5E7EB");
                SetSolidBrush(res, "PosGridLinesBrush", "#2B3140");
                SetSolidBrush(res, "PosIvaStripBgBrush", "#181B22");
                SetSolidBrush(res, "PosIvaLabelBrush", "#D1D5DB");
                SetSolidBrush(res, "PosIvaBlueBrush", "#34D399");
                SetSolidBrush(res, "PosCardBgBrush", "#181B22");
                SetSolidBrush(res, "PosSurfaceAltBrush", "#21252F");
                SetSolidBrush(res, "PosSurfaceAccentBrush", "#292F3D");

                var posHero = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                posHero.GradientStops.Add(new GradientStop(ParseColor("#111318"), 0.0));
                posHero.GradientStops.Add(new GradientStop(ParseColor("#161B22"), 0.55));
                posHero.GradientStops.Add(new GradientStop(ParseColor("#1A1920"), 1.0));
                posHero.Freeze();
                res["PosHeroGradientBrush"] = posHero;
            }
        }
        else if (IsClassicFoxPalette)
        {
            // Authentic Visual FoxPro 9 / Sufer POS color scheme scoped strictly to ABMVENTAS & ITEMSFACTURAVENTAS
            SetSolidBrush(res, "PosWindowBgBrush", "#F8F8F8");
            SetSolidBrush(res, "PosPanelBgBrush", "#F8F8F8");
            SetSolidBrush(res, "PosPanelBorderBrush", "#8A9CB5");
            SetVerticalGradient(res, "PosTopBannerBrush", "#D7E7FD", "#FFFFFF", "#F6FAFF");
            SetSolidBrush(res, "PosTopBannerBorderBrush", "#9EB8DD");
            SetSolidBrush(res, "PosTopBannerTitleBrush", "#5B7CA8");
            SetVerticalGradient(res, "PosSectionBarBrush", "#AFCFFF", "#5A84C4", "#7FA8E3");
            SetSolidBrush(res, "PosSectionBarBorderBrush", "#6393DF");
            SetSolidBrush(res, "PosSectionBarTextBrush", "#FFFFFF");
            SetVerticalGradient(res, "PosFooterBarBrush", "#BBD6FE", "#5A84C4", "#7EA7E2");
            SetSolidBrush(res, "PosFooterBorderBrush", "#6393DF");
            SetSolidBrush(res, "PosFooterTextBrush", "#000000");
            SetSolidBrush(res, "PosTextPrimaryBrush", "#000000");
            SetSolidBrush(res, "PosTextSecondaryBrush", "#4B5563");
            SetSolidBrush(res, "PosSubTotalLabelBrush", "#666666");
            SetSolidBrush(res, "PosSubTotalValueBrush", "#000080");
            SetSolidBrush(res, "PosInputBgBrush", "#FFFFFF");
            SetSolidBrush(res, "PosInputReadOnlyBgBrush", "#E6EBF2");
            SetSolidBrush(res, "PosInputBorderBrush", "#808080");
            SetSolidBrush(res, "PosAccentRedLabelBrush", "#FF0000");
            SetSolidBrush(res, "PosAccentBlueLabelBrush", "#0000FF");
            SetSolidBrush(res, "PosTotalAmountBrush", "#FF0000");
            SetSolidBrush(res, "PosBigTotalBgBrush", "#EFEFEF");
            SetSolidBrush(res, "PosBigTotalBorderBrush", "#808080");
            SetSolidBrush(res, "PosBigTotalFgBrush", "#FF0000");
            SetSolidBrush(res, "PosKpiTotalBgBrush", "#EFEFEF");
            SetSolidBrush(res, "PosKpiTotalLabelBrush", "#102A4C");
            SetSolidBrush(res, "PosKpiTotalFgBrush", "#FF0000");
            SetVerticalGradient(res, "PosButtonBgBrush", "#FFFFFF", "#E8EEF7");
            SetVerticalGradient(res, "PosButtonHoverBgBrush", "#F2F7FF", "#D3E3FA");
            SetSolidBrush(res, "PosButtonBorderBrush", "#7F9AB8");
            SetSolidBrush(res, "PosButtonFgBrush", "#1E293B");
            SetVerticalGradient(res, "PosButtonActiveBgBrush", "#D8E9FC", "#B2D0F7");
            SetSolidBrush(res, "PosButtonActiveBorderBrush", "#3C71B8");
            SetSolidBrush(res, "PosButtonActiveFgBrush", "#FF0000");
            SetVerticalGradient(res, "PosButtonPrimaryBgBrush", "#DDEBFD", "#BBD5F7");
            SetSolidBrush(res, "PosButtonPrimaryFgBrush", "#102A4C");
            SetVerticalGradient(res, "PosClassicGrabarBgBrush", "#DDEBFD", "#BBD5F7");
            SetSolidBrush(res, "PosClassicGrabarBorderBrush", "#3C71B8");
            SetVerticalGradient(res, "PosGridHeaderBrush", "#F8FAFD", "#DCE5F2");
            SetSolidBrush(res, "PosGridHeaderFgBrush", "#000000");
            SetSolidBrush(res, "PosGridLinesBrush", "#D0D7E2");
            SetSolidBrush(res, "PosIvaStripBgBrush", "#FFFFFF");
            SetSolidBrush(res, "PosIvaLabelBrush", "#666666");
            SetSolidBrush(res, "PosIvaBlueBrush", "#0000FF");
            SetSolidBrush(res, "PosCardBgBrush", "#F5F8FD");
            SetSolidBrush(res, "PosSurfaceAltBrush", "#E2ECF9");
            SetSolidBrush(res, "PosSurfaceAccentBrush", "#C7DDF9");

            var posHero = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            posHero.GradientStops.Add(new GradientStop(ParseColor("#D9E8FC"), 0.0));
            posHero.GradientStops.Add(new GradientStop(ParseColor("#EFF5FD"), 0.55));
            posHero.GradientStops.Add(new GradientStop(ParseColor("#C7DDF9"), 1.0));
            posHero.Freeze();
            res["PosHeroGradientBrush"] = posHero;
        }
        else
        {
            Color emerald = isDark ? ParseColor("#34D399") : ParseColor("#059669");
            Color redWarn = isDark ? ParseColor("#F87171") : ParseColor("#B91C1C");

            SetSolidBrush(res, "PosWindowBgBrush", screenBg);
            SetSolidBrush(res, "PosPanelBgBrush", cardBg);
            SetSolidBrush(res, "PosPanelBorderBrush", border);
            SetSolidBrush(res, "PosTopBannerBrush", cardBg);
            SetSolidBrush(res, "PosTopBannerBorderBrush", border);
            SetSolidBrush(res, "PosTopBannerTitleBrush", ink);
            SetSolidBrush(res, "PosSectionBarBrush", surfaceAlt);
            SetSolidBrush(res, "PosSectionBarBorderBrush", border);
            SetSolidBrush(res, "PosSectionBarTextBrush", ink);
            SetSolidBrush(res, "PosFooterBarBrush", surfaceAlt);
            SetSolidBrush(res, "PosFooterBorderBrush", border);
            SetSolidBrush(res, "PosFooterTextBrush", ink);
            SetSolidBrush(res, "PosTextPrimaryBrush", ink);
            SetSolidBrush(res, "PosTextSecondaryBrush", inkSoft);
            SetSolidBrush(res, "PosSubTotalLabelBrush", inkSoft);
            SetSolidBrush(res, "PosSubTotalValueBrush", ink);
            SetSolidBrush(res, "PosInputBgBrush", inputBg);
            SetSolidBrush(res, "PosInputReadOnlyBgBrush", inputReadOnlyBg);
            SetSolidBrush(res, "PosInputBorderBrush", inputBorder);
            SetSolidBrush(res, "PosAccentRedLabelBrush", redWarn);
            SetSolidBrush(res, "PosAccentBlueLabelBrush", ink);
            SetSolidBrush(res, "PosTotalAmountBrush", emerald);
            SetSolidBrush(res, "PosBigTotalBgBrush", surfaceAlt);
            SetSolidBrush(res, "PosBigTotalBorderBrush", border);
            SetSolidBrush(res, "PosBigTotalFgBrush", emerald);
            SetSolidBrush(res, "PosKpiTotalBgBrush", kpiBg);
            SetSolidBrush(res, "PosKpiTotalLabelBrush", kpiText);
            SetSolidBrush(res, "PosKpiTotalFgBrush", kpiValue);
            SetSolidBrush(res, "PosButtonBgBrush", cardBg);
            SetSolidBrush(res, "PosButtonHoverBgBrush", surfaceAlt);
            SetSolidBrush(res, "PosButtonBorderBrush", border);
            SetSolidBrush(res, "PosButtonFgBrush", ink);
            SetSolidBrush(res, "PosButtonActiveBgBrush", btnPrimaryBg);
            SetSolidBrush(res, "PosButtonActiveBorderBrush", btnPrimaryBg);
            SetSolidBrush(res, "PosButtonActiveFgBrush", textInverse);
            SetSolidBrush(res, "PosButtonPrimaryBgBrush", btnPrimaryBg);
            SetSolidBrush(res, "PosButtonPrimaryFgBrush", textInverse);
            SetSolidBrush(res, "PosClassicGrabarBgBrush", cardBg);
            SetSolidBrush(res, "PosClassicGrabarBorderBrush", border);
            SetSolidBrush(res, "PosGridHeaderBrush", surfaceAccent);
            SetSolidBrush(res, "PosGridHeaderFgBrush", ink);
            SetSolidBrush(res, "PosGridLinesBrush", divider);
            SetSolidBrush(res, "PosIvaStripBgBrush", cardBg);
            SetSolidBrush(res, "PosIvaLabelBrush", inkSoft);
            SetSolidBrush(res, "PosIvaBlueBrush", ink);
            SetSolidBrush(res, "PosCardBgBrush", cardBg);
            SetSolidBrush(res, "PosSurfaceAltBrush", surfaceAlt);
            SetSolidBrush(res, "PosSurfaceAccentBrush", surfaceAccent);
            res["PosHeroGradientBrush"] = heroBrush;
        }
    }

    private static void SetColorAndBrush(ResourceDictionary res, string token, Color color)
    {
        res[$"{token}Color"] = color;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        res[$"{token}Brush"] = brush;
    }

    private static void SetSolidBrush(ResourceDictionary res, string key, string hex)
        => SetSolidBrush(res, key, ParseColor(hex));

    private static void SetSolidBrush(ResourceDictionary res, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        res[key] = brush;
    }

    private static void SetHorizontalGradient(ResourceDictionary res, string key, params (string Hex, double Offset)[] stops)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        foreach (var (hex, offset) in stops)
            brush.GradientStops.Add(new GradientStop(ParseColor(hex), offset));
        brush.Freeze();
        res[key] = brush;
    }

    private static void SetVerticalGradient(ResourceDictionary res, string key, string topHex, string bottomHex, string? midHex = null)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        brush.GradientStops.Add(new GradientStop(ParseColor(topHex), 0.0));
        if (!string.IsNullOrWhiteSpace(midHex))
            brush.GradientStops.Add(new GradientStop(ParseColor(midHex), 0.55));
        brush.GradientStops.Add(new GradientStop(ParseColor(bottomHex), 1.0));
        brush.Freeze();
        res[key] = brush;
    }

    private static Color ParseColor(string hex)
        => (Color)ColorConverter.ConvertFromString(hex);

    private static Color Blend(Color a, Color b, double ratioB)
    {
        double ratioA = 1.0 - ratioB;
        return Color.FromRgb(
            (byte)Math.Clamp(a.R * ratioA + b.R * ratioB, 0, 255),
            (byte)Math.Clamp(a.G * ratioA + b.G * ratioB, 0, 255),
            (byte)Math.Clamp(a.B * ratioA + b.B * ratioB, 0, 255));
    }

    private static Color Darken(Color c, double amount)
        => Blend(c, Colors.Black, amount);

    private static Color Lighten(Color c, double amount)
        => Blend(c, Colors.White, amount);
}
