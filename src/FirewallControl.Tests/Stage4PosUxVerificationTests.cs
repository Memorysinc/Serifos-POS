using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using FirewallControl.App.Services;
using FirewallControl.App.ViewModels;
using FirewallControl.App.Views;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Infrastructure.Database;
using FirewallControl.Infrastructure.Repositories;
using FirewallControl.Infrastructure.Services;
using Xunit;

namespace FirewallControl.Tests;

public class Stage4PosUxVerificationTests
{
    private static string GetWorkspaceRoot() => TestPathHelper.GetWorkspaceRootOrTemp();

    private static WindowManagerService CreateTestWindowManager(ConfigService cfg)
    {
        var conn = new MySqlConnectionFactory(cfg);
        var dbf = new VfpDbfService();
        var prodRepo = new ProductRepository(conn);
        var salesRepo = new SalesRepository(conn);
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
    public void FiveCajas_ShareIdenticalCenteredBounds_OnlyActiveVisible_AndPreserveIndependentCarts()
    {
        Exception? threadEx = null;
        var staThread = new Thread(() =>
        {
            WindowManagerService? wm = null;
            try
            {
                if (Application.Current == null)
                {
                    var app = new FirewallControl.App.App();
                    app.InitializeComponent();
                }

                foreach (string style in new[] { "Clasico", "Renovado" })
                {
                    var cfg = new ConfigService(GetWorkspaceRoot());
                    cfg.Preferences.EstiloPos = style;
                    cfg.Preferences.PaletaColores = "ClasicaFox";

                    wm = CreateTestWindowManager(cfg);

                    for (int caja = 1; caja <= 5; caja++)
                    {
                        wm.SaltarACaja(caja);
                        var vm = wm.GetOpenSalesViewModel(caja);
                        Assert.NotNull(vm);

                        // Wait for initial async InitializeNewSaleAsync DB load on dispatcher so it completes before setting test fixture customer
                        var frame = new System.Windows.Threading.DispatcherFrame();
                        System.Threading.Tasks.Task.Delay(120).ContinueWith(_ => frame.Continue = false);
                        System.Windows.Threading.Dispatcher.PushFrame(frame);

                        vm!.NombreCliente = $"CLIENTE CAJA {caja}";
                        SalesCalculationEngine.ApplyItemOperation(vm.Items, new SaleItemEntity
                        {
                            IdProducto = 100 + caja,
                            Codigo = $"COD{caja}",
                            Descripcion = $"PRODUCTO CAJA {caja}",
                            Cantidad = caja,
                            PreUnitario = 10000m,
                            Iva = 1
                        }, "A", 0);
                        vm.SumarProductos();
                        wm.GetItemsSelectorViewModel()?.ActualizarEspejo();
                    }

                    // Criteria 4 & No duplicates: exactly 5 singletons with independent carts
                    Assert.Equal(5, wm.GetAllOpenSalesViewModels().Count);

                    // Criteria 1 & 2: Clasico shares exact centered position/dimensions across Cajas 1..5;
                    // Renovado cascades Cajas 1..5 ("uno detrás de otro") with compact size.
                    Rect? firstBounds = wm.GetSalesWindowBounds(1);
                    Assert.NotNull(firstBounds);
                    if (style == "Clasico")
                    {
                        for (int caja = 2; caja <= 5; caja++)
                        {
                            Rect? b = wm.GetSalesWindowBounds(caja);
                            Assert.NotNull(b);
                            Assert.Equal(firstBounds!.Value.Left, b!.Value.Left, 1);
                            Assert.Equal(firstBounds.Value.Top, b.Value.Top, 1);
                            Assert.Equal(firstBounds.Value.Width, b.Value.Width, 1);
                            Assert.Equal(firstBounds.Value.Height, b.Value.Height, 1);
                        }
                    }
                    else
                    {
                        Rect prev = firstBounds!.Value;
                        for (int caja = 2; caja <= 5; caja++)
                        {
                            Rect? b = wm.GetSalesWindowBounds(caja);
                            Assert.NotNull(b);
                            Assert.True(b!.Value.Left > prev.Left, $"Renovado Caja {caja} Left={b.Value.Left} debe ser > Caja {caja - 1} Left={prev.Left}");
                            Assert.True(b.Value.Top > prev.Top, $"Renovado Caja {caja} Top={b.Value.Top} debe ser > Caja {caja - 1} Top={prev.Top}");
                            Assert.Equal(prev.Width, b.Value.Width, 1);
                            Assert.Equal(prev.Height, b.Value.Height, 1);
                            prev = b.Value;
                        }
                    }

                    // Criteria 3 & 4: switch across Cajas -> Clasico hides inactive; Renovado keeps all open Cajas stacked behind
                    foreach (int target in new[] { 3, 1, 5, 2, 4, 1 })
                    {
                        wm.SaltarACaja(target);
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                            System.Windows.Threading.DispatcherPriority.Background,
                            new Action(() => { }));

                        Assert.Equal(target, wm.ActiveSalesCaja);
                        Assert.Equal($"CLIENTE CAJA {target}", wm.GetOpenSalesViewModel(target)!.NombreCliente);
                        Assert.Equal(target * 10000m, wm.GetItemsSelectorViewModel()!.TotalVentaEspejo);

                        for (int k = 1; k <= 5; k++)
                        {
                            bool actualVis = wm.IsSalesWindowVisible(k);
                            bool expectedVis = style == "Clasico" ? (k == target) : true;
                            Assert.True(actualVis == expectedVis,
                                $"Style={style}, target={target}, k={k}: expected={expectedVis}, actual={actualVis}, active={wm.ActiveSalesCaja}");
                            var vmK = wm.GetOpenSalesViewModel(k)!;
                            Assert.Single(vmK.Items);
                            Assert.Equal(k * 10000m, vmK.TotalVenta);
                        }
                    }

                    wm.ShutdownAllWindows();
                    wm = null;
                }
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

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();
        Assert.Null(threadEx);
    }

    [Fact]
    public void HotSwap_RenovadoAndClasico_SwitchesCascadeAndCenteredWithoutLosingCarts()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"fc_hotswap_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string liveIni = Path.Combine(GetWorkspaceRoot(), "FireWall.Ini");
        if (File.Exists(liveIni))
            File.Copy(liveIni, Path.Combine(tempDir, "FireWall.Ini"), overwrite: true);
        else
            File.WriteAllText(Path.Combine(tempDir, "FireWall.Ini"), "[Config]\r\nlcBaseDatos=firewallcontrol\r\n");

        Exception? threadEx = null;
        var staThread = new Thread(() =>
        {
            WindowManagerService? wm = null;
            try
            {
                if (Application.Current == null)
                {
                    var app = new FirewallControl.App.App();
                    app.InitializeComponent();
                }

                var cfg = new ConfigService(tempDir);
                cfg.Preferences.EstiloPos = "Renovado";
                cfg.Preferences.PaletaColores = "Moderna";
                wm = CreateTestWindowManager(cfg);

                for (int caja = 1; caja <= 3; caja++)
                {
                    wm.SaltarACaja(caja);
                    var vm = wm.GetOpenSalesViewModel(caja)!;
                    SalesCalculationEngine.ApplyItemOperation(vm.Items, new SaleItemEntity
                    {
                        IdProducto = 200 + caja,
                        Codigo = $"HS{caja}",
                        Descripcion = $"PROD HOTSWAP {caja}",
                        Cantidad = caja,
                        PreUnitario = 5000m,
                        Iva = 1
                    }, "A", 0);
                    vm.SumarProductos();
                }

                // In Renovado: Cajas 1..3 cascaded and all visible
                Assert.True(wm.GetSalesWindowBounds(2)!.Value.Left > wm.GetSalesWindowBounds(1)!.Value.Left);
                Assert.True(wm.IsSalesWindowVisible(1));
                Assert.True(wm.IsSalesWindowVisible(2));
                Assert.True(wm.IsSalesWindowVisible(3));

                // Hot-swap to Clasico -> centered shared bounds, only active Caja (3) visible, carts preserved
                Assert.True(wm.ApplyPosStyle("Clasico"));
                Rect b1C = wm.GetSalesWindowBounds(1)!.Value;
                Rect b2C = wm.GetSalesWindowBounds(2)!.Value;
                Rect b3C = wm.GetSalesWindowBounds(3)!.Value;
                Assert.InRange(b3C.Left, b1C.Left - 1.5, b1C.Left + 1.5);
                Assert.InRange(b3C.Top, b1C.Top - 1.5, b1C.Top + 1.5);
                Assert.InRange(b3C.Width, b2C.Width - 1.5, b2C.Width + 1.5);
                Assert.False(wm.IsSalesWindowVisible(1));
                Assert.False(wm.IsSalesWindowVisible(2));
                Assert.True(wm.IsSalesWindowVisible(3));
                Assert.Equal(15000m, wm.GetOpenSalesViewModel(3)!.TotalVenta);

                // Activating Caja 1 in Clasico hides Caja 3 and keeps identical centered bounds
                wm.SaltarACaja(1);
                Rect b1CShown = wm.GetSalesWindowBounds(1)!.Value;
                Assert.InRange(b1CShown.Left, b3C.Left - 1.5, b3C.Left + 1.5);
                Assert.InRange(b1CShown.Top, b3C.Top - 1.5, b3C.Top + 1.5);
                Assert.True(wm.IsSalesWindowVisible(1));
                Assert.False(wm.IsSalesWindowVisible(3));

                // Hot-swap back to Renovado -> cascaded bounds & all 3 visible again
                Assert.True(wm.ApplyPosStyle("Renovado"));
                Assert.True(wm.GetSalesWindowBounds(2)!.Value.Left > wm.GetSalesWindowBounds(1)!.Value.Left);
                Assert.True(wm.GetSalesWindowBounds(3)!.Value.Top > wm.GetSalesWindowBounds(2)!.Value.Top);
                Assert.True(wm.IsSalesWindowVisible(1));
                Assert.True(wm.IsSalesWindowVisible(2));
                Assert.True(wm.IsSalesWindowVisible(3));
                Assert.Equal(5000m, wm.GetOpenSalesViewModel(1)!.TotalVenta);
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

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();
        try { Directory.Delete(tempDir, recursive: true); } catch { }
        Assert.Null(threadEx);
    }

    [Fact]
    public void ColorPalette_And_PosStyle_AreIndependent_AllFourCombinations_AndPersist()
    {
        // Criteria 7: Persistence across restart using isolated ConfigService directory
        string tempDir = Path.Combine(Path.GetTempPath(), $"fc_palette_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "FireWall.Ini"), "[Config]\r\nlcBaseDatos=firewallcontrol\r\n");

        try
        {
            var cfg1 = new ConfigService(tempDir);
            Assert.Equal("Renovado", cfg1.Preferences.EstiloPos);
            Assert.Equal("Moderna", cfg1.Preferences.PaletaColores);

            cfg1.Preferences.EstiloPos = "Clasico";
            cfg1.Preferences.PaletaColores = "ClasicaFox";
            cfg1.SavePreferences();

            var cfgReload1 = new ConfigService(tempDir);
            Assert.Equal("Clasico", cfgReload1.Preferences.EstiloPos);
            Assert.Equal("ClasicaFox", cfgReload1.Preferences.PaletaColores);

            // Independence check: changing PaletaColores does not alter EstiloPos
            cfgReload1.Preferences.PaletaColores = "Moderna";
            cfgReload1.SavePreferences();

            var cfgReload2 = new ConfigService(tempDir);
            Assert.Equal("Clasico", cfgReload2.Preferences.EstiloPos);
            Assert.Equal("Moderna", cfgReload2.Preferences.PaletaColores);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }

        // Criteria 4 & 6: Verify all 4 live combinations on open POS windows
        Exception? threadEx = null;
        var staThread = new Thread(() =>
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
                wm = CreateTestWindowManager(cfg);

                var salesVm = wm.OpenOrActivateSalesWindow(1);
                wm.OpenOrActivateItemsSelector(salesVm, "A", 0, null);

                // 1) Diseño Clásico + Paleta Clásica FOX (exclusiva para ABMVENTAS/ITEMSFACTURAVENTAS; _FONDO permanece Atelier)
                wm.ApplyPosStyle("Clasico");
                ThemeService.ApplyTheme("neutro", isDark: false, "ClasicaFox");
                Assert.Equal(typeof(AbmVentasClassicWindow), wm.GetOpenSalesWindowType(1));
                Assert.Equal(typeof(ItemsFacturaVentasClassicWindow), wm.GetItemsSelectorWindowType());
                Assert.True(ThemeService.IsClassicFoxPalette);
                Assert.Equal(Color.FromRgb(0xFF, 0x00, 0x00), ((SolidColorBrush)Application.Current!.TryFindResource("PosBigTotalFgBrush")).Color);
                Assert.Equal(Color.FromRgb(0xF8, 0xF8, 0xF8), ((SolidColorBrush)Application.Current.TryFindResource("PosWindowBgBrush")).Color);
                Assert.Equal(Color.FromRgb(0xF8, 0xF7, 0xF5), ((SolidColorBrush)Application.Current.TryFindResource("ScreenBgBrush")).Color);
                Assert.Equal(Colors.White, ((SolidColorBrush)Application.Current.TryFindResource("CardBgBrush")).Color);

                // 2) Diseño Clásico + Paleta Moderna
                ThemeService.ApplyTheme("neutro", isDark: false, "Moderna");
                Assert.Equal(typeof(AbmVentasClassicWindow), wm.GetOpenSalesWindowType(1));
                Assert.False(ThemeService.IsClassicFoxPalette);
                Assert.Equal(Color.FromRgb(0x05, 0x96, 0x69), ((SolidColorBrush)Application.Current.TryFindResource("PosBigTotalFgBrush")).Color);
                Assert.Equal(Color.FromRgb(0xF8, 0xF7, 0xF5), ((SolidColorBrush)Application.Current.TryFindResource("ScreenBgBrush")).Color);

                // 3) Diseño Renovado + Paleta Clásica FOX (exclusiva para POS; _FONDO permanece Atelier)
                wm.ApplyPosStyle("Renovado");
                ThemeService.ApplyTheme("neutro", isDark: false, "ClasicaFox");
                Assert.Equal(typeof(AbmVentasAutoserviceWindow), wm.GetOpenSalesWindowType(1));
                Assert.Equal(typeof(ItemsFacturaVentasWindow), wm.GetItemsSelectorWindowType());
                Assert.True(ThemeService.IsClassicFoxPalette);
                Assert.Equal(Color.FromRgb(0xFF, 0x00, 0x00), ((SolidColorBrush)Application.Current.TryFindResource("PosKpiTotalFgBrush")).Color);
                Assert.Equal(Color.FromRgb(0xF8, 0xF7, 0xF5), ((SolidColorBrush)Application.Current.TryFindResource("ScreenBgBrush")).Color);

                // 4) Diseño Renovado + Paleta Moderna
                ThemeService.ApplyTheme("neutro", isDark: false, "Moderna");
                Assert.Equal(typeof(AbmVentasAutoserviceWindow), wm.GetOpenSalesWindowType(1));
                Assert.False(ThemeService.IsClassicFoxPalette);
                Assert.Equal(Color.FromRgb(0xF8, 0xF7, 0xF5), ((SolidColorBrush)Application.Current.TryFindResource("ScreenBgBrush")).Color);
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

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();
        Assert.Null(threadEx);
    }

    [Fact]
    public void FractionableItems_TicketAndLegalInvoice_RecomputeExactTotalAndThreeDecimalWeight()
    {
        var printer = new TicketPrintService();
        var company = new CompanyDataEntity { Descripcion = "AUTOSERVICE TEST", Ruc = "80000000-1" };
        var sale = new SaleEntity
        {
            NroVenta = 4585,
            NroFactura = "001-001-0000123",
            FechaVenta = new DateTime(2026, 11, 22),
            HoraVenta = "21:30:00",
            FormaPago = 1,
            IdCliente = 132,
            NombreCliente = "OCASIONAL",
            RucCliente = "X",
            NombreVendedor = "VENDEDOR DE MUESTRA",
            TotalIva10 = 32000m,
            LiquidacionIva10 = 2909m,
            TotalVenta = 32000m,
            EntregaGuarani = 50000m,
            VueltoGuarani = 18000m,
            Items = new()
            {
                // Truncated 2-decimal DB quantity (0.22 instead of 0.222) and zero PrecioTotal, exact total stored in PreIva10
                new SaleItemEntity
                {
                    Codigo = "TOM01",
                    Descripcion = "TOMATE PERITA",
                    Cantidad = 0.22m,
                    PreUnitario = 9000m,
                    PreIva10 = 2000m,
                    PrecioTotal = 0m,
                    Fraccionable = 1,
                    Iva = 1
                },
                new SaleItemEntity
                {
                    Codigo = "COCA2L",
                    Descripcion = "GASEOSA 2L",
                    Cantidad = 2m,
                    PreUnitario = 15000m,
                    PreIva10 = 30000m,
                    PrecioTotal = 30000m,
                    Fraccionable = 0,
                    Iva = 1
                }
            }
        };

        var resolved = TicketPrintService.ResolvePrintableLineValues(sale.Items[0]);
        Assert.Equal(2000m, resolved.EffectiveTotal);
        Assert.Equal(2000m / 9000m, resolved.EffectiveQty);

        string ticket80 = printer.FormatTicket80mm(sale, company);
        Assert.Contains("0.222", ticket80);
        Assert.Contains("2,000", ticket80);
        Assert.DoesNotContain("0.220", ticket80);
        Assert.DoesNotContain("1,980", ticket80);
        Assert.Contains("2.000", ticket80);
        Assert.Contains("30,000", ticket80);

        string factura = printer.FormatLegalInvoice(sale, company);
        Assert.Contains("0.222", factura);
        Assert.Contains("2,000", factura);
        Assert.DoesNotContain("0.220", factura);
        Assert.DoesNotContain("1,980", factura);
        Assert.Contains("2.000", factura);
        Assert.Contains("30,000", factura);
    }

    [Fact]
    public void ReportsAndConfigWindow_HasHoyButtonInAllThreeTabs_AndMainWindowHasInformesLucroTile()
    {
        Exception? threadEx = null;
        var staThread = new Thread(() =>
        {
            WindowManagerService? wm = null;
            ReportsAndConfigWindow? reportsWin = null;
            FirewallControl.App.MainWindow? mainWin = null;
            try
            {
                if (Application.Current == null)
                {
                    var app = new FirewallControl.App.App();
                    app.InitializeComponent();
                }

                var cfg = new ConfigService(GetWorkspaceRoot());
                var conn = new MySqlConnectionFactory(cfg);
                var dbf = new VfpDbfService();
                var prodRepo = new ProductRepository(conn);
                var salesRepo = new SalesRepository(conn);
                var custRepo = new CustomerRepository(conn);
                var purchRepo = new PurchaseRepository(conn);
                var cashRepo = new CashRepository(conn);
                var sysRepo = new SystemRepository(conn);
                var printSvc = new TicketPrintService(cfg);
                var iaSvc = new CargaRapidaIaService();
                var stage3Repo = new Stage3Repository(conn);

                wm = new WindowManagerService(
                    cfg, conn, dbf, prodRepo, salesRepo, custRepo,
                    purchRepo, cashRepo, sysRepo, printSvc, iaSvc, stage3Repo);

                reportsWin = new ReportsAndConfigWindow(salesRepo, sysRepo, printSvc, custRepo, prodRepo, wm);
                Assert.NotNull(reportsWin.FindName("BtnVentasHoy"));
                Assert.NotNull(reportsWin.FindName("BtnLucroHoy"));
                Assert.NotNull(reportsWin.FindName("BtnLucroProdHoy"));

                mainWin = new FirewallControl.App.MainWindow(cfg, new VfpsLicenseService(), dbf, sysRepo, cashRepo, salesRepo, wm, stage3Repo);
                Assert.NotNull(mainWin.FindName("BtnTileInformesLucro"));
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
            finally
            {
                reportsWin?.Close();
                mainWin?.Close();
                wm?.ShutdownAllWindows();
                ThemeService.ApplyTheme("neutro", isDark: false, "Moderna");
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();
        Assert.Null(threadEx);
    }

    [Fact]
    public void CompactScreens_1024x728_And_800x560_ComputeValidCascadeSelectorAndClassicBounds()
    {
        var viewports = new[]
        {
            new Rect(0, 0, 800, 560),
            new Rect(0, 0, 1024, 680),
            new Rect(0, 0, 1024, 720),
            new Rect(0, 0, 1024, 728),
            new Rect(0, 0, 1092, 614),
            new Rect(0, 0, 1366, 728),
            new Rect(0, 0, 1536, 816),
            new Rect(0, 0, 1920, 1040)
        };

        foreach (var work in viewports)
        {
            Rect sel = WindowManagerService.ComputeRenovadoSelectorBounds(work, 890, 690);
            Assert.True(sel.Width > 0 && sel.Height > 0, $"Selector inválido en {work}: {sel}");
            Assert.True(sel.Left >= work.Left && sel.Right <= work.Right, $"Selector Left/Right fuera de WorkArea {work}: {sel}");
            Assert.True(sel.Top >= work.Top && sel.Bottom <= work.Bottom, $"Selector Top/Bottom fuera de WorkArea {work}: {sel}");

            Rect prev = WindowManagerService.ComputeRenovadoCascadedBounds(work, 1);
            Assert.True(prev.Width > 0 && prev.Height > 0, $"Cascada Caja 1 inválida en {work}: {prev}");
            Assert.True(prev.Left >= work.Left && prev.Right <= work.Right, $"Caja 1 fuera horizontal en {work}: {prev}");
            Assert.True(prev.Top >= work.Top && prev.Bottom <= work.Bottom, $"Caja 1 fuera vertical en {work}: {prev}");

            for (int caja = 2; caja <= 5; caja++)
            {
                Rect curr = WindowManagerService.ComputeRenovadoCascadedBounds(work, caja);
                Assert.True(curr.Left > prev.Left, $"Work={work} Caja {caja} Left={curr.Left} debe ser > Caja {caja - 1} Left={prev.Left}");
                Assert.True(curr.Top > prev.Top, $"Work={work} Caja {caja} Top={curr.Top} debe ser > Caja {caja - 1} Top={prev.Top}");
                Assert.Equal(prev.Width, curr.Width, 1);
                Assert.Equal(prev.Height, curr.Height, 1);
                Assert.True(curr.Right <= work.Right, $"Work={work} Caja {caja} excede Right: {curr}");
                Assert.True(curr.Bottom <= work.Bottom, $"Work={work} Caja {caja} excede Bottom: {curr}");
                prev = curr;
            }

            Rect classic = WindowManagerService.ComputeClassicSharedBounds(work);
            Assert.True(classic.Width > 0 && classic.Height > 0, $"Classic bounds inválidos en {work}: {classic}");
            Assert.True(classic.Left >= work.Left && classic.Right <= work.Right, $"Classic fuera horizontal en {work}: {classic}");
            Assert.True(classic.Top >= work.Top && classic.Bottom <= work.Bottom, $"Classic fuera vertical en {work}: {classic}");
        }
    }
}
