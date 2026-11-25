using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FirewallControl.App;
using FirewallControl.App.Services;
using FirewallControl.App.Views;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Infrastructure.Database;
using FirewallControl.Infrastructure.Repositories;
using FirewallControl.Infrastructure.Services;
using Xunit;

namespace FirewallControl.Tests;

public class Stage5SefirosPolishTests
{
    private static string GetSourceRoot() => TestPathHelper.GetSourceRoot();

    private static string GetWorkspaceRoot() => TestPathHelper.GetWorkspaceRootOrTemp();

    private static WindowManagerService CreateTestWindowManager(ConfigService cfg)
    {
        var conn = new MySqlConnectionFactory(cfg);
        var dbf = new VfpDbfService();
        var prodRepo = new ProductRepository(conn);
        var salesRepo = new SalesRepository(conn, cfg);
        var custRepo = new CustomerRepository(conn);
        var purchRepo = new PurchaseRepository(conn);
        var cashRepo = new CashRepository(conn);
        var sysRepo = new SystemRepository(conn);
        var printSvc = new TicketPrintService(cfg);
        var iaSvc = new CargaRapidaIaService();
        var stage3Repo = new Stage3Repository(conn);

        return new WindowManagerService(
            cfg, conn, dbf, prodRepo, salesRepo, custRepo,
            purchRepo, cashRepo, sysRepo, printSvc, iaSvc, stage3Repo);
    }

    [Fact]
    public void Codebase_HasZeroUnprofessionalValidationMessages()
    {
        string[] forbiddenPhrases =
        {
            "no mi ciela",
            "bebesita",
            "aqui no debes tocar",
            "doble escaneado"
        };

        var files = Directory.GetFiles(GetSourceRoot(), "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                        && !f.EndsWith("Stage5SefirosPolishTests.cs", StringComparison.OrdinalIgnoreCase));

        foreach (var file in files)
        {
            string text = File.ReadAllText(file);
            foreach (var phrase in forbiddenPhrases)
            {
                Assert.False(
                    text.Contains(phrase, StringComparison.OrdinalIgnoreCase),
                    $"Frase no profesional '{phrase}' encontrada en {file}");
            }
        }
    }

    [Fact]
    public void EditableItemRestrictions_AllowCustomLowPriceLike3_AndCustomOrUnlimitedQuantity()
    {
        // Default rejects 3 (below 99) and >120
        var defaultLowPrice = SalesCalculationEngine.ValidateUnitPrice(3m, 1m, checkGrabarLimits: true, permitirVentaBajoCosto: false, precioMinimo: 99m, precioMaximo: 1000000m);
        Assert.False(defaultLowPrice.IsValid);

        // Editable PrecioMinimoVenta = 0 (or <= 3) permits price = 3
        var editableLowPrice = SalesCalculationEngine.ValidateUnitPrice(3m, 1m, checkGrabarLimits: true, permitirVentaBajoCosto: false, precioMinimo: 0m, precioMaximo: 1000000m);
        Assert.True(editableLowPrice.IsValid);

        // Disabling ValidarLimitesPrecioEnItem also permits any positive price when above cost (or PermitirVentaBajoCosto = true)
        var unlimitedPrice = SalesCalculationEngine.ValidateUnitPrice(3m, 10m, checkGrabarLimits: false, permitirVentaBajoCosto: true, precioMinimo: 99m, precioMaximo: 1000000m);
        Assert.True(unlimitedPrice.IsValid);

        // LimiteCantidadPorItem = 0 disables upper limit; custom 250 enforces 250
        Assert.True(SalesCalculationEngine.ValidateQuantity(500m, 1, 1, 0m, noControlarStockGlobal: false, limiteCantidad: 0m).IsValid);
        Assert.True(SalesCalculationEngine.ValidateQuantity(200m, 1, 1, 0m, noControlarStockGlobal: false, limiteCantidad: 250m).IsValid);
        Assert.False(SalesCalculationEngine.ValidateQuantity(251m, 1, 1, 0m, noControlarStockGlobal: false, limiteCantidad: 250m).IsValid);

        // Preferences defaults
        var prefs = new AppPreferencesEntity();
        Assert.Equal(99m, prefs.PrecioMinimoVenta);
        Assert.Equal(1000000m, prefs.PrecioMaximoVenta);
        Assert.True(prefs.ValidarLimitesPrecioEnItem);
        Assert.True(prefs.CongelarStockSinControl);
        Assert.False(prefs.ItemFacturaModal);
    }

    [Fact]
    public void CongelarStockSinControl_PreventsDeductionIntoNegative_ForNoControlProducts()
    {
        var prefsFreeze = new AppPreferencesEntity { CongelarStockSinControl = true, NoControlarStockGlobal = false };
        Assert.True(SalesRepository.ShouldSkipStockUpdate(1, prefsFreeze));
        Assert.False(SalesRepository.ShouldSkipStockUpdate(0, prefsFreeze));

        var prefsGlobalFreeFreeze = new AppPreferencesEntity
        {
            CongelarStockSinControl = true,
            NoControlarStockGlobal = true,
            DescontarStockAunqueNoControle = false
        };
        Assert.True(SalesRepository.ShouldSkipStockUpdate(0, prefsGlobalFreeFreeze));

        var prefsNoFreeze = new AppPreferencesEntity { CongelarStockSinControl = false };
        Assert.False(SalesRepository.ShouldSkipStockUpdate(1, prefsNoFreeze));
    }

    [Fact]
    public void ThemeService_KeepsRealSemanticGreenAndBlueColors_EvenInNeutroAtelier()
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

                foreach (bool isDark in new[] { false, true })
                {
                    ThemeService.ApplyTheme("neutro", isDark: isDark, "Moderna");

                    var successText = Assert.IsType<SolidColorBrush>(Application.Current!.TryFindResource("SuccessTextBrush")).Color;
                    Assert.True(successText.G > successText.R && successText.G > successText.B,
                        $"SuccessText debe ser verde real (isDark={isDark}), actual: {successText}");

                    var infoText = Assert.IsType<SolidColorBrush>(Application.Current.TryFindResource("InfoTextBrush")).Color;
                    Assert.True(infoText.B > infoText.R && infoText.B > 120,
                        $"InfoText debe ser azul real (isDark={isDark}), actual: {infoText}");
                }
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
            finally
            {
                ThemeService.ApplyTheme("neutro", isDark: false, "Moderna");
            }
        });
        sta.SetApartmentState(ApartmentState.STA);
        sta.Start();
        sta.Join();
        Assert.Null(threadEx);
    }

    [Fact]
    public void SalesCloseX_ClearsCart_MinimizeAndCajaSwitchPreserve_AndModal1LocksBackground()
    {
        Exception? threadEx = null;
        var sta = new Thread(() =>
        {
            WindowManagerService? wm = null;
            try
            {
                if (Application.Current == null)
                {
                    var app = new FirewallControl.App.App();
                    app.InitializeComponent();
                }

                var cfg = new ConfigService(GetWorkspaceRoot());
                cfg.Preferences.EstiloPos = "Clasico";
                cfg.Preferences.PaletaColores = "ClasicaFox";
                cfg.Preferences.ItemFacturaModal = false;

                wm = CreateTestWindowManager(cfg);

                // 1. Cargar items en Caja 1 y Caja 2
                wm.SaltarACaja(1);
                PumpDispatcher();
                var vm1 = wm.GetOpenSalesViewModel(1)!;
                SalesCalculationEngine.ApplyItemOperation(vm1.Items, new SaleItemEntity
                {
                    IdProducto = 10,
                    Codigo = "A1",
                    Descripcion = "PRODUCTO CAJA 1",
                    Cantidad = 2m,
                    PreUnitario = 15000m,
                    Iva = 1
                }, "A", 0);
                vm1.SumarProductos();

                wm.SaltarACaja(2);
                PumpDispatcher();
                var vm2 = wm.GetOpenSalesViewModel(2)!;
                SalesCalculationEngine.ApplyItemOperation(vm2.Items, new SaleItemEntity
                {
                    IdProducto = 20,
                    Codigo = "A2",
                    Descripcion = "PRODUCTO CAJA 2",
                    Cantidad = 1m,
                    PreUnitario = 25000m,
                    Iva = 1
                }, "A", 0);
                vm2.SumarProductos();

                // Cambio a Caja 2 conserva Caja 1
                Assert.Single(vm1.Items);
                Assert.Single(vm2.Items);

                // ABMVENTAS usa tamaño amplio por defecto (~94% x ~96% del WorkArea) idéntico entre Cajas
                Rect? bounds2 = wm.GetSalesWindowBounds(2);
                Assert.NotNull(bounds2);
                Rect work = SystemParameters.WorkArea;
                if (work.Width <= 200 || work.Height <= 200)
                    work = new Rect(0, 0, 1366, 728);
                double expectedW = Math.Round(Math.Clamp(work.Width * 0.94, Math.Min(1040, work.Width), Math.Max(Math.Min(1040, work.Width), work.Width - 24)));
                double expectedH = Math.Round(Math.Clamp(work.Height * 0.96, Math.Min(640, work.Height), Math.Max(Math.Min(640, work.Height), work.Height - 16)));
                Assert.InRange(bounds2!.Value.Width, expectedW - 2, expectedW + 2);
                Assert.InRange(bounds2.Value.Height, expectedH - 2, expectedH + 2);

                // 2. Modal 1 desactivado por defecto -> fondo NO bloqueado; Cantidad inicial = 0
                Assert.False(wm.IsBackgroundLockedByItemModal);
                var selVm = wm.GetItemsSelectorViewModel()!;
                Assert.Equal(0m, selVm.Cantidad);

                // Genes A/S/D/F abren SIEMPRE incluso cuando hay texto escrito en codproducto (ej: "2")
                selVm.CodProducto = "2";
                foreach (string genKey in new[] { "A", "S", "D", "F" })
                {
                    var genBinding = ShortcutCatalog.ResolveTriggeredBinding(
                        cfg.Preferences,
                        ShortcutCatalog.WinItemsFactura,
                        genKey,
                        isInputEmpty: false);
                    Assert.NotNull(genBinding);
                    Assert.Equal(genKey, genBinding!.ActionCode);
                }
                selVm.CodProducto = string.Empty;

                // Botón Lupa en ITEMSFACTURAVENTAS clásico no toma foco de teclado y tiene comando ejecutable
                var selWinCheck = GetPrivateSelectorWindow(wm);
                var lupaBtn = Assert.IsType<Button>(selWinCheck.FindName("BtnBuscarProducto"));
                Assert.False(lupaBtn.Focusable);
                Assert.NotNull(lupaBtn.Command);
                Assert.True(lupaBtn.Command.CanExecute(null));

                // Activar Modal 1 y refrescar -> bloqueo Win32 nativo SIN poner gris (IsEnabled sigue true en WPF)
                var salesWin2 = GetPrivateSalesWindow(wm, 2);
                cfg.Preferences.ItemFacturaModal = true;
                wm.RefreshItemSelectorModalState();
                PumpDispatcher();
                Assert.True(wm.IsBackgroundLockedByItemModal);
                Assert.True(salesWin2.IsEnabled);
                Assert.True(wm.IsWindowNativeInputLocked(salesWin2), "Modal 1 debe bloquear entrada Win32 nativa sin deshabilitar WPF.");

                // Cerrar selector -> libera Modal 1 nativo sin perder Caja 2
                var selWin = GetPrivateSelectorWindow(wm);
                wm.HandleItemsSelectorClosedByUser(selWin);
                Assert.False(wm.IsBackgroundLockedByItemModal);
                Assert.False(wm.IsWindowNativeInputLocked(salesWin2));
                Assert.Single(vm2.Items);

                // 3. Minimizar ventana Caja 2 conserva su interior
                salesWin2.WindowState = WindowState.Minimized;
                PumpDispatcher();
                Assert.Single(vm2.Items);
                salesWin2.WindowState = WindowState.Normal;

                // 4. Cerrar Caja 1 con X limpia todo su interior pero deja Caja 2 intacta
                wm.SaltarACaja(1);
                var salesWin1 = GetPrivateSalesWindow(wm, 1);
                wm.HandleSalesWindowClosedByUser(1, salesWin1);
                Assert.Empty(vm1.Items);
                Assert.Equal(0m, vm1.TotalVenta);
                Assert.Single(vm2.Items);
                Assert.Equal(25000m, vm2.TotalVenta);

                // 5. F9 Carga Rapida tiene ChkFastNoControlaStock marcada por defecto
                var conn = new MySqlConnectionFactory(cfg);
                var cargaWin = new CargaRapidaProductosWindow(
                    new ProductRepository(conn),
                    cfg,
                    new CargaRapidaIaService(),
                    wm);
                var chkNoControl = Assert.IsType<CheckBox>(cargaWin.FindName("ChkFastNoControlaStock"));
                Assert.True(chkNoControl.IsChecked);
                cargaWin.AllowPermanentClose = true;
                cargaWin.Close();

                // 6. Sefiros POS MainWindow & ProductsManagementWindow & icono oficial cargan sin errores
                Assert.Equal("SISTEMA LICENCIADO - SEFIROS POS .NET 10", new VfpsLicenseService().RegisteredCompany);
                var mainWin = new MainWindow(
                    cfg,
                    new VfpsLicenseService(),
                    new VfpDbfService(),
                    new SystemRepository(conn),
                    new CashRepository(conn),
                    new SalesRepository(conn, cfg),
                    wm,
                    new Stage3Repository(conn));
                Assert.Contains("Sefiros POS", mainWin.Title, StringComparison.OrdinalIgnoreCase);
                Assert.Null(mainWin.FindName("BtnNavCaja1"));
                Assert.Null(mainWin.FindName("BtnNavCaja2"));
                Assert.NotNull(mainWin.FindName("BtnTileVenta"));
                Assert.NotNull(mainWin.FindName("LblCurrentUserTop"));

                var prodWin = new ProductsManagementWindow(
                    new ProductRepository(conn),
                    new SystemRepository(conn),
                    wm,
                    new PurchaseRepository(conn),
                    new CashRepository(conn));
                Assert.NotNull(prodWin.FindName("GridProducts"));
                Assert.NotNull(prodWin.FindName("TxtCosto1"));
                Assert.NotNull(prodWin.FindName("TxtPrecio7"));

                // Icon resource & PNG logo exist as embedded WPF resources + valid ICO frames (with biSizeImage > 0)
                var icoRes = Application.GetResourceStream(new Uri("pack://application:,,,/Sefiros.App;component/Assets/sefiros_pos.ico"));
                Assert.NotNull(icoRes);
                using (var icoMs = new MemoryStream())
                {
                    icoRes!.Stream.CopyTo(icoMs);
                    byte[] rawIco = icoMs.ToArray();
                    for (int i = 0; i < 5; i++)
                    {
                        int entryOff = 6 + (i * 16);
                        int imgOff = (int)BitConverter.ToUInt32(rawIco, entryOff + 12);
                        uint biSizeImage = BitConverter.ToUInt32(rawIco, imgOff + 20);
                        Assert.True(biSizeImage > 0, $"DIB frame {i} debe tener biSizeImage > 0.");
                    }

                    icoMs.Position = 0;
                    var dec = new System.Windows.Media.Imaging.IconBitmapDecoder(
                        icoMs,
                        System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                        System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                    Assert.Equal(7, dec.Frames.Count);
                }
                Assert.NotNull(Application.GetResourceStream(new Uri("pack://application:,,,/Sefiros.App;component/Assets/sefiros_pos_logo.png")));

                // Native Win32 icon handles (WM_SETICON + GCLP_HICON/SM) are populated when window is shown
                mainWin.WindowState = WindowState.Normal;
                mainWin.WindowStartupLocation = WindowStartupLocation.Manual;
                mainWin.Left = -3200;
                mainWin.Top = -3200;
                mainWin.ShowInTaskbar = false;
                mainWin.Show();
                PumpDispatcher();
                var iconHandles = AppIconHelper.InspectWindowIconHandles(mainWin);
                Assert.True(iconHandles.Big != IntPtr.Zero, "ICON_BIG debe estar asignado en HWND.");
                Assert.True(iconHandles.Small != IntPtr.Zero, "ICON_SMALL debe estar asignado en HWND.");
                Assert.True(iconHandles.ClassBig != IntPtr.Zero, "GCLP_HICON debe estar asignado.");
                Assert.True(iconHandles.ClassSmall != IntPtr.Zero, "GCLP_HICONSM debe estar asignado.");
                mainWin.Close();

                if (TestPathHelper.HasLocalLegacyDbfs())
                {
                    string deepPath = Path.Combine(GetSourceRoot(), "FirewallControl.App", "bin", "Release", "net10.0-windows", "win-x64");
                    var deepCfg = new ConfigService(deepPath);
                    var deepMenu = new VfpDbfService().LoadMenuStructure(Path.Combine(deepCfg.Config.BaseDirectory, "Styles"), includeVeterinaria: false);
                    Assert.True(deepMenu.Count >= 7, $"Esperados >=7 menús superiores desde ruta profunda, encontrados: {deepMenu.Count}");
                    Assert.Contains(deepMenu, m => m.CleanTitle.Contains("Archivo", StringComparison.OrdinalIgnoreCase));
                    Assert.Contains(deepMenu, m => m.CleanTitle.Contains("Personales", StringComparison.OrdinalIgnoreCase));
                    Assert.Contains(deepMenu, m => m.CleanTitle.Contains("Catalogos", StringComparison.OrdinalIgnoreCase));
                }

                // Even without any DBF files in a temp dir, fallback menu provides all 7 top-level menus
                var fallbackMenu = new VfpDbfService().LoadMenuStructure(Path.Combine(Path.GetTempPath(), $"no_styles_{Guid.NewGuid():N}"), includeVeterinaria: false);
                Assert.Equal(7, fallbackMenu.Count);

                // Verify MainWindow exposes both DynamicMainMenu and BtnOtrosModulos
                var otrosCfg = new ConfigService(GetWorkspaceRoot());
                var otrosConn = new MySqlConnectionFactory(otrosCfg);
                var otrosWm = CreateTestWindowManager(otrosCfg);
                var otrosMainWin = new FirewallControl.App.MainWindow(
                    otrosCfg,
                    new VfpsLicenseService(),
                    new VfpDbfService(),
                    new SystemRepository(otrosConn),
                    new CashRepository(otrosConn),
                    new SalesRepository(otrosConn),
                    otrosWm);
                Assert.IsType<Button>(otrosMainWin.FindName("BtnOtrosModulos"));
                var otrosBuildMethod = typeof(FirewallControl.App.MainWindow).GetMethod(
                    "BuildDynamicMenu",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(otrosBuildMethod);
                otrosBuildMethod!.Invoke(otrosMainWin, new object[] { true });
                var otrosDynamicMenu = Assert.IsType<Menu>(otrosMainWin.FindName("DynamicMainMenu"));
                Assert.True(otrosDynamicMenu.Items.Count >= 7);
                otrosMainWin.Close();
                otrosWm.ShutdownAllWindows();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
            finally
            {
                wm?.ShutdownAllWindows();
                ThemeService.ApplyTheme("neutro", isDark: false, "Moderna");
            }
        });
        sta.SetApartmentState(ApartmentState.STA);
        sta.Start();
        sta.Join();
        Assert.Null(threadEx);
    }

    [Fact]
    public void CrispRendering_ZeroBlurEffectsOnTextContainers_AndRenovadoStockBoxWorks()
    {
        string appRoot = Path.Combine(GetSourceRoot(), "FirewallControl.App");
        var xamlFiles = Directory.GetFiles(appRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToList();

        int windowCount = 0;
        foreach (string xaml in xamlFiles)
        {
            string raw = File.ReadAllText(xaml);
            Assert.False(raw.Contains("CornerRadius=\"99\"", StringComparison.OrdinalIgnoreCase),
                $"CornerRadius=\"99\" deforma píldoras en {xaml}");

            if (raw.Contains("<Window", StringComparison.Ordinal))
            {
                windowCount++;
                Assert.Contains("UseLayoutRounding=\"True\"", raw, StringComparison.Ordinal);
                Assert.Contains("SnapsToDevicePixels=\"True\"", raw, StringComparison.Ordinal);
                Assert.Contains("TextOptions.TextFormattingMode=\"Ideal\"", raw, StringComparison.Ordinal);
                Assert.Contains("TextOptions.TextRenderingMode=\"ClearType\"", raw, StringComparison.Ordinal);
            }
        }
        Assert.True(windowCount >= 42, $"Esperadas >=42 ventanas XAML con nitidez explícita, encontradas {windowCount}");

        string abmRenovadoXaml = File.ReadAllText(Path.Combine(appRoot, "Views", "AbmVentasAutoserviceWindow.xaml"));
        string itemsRenovadoXaml = File.ReadAllText(Path.Combine(appRoot, "Views", "ItemsFacturaVentasWindow.xaml"));
        Assert.DoesNotContain("SoftCardShadow", abmRenovadoXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DropShadowEffect", abmRenovadoXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SoftCardShadow", itemsRenovadoXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DropShadowEffect", itemsRenovadoXaml, StringComparison.Ordinal);

        Exception? threadEx = null;
        var sta = new Thread(() =>
        {
            WindowManagerService? wm = null;
            try
            {
                if (Application.Current == null)
                {
                    var app = new FirewallControl.App.App();
                    app.InitializeComponent();
                }

                foreach (string styleKey in new[] { "AtelierCardStyle", "HeroCardStyle" })
                {
                    var style = Assert.IsType<Style>(Application.Current!.TryFindResource(styleKey));
                    Assert.DoesNotContain(style.Setters.OfType<Setter>(), s => s.Property == UIElement.EffectProperty);
                }

                var cfg = new ConfigService(GetWorkspaceRoot());
                cfg.Preferences.EstiloPos = "Renovado";
                cfg.Preferences.PaletaColores = "Moderna";
                wm = CreateTestWindowManager(cfg);

                wm.SaltarACaja(1);
                PumpDispatcher();
                var salesWin = GetPrivateSalesWindow(wm, 1);
                var selWin = GetPrivateSelectorWindow(wm);

                Assert.NotNull(salesWin.FindName("BorderStockActualRenovado"));
                Assert.NotNull(selWin.FindName("BorderStockActualSelector"));
                Assert.True(salesWin.UseLayoutRounding);
                Assert.True(selWin.SnapsToDevicePixels);
                Assert.Equal(TextFormattingMode.Ideal, TextOptions.GetTextFormattingMode(salesWin));
                Assert.Equal(TextRenderingMode.ClearType, TextOptions.GetTextRenderingMode(selWin));

                var salesVm = wm.GetOpenSalesViewModel(1)!;
                Assert.False(salesVm.TieneStockActual);
                Assert.Equal(0m, salesVm.StockActualValor);
                Assert.False(salesVm.EsStockBajoOCero);

                salesVm.UpdateStockActualPreview(24.5m, "TOMATE PERITA");
                Assert.True(salesVm.TieneStockActual);
                Assert.Equal(24.5m, salesVm.StockActualValor);
                Assert.Equal("TOMATE PERITA", salesVm.StockActualProducto);
                Assert.False(salesVm.EsStockBajoOCero);

                salesVm.UpdateStockActualPreview(0m, "PAN FRANCÉS");
                Assert.True(salesVm.EsStockBajoOCero);

                salesVm.ClearStockActualPreview();
                Assert.False(salesVm.TieneStockActual);
                Assert.Equal("Sin ítem", salesVm.StockActualProducto);

                var prefWin = new ConfiguracionPreferenciasWindow(cfg);
                Assert.True(prefWin.UseLayoutRounding);
                Assert.Equal(TextRenderingMode.ClearType, TextOptions.GetTextRenderingMode(prefWin));
                prefWin.Close();

                var atelierDlg = new ColorAtelierDialog("neutro");
                Assert.True(atelierDlg.UseLayoutRounding);
                Assert.Equal(TextRenderingMode.ClearType, TextOptions.GetTextRenderingMode(atelierDlg));
                atelierDlg.Close();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
            finally
            {
                wm?.ShutdownAllWindows();
                ThemeService.ApplyTheme("neutro", isDark: false, "Moderna");
            }
        });
        sta.SetApartmentState(ApartmentState.STA);
        sta.Start();
        sta.Join();
        Assert.Null(threadEx);
    }

    [Fact]
    public void TemaSefiros_AppliesAntigravityPaletteToFoxClassic_AndSettingsReorganizationWorks()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"fc_sefiros_theme_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string liveIni = Path.Combine(GetWorkspaceRoot(), "FireWall.Ini");
        if (File.Exists(liveIni))
            File.Copy(liveIni, Path.Combine(tempDir, "FireWall.Ini"), overwrite: true);
        else
            File.WriteAllText(Path.Combine(tempDir, "FireWall.Ini"), "[Config]\r\nlcBaseDatos=firewallcontrol\r\n");

        Exception? threadEx = null;
        var sta = new Thread(() =>
        {
            WindowManagerService? wm = null;
            try
            {
                if (Application.Current == null)
                {
                    var app = new FirewallControl.App.App();
                    app.InitializeComponent();
                }

                var sefirosPal = ThemeService.GetPalette("sefiros");
                Assert.Equal("sefiros", sefirosPal.Key);
                Assert.Equal("Tema Sefiros", sefirosPal.Label);

                var swatch = ThemeService.CreatePaletteSwatchBrush(sefirosPal);
                var rainbow = Assert.IsType<LinearGradientBrush>(swatch);
                Assert.True(rainbow.GradientStops.Count >= 4, "El swatch del Tema Sefiros debe mostrar el degradado Antigravity.");

                // 1. Persistencia en ConfigService aislado
                var cfgPersist = new ConfigService(tempDir);
                cfgPersist.Preferences.TemaAtelier = "sefiros";
                cfgPersist.Preferences.PaletaColores = "ClasicaFox";
                cfgPersist.SavePreferences();
                var cfgReload = new ConfigService(tempDir);
                Assert.Equal("sefiros", cfgReload.Preferences.TemaAtelier);
                Assert.Equal("ClasicaFox", cfgReload.Preferences.PaletaColores);

                // 2. Tema Sefiros afecta incluso la versión Clásica FOX de ABMVENTAS e ITEMSFACTURAVENTAS
                ThemeService.ApplyTheme("neutro", isDark: false, "ClasicaFox");
                Assert.False(ThemeService.IsSefirosTheme);
                Assert.True(ThemeService.IsClassicFoxPalette);
                var classicWindowBg = Assert.IsType<SolidColorBrush>(Application.Current!.TryFindResource("PosWindowBgBrush")).Color;
                Assert.Equal(Color.FromRgb(0xF8, 0xF8, 0xF8), classicWindowBg);
                var classicSectionGrad = Assert.IsType<LinearGradientBrush>(Application.Current.TryFindResource("PosSectionBarBrush"));

                ThemeService.ApplyTheme("sefiros", isDark: false, "ClasicaFox");
                Assert.True(ThemeService.IsSefirosTheme);
                var sefirosLightBg = Assert.IsType<SolidColorBrush>(Application.Current.TryFindResource("PosWindowBgBrush")).Color;
                Assert.Equal(Color.FromRgb(0xF8, 0xF9, 0xFB), sefirosLightBg);
                var sefirosSectionGrad = Assert.IsType<LinearGradientBrush>(Application.Current.TryFindResource("PosSectionBarBrush"));
                var sefirosTopBanner = Assert.IsType<LinearGradientBrush>(Application.Current.TryFindResource("PosTopBannerBrush"));
                var sefirosFooterBar = Assert.IsType<LinearGradientBrush>(Application.Current.TryFindResource("PosFooterBarBrush"));
                var sefirosActiveBtn = Assert.IsType<LinearGradientBrush>(Application.Current.TryFindResource("PosButtonActiveBgBrush"));
                var sefirosClassicGrabarBg = Assert.IsType<LinearGradientBrush>(Application.Current.TryFindResource("PosClassicGrabarBgBrush"));
                var sefirosClassicSalirBg = Assert.IsType<LinearGradientBrush>(Application.Current.TryFindResource("PosButtonBgBrush"));
                var sefirosConfirmBtn = Assert.IsType<LinearGradientBrush>(Application.Current.TryFindResource("PosButtonPrimaryBgBrush"));
                var sefirosGlobalPrimary = Assert.IsType<LinearGradientBrush>(Application.Current.TryFindResource("ButtonPrimaryBgBrush"));
                Assert.NotEqual(classicSectionGrad.GradientStops[0].Color, sefirosSectionGrad.GradientStops[0].Color);
                Assert.Equal(4, sefirosTopBanner.GradientStops.Count);
                Assert.Equal(4, sefirosSectionGrad.GradientStops.Count);
                Assert.Equal(4, sefirosFooterBar.GradientStops.Count);
                Assert.Equal(3, sefirosActiveBtn.GradientStops.Count);
                Assert.Equal(sefirosClassicSalirBg.GradientStops[0].Color, sefirosClassicGrabarBg.GradientStops[0].Color);
                Assert.Equal(sefirosClassicSalirBg.GradientStops[1].Color, sefirosClassicGrabarBg.GradientStops[1].Color);
                Assert.Equal(4, sefirosConfirmBtn.GradientStops.Count);
                Assert.Equal(4, sefirosGlobalPrimary.GradientStops.Count);

                ThemeService.ApplyTheme("sefiros", isDark: true, "ClasicaFox");
                Assert.True(ThemeService.IsSefirosTheme);
                Assert.True(ThemeService.CurrentIsDark);
                var sefirosDarkBg = Assert.IsType<SolidColorBrush>(Application.Current.TryFindResource("PosWindowBgBrush")).Color;
                Assert.Equal(Color.FromRgb(0x11, 0x13, 0x18), sefirosDarkBg);

                // Restaurar FOX tradicional al volver a Neutro + ClasicaFox
                ThemeService.ApplyTheme("neutro", isDark: false, "ClasicaFox");
                Assert.False(ThemeService.IsSefirosTheme);
                var restoredClassicBg = Assert.IsType<SolidColorBrush>(Application.Current.TryFindResource("PosWindowBgBrush")).Color;
                Assert.Equal(Color.FromRgb(0xF8, 0xF8, 0xF8), restoredClassicBg);

                // 3. Abrir ventanas Clásicas FOX bajo Tema Sefiros sin errores
                ThemeService.ApplyTheme("sefiros", isDark: false, "ClasicaFox");
                cfgReload.Preferences.EstiloPos = "Clasico";
                wm = CreateTestWindowManager(cfgReload);
                wm.SaltarACaja(1);
                PumpDispatcher();
                Assert.IsType<AbmVentasClassicWindow>(GetPrivateSalesWindow(wm, 1));
                Assert.IsType<ItemsFacturaVentasClassicWindow>(GetPrivateSelectorWindow(wm));

                // 4. Reorganización lógica de ConfiguracionPreferenciasWindow + ViewApariencia (tab 9)
                var prefWin = new ConfiguracionPreferenciasWindow(cfgReload);
                Assert.NotNull(prefWin.FindName("ViewApariencia"));
                Assert.NotNull(prefWin.FindName("BannerSefirosOverrideInfo"));
                Assert.NotNull(prefWin.FindName("BtnActivateSefirosTheme"));
                Assert.NotNull(prefWin.FindName("ChkTemaOscuroApariencia"));
                Assert.NotNull(prefWin.FindName("RbEstiloRenovado"));
                Assert.NotNull(prefWin.FindName("RbEstiloClasico"));
                Assert.NotNull(prefWin.FindName("RbPaletaModerna"));
                Assert.NotNull(prefWin.FindName("RbPaletaClasicaFox"));
                Assert.NotNull(prefWin.FindName("ChkPrefNoControlarStockGlobal"));
                Assert.NotNull(prefWin.FindName("ChkPrefItemFacturaModal"));
                Assert.NotNull(prefWin.FindName("TxtPrefLimiteCantidad"));
                Assert.NotNull(prefWin.FindName("TxtPrefPrecioMinimo"));
                Assert.NotNull(prefWin.FindName("TxtPrefPrecioMaximo"));

                for (int tab = 0; tab <= 9; tab++)
                {
                    prefWin.Configure(tab);
                }

                prefWin.Configure(9);
                var viewApariencia = Assert.IsType<ScrollViewer>(prefWin.FindName("ViewApariencia"));
                var viewPreferencias = Assert.IsType<ScrollViewer>(prefWin.FindName("ViewPreferencias"));
                Assert.Equal(Visibility.Visible, viewApariencia.Visibility);
                Assert.Equal(Visibility.Collapsed, viewPreferencias.Visibility);

                prefWin.Configure(1);
                Assert.Equal(Visibility.Visible, viewPreferencias.Visibility);
                Assert.Equal(Visibility.Collapsed, viewApariencia.Visibility);
                prefWin.Close();

                // 5. ColorAtelierDialog muestra 8 tarjetas (incluyendo Tema Sefiros)
                var atelierDlg = new ColorAtelierDialog("sefiros");
                Assert.Equal("sefiros", atelierDlg.SelectedThemeKey);
                var palGrid = Assert.IsType<System.Windows.Controls.Primitives.UniformGrid>(atelierDlg.FindName("PalettesGrid"));
                Assert.Equal(8, palGrid.Children.Count);
                atelierDlg.Close();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
            finally
            {
                wm?.ShutdownAllWindows();
                ThemeService.ApplyTheme("neutro", isDark: false, "Moderna");
            }
        });
        sta.SetApartmentState(ApartmentState.STA);
        sta.Start();
        sta.Join();
        try { Directory.Delete(tempDir, recursive: true); } catch { }
        Assert.Null(threadEx);
    }

    [Fact]
    public void SplashPresentation_HasProgressiveSStroke_SixGeminiSparkles_AndCompletesCleanly()
    {
        Exception? threadEx = null;
        var sta = new Thread(() =>
        {
            SefirosSplashWindow? splash = null;
            bool prevSuppress = SefirosStartupSoundService.SuppressPlayback;
            try
            {
                SefirosStartupSoundService.SuppressPlayback = true;
                if (Application.Current == null)
                {
                    var app = new FirewallControl.App.App();
                    app.InitializeComponent();
                }

                ThemeService.ApplyTheme("sefiros", isDark: false, "Moderna");

                splash = new SefirosSplashWindow
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -3200,
                    Top = -3200,
                    Topmost = false
                };

                // Atributos de nitidez ClearType y sombra aislada (sin Effect sobre el contenedor con texto)
                Assert.True(splash.UseLayoutRounding);
                Assert.True(splash.SnapsToDevicePixels);
                Assert.Equal(TextFormattingMode.Ideal, TextOptions.GetTextFormattingMode(splash));
                Assert.Equal(TextRenderingMode.ClearType, TextOptions.GetTextRenderingMode(splash));

                var cardRoot = Assert.IsType<Border>(splash.FindName("CardRoot"));
                Assert.Null(cardRoot.Effect);
                Assert.True(splash.Width >= 520d, $"Splash ampliado debe medir >= 520 DIPs de ancho, actual: {splash.Width}");
                Assert.True(splash.Height >= 420d, $"Splash ampliado debe medir >= 420 DIPs de alto, actual: {splash.Height}");
                Assert.IsType<Border>(splash.FindName("SquircleBadge"));
                Assert.IsType<Grid>(splash.FindName("PrismAura"));
                Assert.IsType<Grid>(splash.FindName("CometTip"));
                Assert.IsType<Grid>(splash.FindName("EndStarGroup"));
                Assert.IsType<System.Windows.Shapes.Ellipse>(splash.FindName("StarRipple"));
                Assert.IsType<System.Windows.Shapes.Path>(splash.FindName("StarAstroid"));
                Assert.IsType<TextBlock>(splash.FindName("TxtSplashTitle"));
                Assert.IsType<TextBlock>(splash.FindName("TxtSplashSubtitle"));
                Assert.IsType<Border>(splash.FindName("ProgressFill"));

                var pathS = Assert.IsType<System.Windows.Shapes.Path>(splash.FindName("PathSStroke"));
                Assert.IsType<System.Windows.Shapes.Path>(splash.FindName("PathSWarmBloom"));
                Assert.IsType<System.Windows.Shapes.Path>(splash.FindName("PathSGlowInner"));
                Assert.IsType<System.Windows.Shapes.Path>(splash.FindName("PathSGlowOuter"));
                var pathSheen = Assert.IsType<System.Windows.Shapes.Path>(splash.FindName("PathSSheen"));
                Assert.Equal(PenLineCap.Round, pathS.StrokeStartLineCap);
                Assert.Equal(PenLineCap.Round, pathS.StrokeEndLineCap);

                double pathLen = SefirosSplashWindow.ComputePathLength(pathS.Data);
                Assert.True(pathLen > 150d, $"La geometría de la 'S' debe medir > 150 DIPs, medido: {pathLen}");

                for (int i = 1; i <= 6; i++)
                {
                    var sparkle = Assert.IsType<System.Windows.Shapes.Path>(splash.FindName($"SparkleGemini{i}"));
                    Assert.NotNull(sparkle.Data);
                    Assert.True(SefirosSplashWindow.ComputePathLength(sparkle.Data) > 40d, $"SparkleGemini{i} debe tener contorno Gemini válido.");
                }

                int readyCount = 0;
                int completedCount = 0;
                splash.PresentationReadyToTransition += () => readyCount++;
                splash.PresentationCompleted += () => completedCount++;

                splash.Show();
                PumpDispatcher(120);

                // Al cargarse, PathSStroke ya tiene calculado StrokeDashArray [dashUnits, dashUnits+4] para el dibujado progresivo
                Assert.Equal(2, pathS.StrokeDashArray.Count);
                Assert.True(pathS.StrokeDashArray[0] > 5.0);
                Assert.True(pathS.StrokeDashArray[1] > pathS.StrokeDashArray[0]);

                // Verificar en t = 750ms que TODAS las animaciones de Transform/Cometa/Aura realmente corren (sin Freezable congelado)
                splash.SeekIntroForVerification(TimeSpan.FromMilliseconds(750));
                var topAccentScale = Assert.IsType<ScaleTransform>(splash.FindName("TopAccentScale"));
                var progressScale = Assert.IsType<ScaleTransform>(splash.FindName("ProgressScale"));
                var prismAura = Assert.IsType<Grid>(splash.FindName("PrismAura"));
                var prismRotate = Assert.IsType<RotateTransform>(splash.FindName("PrismRotate"));
                var cometTip = Assert.IsType<Grid>(splash.FindName("CometTip"));
                var cometTranslate = Assert.IsType<TranslateTransform>(splash.FindName("CometTranslate"));
                Assert.True(topAccentScale.ScaleX > 0.2, $"TopAccentScale debe animarse en t=750ms, actual: {topAccentScale.ScaleX}");
                Assert.True(progressScale.ScaleX > 0.05, $"ProgressScale debe animarse en t=750ms, actual: {progressScale.ScaleX}");
                Assert.True(prismAura.Opacity > 0.5, $"PrismAura debe ser visible en t=750ms, actual: {prismAura.Opacity}");
                Assert.True(prismRotate.Angle > 15.0, $"PrismRotate debe girar en t=750ms, actual: {prismRotate.Angle}");
                Assert.True(cometTip.Opacity > 0.7, $"CometTip debe brillar en t=750ms, actual: {cometTip.Opacity}");
                Assert.True(Math.Abs(cometTranslate.X - 85.0) > 1.0 || Math.Abs(cometTranslate.Y - 32.0) > 1.0,
                    $"CometTranslate debe recorrer la S en t=750ms, actual: ({cometTranslate.X}, {cometTranslate.Y})");

                // Verificar en t = 1520ms: destello especular sobre la S, estrella final + onda expansiva y estrellas Gemini alrededor
                splash.SeekIntroForVerification(TimeSpan.FromMilliseconds(1520));
                var endStarGroup = Assert.IsType<Grid>(splash.FindName("EndStarGroup"));
                var starAstroid = Assert.IsType<System.Windows.Shapes.Path>(splash.FindName("StarAstroid"));
                var starRippleScale = Assert.IsType<ScaleTransform>(splash.FindName("StarRippleScale"));
                var sparkle1 = Assert.IsType<System.Windows.Shapes.Path>(splash.FindName("SparkleGemini1"));
                var sparkle4 = Assert.IsType<System.Windows.Shapes.Path>(splash.FindName("SparkleGemini4"));
                Assert.True(pathSheen.Opacity > 0.3, $"Destello especular PathSSheen debe ser visible en t=1520ms, actual: {pathSheen.Opacity}");
                Assert.True(endStarGroup.Opacity > 0.7, $"Estrella final EndStarGroup debe brillar en t=1520ms, actual: {endStarGroup.Opacity}");
                Assert.True(((ScaleTransform)((TransformGroup)starAstroid.RenderTransform).Children[0]).ScaleX > 0.9, "StarAstroid debe escalar > 0.9x en t=1520ms");
                Assert.True(starRippleScale.ScaleX > 0.9, $"Onda expansiva StarRipple debe crecer > 0.9x en t=1520ms, actual: {starRippleScale.ScaleX}");
                Assert.True(sparkle1.Opacity > 0.5 && ((ScaleTransform)((TransformGroup)sparkle1.RenderTransform).Children[0]).ScaleX > 0.6,
                    "SparkleGemini1 alrededor del emblema debe estar visible en t=1520ms");
                Assert.True(sparkle4.Opacity > 0.5 && ((ScaleTransform)((TransformGroup)sparkle4.RenderTransform).Children[0]).ScaleX > 0.6,
                    "SparkleGemini4 alrededor del emblema debe estar visible en t=1520ms");

                // Omitir presentación ejecuta transición suave e idempotente (1 evento Ready, 1 Completed)
                splash.SkipPresentation();
                PumpDispatcher(220);

                Assert.Equal(1, readyCount);
                Assert.Equal(1, completedCount);
                Assert.True(splash.IsPresentationCompleted);

                splash.Close();
                PumpDispatcher(40);
                Assert.Equal(1, readyCount);
                Assert.Equal(1, completedCount);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
            finally
            {
                if (splash != null)
                {
                    try { splash.Close(); } catch { }
                }
                SefirosStartupSoundService.SuppressPlayback = prevSuppress;
                ThemeService.ApplyTheme("neutro", isDark: false, "Moderna");
            }
        });
        sta.SetApartmentState(ApartmentState.STA);
        sta.Start();
        sta.Join();
        Assert.Null(threadEx);
    }

    [Fact]
    public void GenesSelector_FillsFullVerticalWorkArea_AndEnlargesItemIconsLikeFox()
    {
        Exception? threadEx = null;
        var sta = new Thread(() =>
        {
            WindowManagerService? wm = null;
            GenesSelectorWindow? genesWin = null;
            try
            {
                if (Application.Current == null)
                {
                    var app = new FirewallControl.App.App();
                    app.InitializeComponent();
                }

                var cfg = new ConfigService(GetWorkspaceRoot());
                var conn = new MySqlConnectionFactory(cfg);
                wm = CreateTestWindowManager(cfg);

                genesWin = new GenesSelectorWindow(
                    new VfpDbfService(),
                    cfg,
                    new ProductRepository(conn),
                    wm);

                Rect work = SystemParameters.WorkArea;
                if (work.Width <= 200 || work.Height <= 200)
                    work = new Rect(0, 0, 1366, 728);

                Assert.Equal(WindowStartupLocation.Manual, genesWin.WindowStartupLocation);
                Assert.InRange(genesWin.Top, Math.Round(work.Top) - 2, Math.Round(work.Top) + 2);
                Assert.InRange(genesWin.Height, Math.Round(work.Height) - 2, Math.Round(work.Height) + 2);

                genesWin.Configure(1);

                Assert.True(genesWin.CurrentCardHeight >= 140d, $"La tarjeta debe medir >= 140 DIPs, medido: {genesWin.CurrentCardHeight}");
                Assert.True(genesWin.CurrentIconHeight >= 84d, $"El ícono de ítem debe medir >= 84 DIPs como en Fox, medido: {genesWin.CurrentIconHeight}");

                Assert.NotNull(genesWin.FindName("BtnGen1"));
                Assert.NotNull(genesWin.FindName("BtnGen4"));
                Assert.NotNull(genesWin.FindName("BtnNuevo"));
                Assert.NotNull(genesWin.FindName("BtnCerrar"));

                var grid = Assert.IsType<System.Windows.Controls.Primitives.UniformGrid>(genesWin.FindName("CardsUniformGrid"));
                if (TestPathHelper.HasLocalLegacyDbfs())
                {
                    Assert.True(grid.Children.Count > 0, "Gen 1 debe cargar productos desde genes_productos.dbf.");

                    var firstCard = Assert.IsType<Border>(grid.Children[0]);
                    Assert.True(firstCard.Height >= 140d);
                    var innerGrid = Assert.IsType<Grid>(firstCard.Child);
                    Assert.True(innerGrid.RowDefinitions[0].Height.Value >= 90d);

                    if (innerGrid.Children[0] is Image firstImg)
                    {
                        Assert.True(firstImg.Height >= 84d, $"Imagen debe medir >= 84 DIPs, actual: {firstImg.Height}");
                        Assert.Equal(BitmapScalingMode.HighQuality, RenderOptions.GetBitmapScalingMode(firstImg));
                    }
                    else if (innerGrid.Children[0] is Border firstCircle)
                    {
                        Assert.True(firstCircle.Height >= 60d);
                    }
                }

                genesWin.Show();
                PumpDispatcher(120);

                Assert.InRange(genesWin.Top, Math.Round(work.Top) - 2, Math.Round(work.Top) + 2);
                Assert.InRange(genesWin.Height, Math.Round(work.Height) - 2, Math.Round(work.Height) + 2);
                Assert.True(genesWin.CurrentCardHeight >= 140d);
                Assert.True(genesWin.CurrentIconHeight >= 84d);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
            finally
            {
                if (genesWin != null)
                {
                    try { genesWin.Close(); } catch { }
                }
                wm?.ShutdownAllWindows();
                ThemeService.ApplyTheme("neutro", isDark: false, "Moderna");
            }
        });
        sta.SetApartmentState(ApartmentState.STA);
        sta.Start();
        sta.Join();
        Assert.Null(threadEx);
    }

    [Fact]
    public void SefirosReleaseReadiness_AssembliesBrandingAndStartupSound_AreValid()
    {
        // 1. Nombres exactos de ensamblados finales: Sefiros.App, Sefiros.Core, Sefiros.Infrastructure
        Assert.Equal("Sefiros.App", typeof(FirewallControl.App.App).Assembly.GetName().Name);
        Assert.Equal("Sefiros.Core", typeof(SaleEntity).Assembly.GetName().Name);
        Assert.Equal("Sefiros.Infrastructure", typeof(ConfigService).Assembly.GetName().Name);

        // 2. Ninguna vista ni referencia UI menciona "Antigravity" o "Respaldo Firewall Control"
        string viewsDir = Path.Combine(GetSourceRoot(), "FirewallControl.App", "Views");
        foreach (string file in Directory.GetFiles(viewsDir, "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            string text = File.ReadAllText(file);
            Assert.False(text.Contains("Antigravity", StringComparison.OrdinalIgnoreCase),
                $"Texto 'Antigravity' aún presente en {file}");
            Assert.False(text.Contains("Respaldo Firewall Control", StringComparison.OrdinalIgnoreCase),
                $"Texto 'Respaldo Firewall Control' aún presente en {file}");
        }

        // 3. Síntesis orgánica de audio de inicio (~2.85s): WAV PCM estéreo 44100 Hz 16-bit con fondo grave suave y cierre relajante
        byte[] wav = SefirosStartupSoundService.BuildStartupChimeWavBytes();
        Assert.True(wav.Length > 450000, $"Audio de inicio demasiado corto: {wav.Length} bytes");
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal("fmt ", System.Text.Encoding.ASCII.GetString(wav, 12, 4));
        Assert.Equal(2, BitConverter.ToInt16(wav, 22)); // Stereo
        Assert.Equal(44100, BitConverter.ToInt32(wav, 24)); // Sample rate
        Assert.Equal(16, BitConverter.ToInt16(wav, 34)); // Bits per sample
        Assert.Equal("data", System.Text.Encoding.ASCII.GetString(wav, 36, 4));

        int dataBytes = BitConverter.ToInt32(wav, 40);
        Assert.Equal(wav.Length - 44, dataBytes);

        // Analizar energía grave inicial (0.2s..0.9s) vs presencia armónica del cierre estrella (1.30s..2.30s)
        double maxAbs = 0d;
        double deepSumSq = 0d;
        int deepCount = 0;
        double earlyHfSumSq = 0d;
        double lateHfSumSq = 0d;
        int lateCount = 0;
        double prevSample = 0d;

        int totalFrames = dataBytes / 4;
        for (int i = 0; i < totalFrames; i++)
        {
            short l = BitConverter.ToInt16(wav, 44 + (i * 4));
            short r = BitConverter.ToInt16(wav, 44 + (i * 4) + 2);
            double mono = ((l + r) * 0.5) / 32768.0;
            double abs = Math.Abs(mono);
            if (abs > maxAbs) maxAbs = abs;

            double diff = mono - prevSample;
            prevSample = mono;
            double t = i / 44100.0;

            if (t >= 0.20 && t <= 0.90)
            {
                deepSumSq += mono * mono;
                earlyHfSumSq += diff * diff;
                deepCount++;
            }
            else if (t >= 1.30 && t <= 2.30)
            {
                lateHfSumSq += diff * diff;
                lateCount++;
            }
        }

        double deepRms = Math.Sqrt(deepSumSq / Math.Max(1, deepCount));
        double earlyHfRms = Math.Sqrt(earlyHfSumSq / Math.Max(1, deepCount));
        double lateHfRms = Math.Sqrt(lateHfSumSq / Math.Max(1, lateCount));

        Assert.InRange(maxAbs, 0.20, 0.95);
        Assert.True(deepRms > 0.04, $"El tramo grave inicial debe tener presencia acústica (RMS={deepRms:F4}).");
        Assert.True(lateHfRms > earlyHfRms * 1.15,
            $"El cierre armónico con estrella final debe elevar suavemente las frecuencias medias-altas sin perder suavidad (early={earlyHfRms:F5}, late={lateHfRms:F5}).");
    }

    private static void PumpDispatcher(int delayMs = 60)
    {
        var frame = new DispatcherFrame();
        System.Threading.Tasks.Task.Delay(delayMs).ContinueWith(_ => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private static Window GetPrivateSelectorWindow(WindowManagerService wm)
    {
        var field = typeof(WindowManagerService).GetField("_itemsSelectorWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var val = field?.GetValue(wm);
        Assert.NotNull(val);
        return ((FirewallControl.App.Services.IItemsSelectorWindow)val!).Window;
    }

    private static Window GetPrivateSalesWindow(WindowManagerService wm, int caja)
    {
        var field = typeof(WindowManagerService).GetField("_salesWindowsByCaja", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var dict = (System.Collections.Generic.Dictionary<int, FirewallControl.App.Services.ISalesPosWindow>)field!.GetValue(wm)!;
        return dict[caja].Window;
    }
}
