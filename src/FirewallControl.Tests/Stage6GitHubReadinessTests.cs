using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using FirewallControl.App.Services;
using FirewallControl.App.Views;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Infrastructure.Services;
using Xunit;

namespace FirewallControl.Tests;

public class Stage6GitHubReadinessTests
{
    [Fact]
    public void LatamCurrencyCatalog_ContainsAllLatinAmericanCurrencies_FormatsAndConvertsCleanly()
    {
        Assert.True(LatamCurrencyCatalog.All.Count >= 21, "Debe incluir las 20 monedas latinoamericanas + EUR.");

        string[] expectedIsoCodes =
        {
            "PYG", "ARS", "BOB", "BRL", "CLP", "COP", "CRC", "CUP", "DOP",
            "USD", "GTQ", "HNL", "HTG", "MXN", "NIO", "PAB", "PEN", "UYU", "VES", "BZD", "EUR"
        };

        foreach (string code in expectedIsoCodes)
        {
            var cur = LatamCurrencyCatalog.GetByCode(code);
            Assert.Equal(code, cur.IsoCode);
            Assert.False(string.IsNullOrWhiteSpace(cur.Name));
            Assert.False(string.IsNullOrWhiteSpace(cur.Symbol));
            Assert.False(string.IsNullOrWhiteSpace(cur.Country));
        }

        Assert.Equal("15,000 Gs.", LatamCurrencyCatalog.FormatAmount(15000m, "PYG"));
        Assert.Equal("US$ 100.50", LatamCurrencyCatalog.FormatAmount(100.5m, "USD"));
        Assert.Equal("R$ 58.25", LatamCurrencyCatalog.FormatAmount(58.25m, "BRL"));
        Assert.Equal("$ 1,250", LatamCurrencyCatalog.FormatAmount(1250m, "CLP"));

        decimal brlFromUsd = LatamCurrencyCatalog.Convert(10m, "USD", "BRL");
        Assert.True(brlFromUsd > 10m);

        var entities = LatamCurrencyCatalog.ToCurrencyEntities("PYG");
        Assert.Equal(LatamCurrencyCatalog.All.Count, entities.Count);
        Assert.Equal(1, entities[0].IdMoneda);
        Assert.Contains("PYG", entities[0].Descripcion);

        Assert.Contains("GUARANIES", VfpCompat.Letras(25000m, "PYG"));
        Assert.Contains("REALES CON 50/100", VfpCompat.Letras(100.50m, "BRL"));
        Assert.Contains("PESOS MEXICANOS", VfpCompat.Letras(500m, "MXN"));
        Assert.Contains("SOLES CON 25/100", VfpCompat.Letras(40.25m, "PEN"));
    }

    [Fact]
    public void LocalizationService_SwitchesLiveBetweenSpanishPortugueseAndEnglish()
    {
        Exception? threadEx = null;
        var sta = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    var app = new FirewallControl.App.App();
                    app.InitializeComponent();
                }

                Assert.Equal(3, LocalizationService.Languages.Count);

                LocalizationService.ApplyLanguage("es");
                Assert.Equal("es", LocalizationService.CurrentLanguage);
                Assert.Equal("Nueva Venta", LocalizationService.Get("Str_Tile_NuevaVenta"));

                LocalizationService.ApplyLanguage("pt");
                Assert.Equal("pt", LocalizationService.CurrentLanguage);
                Assert.Equal("Nova Venda", LocalizationService.Get("Str_Tile_NuevaVenta"));

                LocalizationService.ApplyLanguage("en");
                Assert.Equal("en", LocalizationService.CurrentLanguage);
                Assert.Equal("New Sale", LocalizationService.Get("Str_Tile_NuevaVenta"));

                LocalizationService.ApplyLanguage("es");
                Assert.Equal("es", LocalizationService.CurrentLanguage);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        sta.SetApartmentState(ApartmentState.STA);
        sta.Start();
        sta.Join();
        Assert.Null(threadEx);
    }

    [Fact]
    public void FactoryReset_RestoresCleanFactoryDefaults_AndConfigHasNoHardcodedPassword()
    {
        var cleanCfg = new FirewallConfig();
        Assert.Equal(string.Empty, cleanCfg.SqlPassword);

        var cleanCompany = new CompanyDataEntity();
        Assert.Equal("SEFIROS POS", cleanCompany.Descripcion);

        string tempDir = Path.Combine(Path.GetTempPath(), $"sefiros_factory_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var cfg = new ConfigService(tempDir);
            Assert.Equal("sefiros", cfg.Preferences.TemaAtelier);
            Assert.Equal("es", cfg.Preferences.Idioma);
            Assert.Equal("PYG", cfg.Preferences.MonedaBaseIso);
            Assert.Equal("Renovado", cfg.Preferences.EstiloPos);
            Assert.False(cfg.Preferences.NoControlarStockGlobal);

            // Mutate preferences as an end-user would
            cfg.Preferences.TemaAtelier = "rose";
            cfg.Preferences.TemaOscuroConfiguracion = true;
            cfg.Preferences.Idioma = "en";
            cfg.Preferences.MonedaBaseIso = "MXN";
            cfg.Preferences.EstiloPos = "Clasico";
            cfg.Preferences.NoControlarStockGlobal = true;
            cfg.Preferences.CustomShortcuts["MAIN_CAJA_1"] = "F8";
            cfg.SavePreferences();

            // Factory reset restores clean factory state
            cfg.ResetToFactoryDefaults();
            var reloaded = new ConfigService(tempDir);
            Assert.Equal("sefiros", reloaded.Preferences.TemaAtelier);
            Assert.False(reloaded.Preferences.TemaOscuroConfiguracion);
            Assert.Equal("es", reloaded.Preferences.Idioma);
            Assert.Equal("PYG", reloaded.Preferences.MonedaBaseIso);
            Assert.Equal("Renovado", reloaded.Preferences.EstiloPos);
            Assert.Equal("Moderna", reloaded.Preferences.PaletaColores);
            Assert.False(reloaded.Preferences.NoControlarStockGlobal);
            Assert.Empty(reloaded.Preferences.CustomShortcuts);

            // Verify ConfiguracionPreferenciasWindow exposes Language, Currency and Factory Reset controls
            Exception? uiEx = null;
            var sta = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null)
                    {
                        var app = new FirewallControl.App.App();
                        app.InitializeComponent();
                    }

                    var win = new ConfiguracionPreferenciasWindow(reloaded);
                    Assert.IsType<ComboBox>(win.FindName("CmbIdioma"));
                    Assert.IsType<ComboBox>(win.FindName("CmbMonedaBase"));
                    Assert.IsType<Button>(win.FindName("BtnFactoryReset"));
                    win.Close();
                }
                catch (Exception ex)
                {
                    uiEx = ex;
                }
                finally
                {
                    LocalizationService.ApplyLanguage("es");
                    ThemeService.ApplyTheme("sefiros", isDark: false, "Moderna");
                }
            });
            sta.SetApartmentState(ApartmentState.STA);
            sta.Start();
            sta.Join();
            Assert.Null(uiEx);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SecurityAudit_ZeroPersonalPathsOrCredentialsInSourceTree()
    {
        string sourceRoot = TestPathHelper.GetSourceRoot();
        string[] forbiddenTokens =
        {
            "koraimae",
            "wulli",
            "192.168.1.34",
            "AIzaSy",
            @"c:\Users\"
        };

        var files = Directory.GetFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                     && (f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                      || f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                      || f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                      || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)));

        foreach (string file in files)
        {
            string content = File.ReadAllText(file);
            foreach (string token in forbiddenTokens)
            {
                // Ignore the literal forbidden-token array inside this test file itself
                if (Path.GetFileName(file).Equals("Stage6GitHubReadinessTests.cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                Assert.False(content.Contains(token, StringComparison.OrdinalIgnoreCase),
                    $"Token sensible '{token}' encontrado en {file}");
            }
        }
    }
}
