using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

public class Stage1VerificationTests
{
    private static string GetWorkspaceRoot() => TestPathHelper.GetWorkspaceRootOrTemp();

    [Fact]
    public void EstiloPos_Persistence_And_LiveHotSwap_PreservesCartItems()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"fc_style_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "FireWall.Ini"), "[Config]\r\nlcBaseDatos=firewallcontrol\r\n");

        try
        {
            var cfg1 = new ConfigService(tempDir);
            Assert.Equal("Renovado", cfg1.Preferences.EstiloPos);
            cfg1.Preferences.EstiloPos = "Clasico";
            cfg1.SavePreferences();

            var cfg2 = new ConfigService(tempDir);
            Assert.Equal("Clasico", cfg2.Preferences.EstiloPos);

            Exception? threadEx = null;
            var staThread = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null)
                    {
                        var app = new FirewallControl.App.App();
                        app.InitializeComponent();
                    }

                    var liveCfg = new ConfigService(GetWorkspaceRoot());
                    liveCfg.Preferences.EstiloPos = "Renovado";
                    var connFactory = new MySqlConnectionFactory(liveCfg);
                    var dbfService = new VfpDbfService();
                    var productRepo = new ProductRepository(connFactory);
                    var salesRepo = new SalesRepository(connFactory);
                    var customerRepo = new CustomerRepository(connFactory);
                    var purchaseRepo = new PurchaseRepository(connFactory);
                    var cashRepo = new CashRepository(connFactory);
                    var systemRepo = new SystemRepository(connFactory);
                    var printService = new TicketPrintService(liveCfg);
                    var iaService = new CargaRapidaIaService();

                    var wm = new WindowManagerService(
                        liveCfg, connFactory, dbfService,
                        productRepo, salesRepo, customerRepo,
                        purchaseRepo, cashRepo, systemRepo, printService, iaService);

                    try
                    {
                        var salesVm = wm.OpenOrActivateSalesWindow(1);
                        var selVm = wm.OpenOrActivateItemsSelector(salesVm, "A", 0, null);
                        Assert.Equal(typeof(AbmVentasAutoserviceWindow), wm.GetOpenSalesWindowType(1));
                        Assert.Equal(typeof(ItemsFacturaVentasWindow), wm.GetItemsSelectorWindowType());

                        SalesCalculationEngine.ApplyItemOperation(salesVm.Items, new SaleItemEntity
                        {
                            IdProducto = 10,
                            Codigo = "TEST01",
                            Descripcion = "PRODUCTO PRUEBA SWAP",
                            Cantidad = 3m,
                            PreUnitario = 15000m,
                            Iva = 1
                        }, "A", 0);
                        salesVm.SumarProductos();
                        selVm.ActualizarEspejo();
                        Assert.Single(salesVm.Items);
                        Assert.Equal(45000m, salesVm.TotalVenta);

                        // Hot-swap to Clasico (Sufer POS) while keeping loaded items intact
                        bool swappedToClassic = wm.ApplyPosStyle("Clasico");
                        Assert.True(swappedToClassic);
                        Assert.Equal(typeof(AbmVentasClassicWindow), wm.GetOpenSalesWindowType(1));
                        Assert.Equal(typeof(ItemsFacturaVentasClassicWindow), wm.GetItemsSelectorWindowType());
                        Assert.Single(salesVm.Items);
                        Assert.Equal(45000m, salesVm.TotalVenta);
                        Assert.Equal(45000m, selVm.TotalVentaEspejo);

                        // Hot-swap back to Renovado
                        bool swappedToRenovado = wm.ApplyPosStyle("Renovado");
                        Assert.True(swappedToRenovado);
                        Assert.Equal(typeof(AbmVentasAutoserviceWindow), wm.GetOpenSalesWindowType(1));
                        Assert.Equal(typeof(ItemsFacturaVentasWindow), wm.GetItemsSelectorWindowType());
                        Assert.Single(salesVm.Items);
                        Assert.Equal(45000m, salesVm.TotalVenta);
                    }
                    finally
                    {
                        wm.ShutdownAllWindows();
                    }
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
            });

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();
            Assert.Null(threadEx);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void KeyShortcutHelper_FormatKeyCombo_UnifiesCaptureAndRuntime()
    {
        Assert.Equal("ESPACIO", KeyShortcutHelper.FormatKeyCombo(Key.Space, ModifierKeys.None));
        Assert.Equal("INICIO", KeyShortcutHelper.FormatKeyCombo(Key.Home, ModifierKeys.None));
        Assert.Equal("FIN", KeyShortcutHelper.FormatKeyCombo(Key.End, ModifierKeys.None));
        Assert.Equal("REPAG", KeyShortcutHelper.FormatKeyCombo(Key.PageUp, ModifierKeys.None));
        Assert.Equal("AVPAG", KeyShortcutHelper.FormatKeyCombo(Key.PageDown, ModifierKeys.None));
        Assert.Equal("DEL", KeyShortcutHelper.FormatKeyCombo(Key.Delete, ModifierKeys.None));
        Assert.Equal("CTRL+X", KeyShortcutHelper.FormatKeyCombo(Key.X, ModifierKeys.Control));
        Assert.Equal("ALT+F", KeyShortcutHelper.FormatKeyCombo(Key.F, ModifierKeys.Alt));
        Assert.Equal("F9", KeyShortcutHelper.FormatKeyCombo(Key.F9, ModifierKeys.None));

        var prefs = new AppPreferencesEntity();
        var f9Binding = ShortcutCatalog.ResolveTriggeredBinding(prefs, ShortcutCatalog.WinItemsFactura, "F9", isInputEmpty: false);
        Assert.NotNull(f9Binding);
        Assert.Equal("CARGA_RAPIDA", f9Binding!.ActionCode);
    }

    [Fact]
    public void DynamicMenu_DoesNotDropSeparatedItems()
    {
        string root = GetWorkspaceRoot();
        var dbfService = new VfpDbfService();
        var menus = dbfService.LoadMenuStructure(Path.Combine(root, "Styles"), includeVeterinaria: true);

        int expectedSeparatedItems = menus
            .SelectMany(m => m.Children)
            .Count(b => b.Separador);
        if (TestPathHelper.HasLocalLegacyDbfs())
            Assert.True(expectedSeparatedItems > 0, "Expected _MenuBars.dbf to have items with Separador=1.");

        Exception? threadEx = null;
        var staThread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    var app = new FirewallControl.App.App();
                    app.InitializeComponent();
                }

                var cfg = new ConfigService(root);
                var lic = new VfpsLicenseService();
                var conn = new MySqlConnectionFactory(cfg);
                var prodRepo = new ProductRepository(conn);
                var salesRepo = new SalesRepository(conn);
                var custRepo = new CustomerRepository(conn);
                var purchRepo = new PurchaseRepository(conn);
                var cashRepo = new CashRepository(conn);
                var sysRepo = new SystemRepository(conn);
                var printSvc = new TicketPrintService(cfg);
                var iaSvc = new CargaRapidaIaService();
                var wm = new WindowManagerService(cfg, conn, dbfService, prodRepo, salesRepo, custRepo, purchRepo, cashRepo, sysRepo, printSvc, iaSvc);

                var mainWin = new FirewallControl.App.MainWindow(cfg, lic, dbfService, sysRepo, cashRepo, salesRepo, wm);
                var buildMethod = typeof(FirewallControl.App.MainWindow).GetMethod(
                    "BuildDynamicMenu",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(buildMethod);
                buildMethod!.Invoke(mainWin, new object[] { true });

                var menuField = mainWin.FindName("DynamicMainMenu") as Menu;
                Assert.NotNull(menuField);
                Assert.Equal(menus.Count, menuField!.Items.Count);

                // Every separated child bar MUST produce BOTH its MenuItem and a trailing Separator!
                for (int i = 0; i < menus.Count; i++)
                {
                    var expectedChildren = menus[i].Children;
                    var topMenu = Assert.IsType<MenuItem>(menuField.Items[i]);
                    int actualMenuItems = topMenu.Items.OfType<MenuItem>().Count();
                    int actualSeparators = topMenu.Items.OfType<Separator>().Count();

                    Assert.Equal(expectedChildren.Count, actualMenuItems);
                    Assert.Equal(expectedChildren.Count(c => c.Separador), actualSeparators);
                }

                mainWin.Close();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();
        Assert.Null(threadEx);
    }

    [Fact]
    public void FastCargaParser_Cleaners_Tsv_GeminiJson_And_ModelsJson_WorkExact()
    {
        Assert.Equal("COCA COLA 1.5L", FastCargaParser.CleanNombreIa(" - COCA COLA PET 1500 x 6"));
        Assert.Equal("AGUA MINERAL 500ml", FastCargaParser.CleanNombreIa("- AGUA MINERAL 500 X 12"));
        Assert.Equal("JUGO NARANJA 2L", FastCargaParser.CleanNombreIa("JUGO NARANJA 2000"));
        Assert.Equal(8650m, FastCargaParser.CleanCostoIa("8.650,00"));
        Assert.Equal(51900m, FastCargaParser.CleanCostoIa("51.900"));

        const string sampleTsv =
            "```\n" +
            "ABC123\tCODIGO CON LETRAS DEBE DESCARTARSE\t5000\n" +
            "7891234567890\t- FANTA NARANJA PET 2000 x 6\t8.500\n" +
            "SoloDosColumnas\t9000\n" +
            "7840011223344\tAGUA 500 x 12\t1200.00\n" +
            "```";
        var parsedQueue = FastCargaParser.ParseTsvToQueue(sampleTsv);
        Assert.Equal(2, parsedQueue.Count);
        Assert.Equal("7891234567890", parsedQueue[0].Codigo);
        Assert.Equal("FANTA NARANJA 2L", parsedQueue[0].Nombre);
        Assert.Equal(8500m, parsedQueue[0].Costo);
        Assert.Equal("AGUA 500ml", parsedQueue[1].Nombre);
        Assert.Equal(1200m, parsedQueue[1].Costo);

        const string geminiJson = """
        {
          "candidates": [{
            "content": {
              "parts": [
                { "thought": true, "text": "thinking internal..." },
                { "text": "7891000\tCOCA 2L\t11000" }
              ]
            }
          }]
        }
        """;
        Assert.Equal("7891000\tCOCA 2L\t11000", FastCargaParser.ExtractGeminiText(geminiJson));

        const string modelsJson = """
        {
          "models": [
            { "name": "models/gemini-3.0-flash", "supportedGenerationMethods": ["generateContent"] },
            { "name": "models/gemini-embedding-001", "supportedGenerationMethods": ["embedContent"] },
            { "name": "models/veo-2.0-generate", "supportedGenerationMethods": ["predictLongRunning"] },
            { "name": "models/gemini-2.5-pro", "supportedGenerationMethods": ["generateContent", "countTokens"] }
          ]
        }
        """;
        var models = FastCargaParser.ParseModelsJson(modelsJson);
        Assert.Equal(2, models.Count);
        Assert.Contains("gemini-3.0-flash", models);
        Assert.Contains("gemini-2.5-pro", models);

        var cuotas = FastCargaParser.CalculateInstallments(100000m, 10000m, 4, 30, new DateTime(2026, 1, 1));
        Assert.Equal(4, cuotas.Count);
        Assert.Equal(90000m, cuotas.Sum(c => c.Monto));
        Assert.Equal(new DateTime(2026, 1, 31), cuotas[0].Vencimiento);
        Assert.Equal("4/4", cuotas[3].CuotaTexto);
    }

    [Fact]
    public void CargaRapidaIaService_QueueAdvanceSkipAndSnapshot_WorkCleanly()
    {
        var svc = new CargaRapidaIaService();
        svc.PopulateQueue(new[]
        {
            new FastCargaQueueItem { ItemId = 1, Codigo = "1001", Nombre = "PROD 1", Costo = 5000m },
            new FastCargaQueueItem { ItemId = 2, Codigo = "1002", Nombre = "PROD 2", Costo = 8000m }
        });

        Assert.True(svc.IsQueueActive);
        Assert.Equal(1, svc.QueueIndex);
        Assert.Equal(2, svc.QueueTotal);
        Assert.Equal("1001", svc.GetCurrentQueueItem()?.Codigo);

        svc.SaveSnapshot(new FastCargaSnapshot { Codigo = "1001", PrecioVenta = 7500m, LastFocusedControl = "TxtFastVenta" });
        Assert.True(svc.IsHidden);
        Assert.NotNull(svc.Snapshot);

        var next = svc.AdvanceQueue(skipped: true);
        Assert.NotNull(next);
        Assert.Equal("1002", next!.Codigo);
        Assert.Equal("SALTADO", svc.QueueItems[0].Estado);

        var done = svc.AdvanceQueue(skipped: false);
        Assert.Null(done);
        Assert.False(svc.IsQueueActive);
        Assert.Equal("GUARDADO", svc.QueueItems[1].Estado);

        svc.CancelAndResetAll();
        Assert.Empty(svc.QueueItems);
        Assert.Null(svc.Snapshot);
    }

    [Fact]
    public async Task LiveMySql_Stage1Repositories_QueriesAndFastCargaIni_WorkCleanly()
    {
        if (await TestPathHelper.CanConnectMySqlAsync())
        {
            var cfg = new ConfigService(GetWorkspaceRoot());
            var conn = new MySqlConnectionFactory(cfg);
            Assert.True(await conn.TestConnectionAsync());

            var productRepo = new ProductRepository(conn);
            var salesRepo = new SalesRepository(conn);
            var systemRepo = new SystemRepository(conn);

            var sups = await systemRepo.GetSupervisorsAsync();
            Assert.NotNull(sups);

            var budgets = await salesRepo.SearchBudgetsAsync(null, 10);
            Assert.NotNull(budgets);

            var lots = await productRepo.GetProductLotsAsync(0);
            Assert.NotNull(lots);

            int nextAdj = await productRepo.GetNextNroAjusteAsync();
            Assert.True(nextAdj >= 1);
        }

        string tempDir = Path.Combine(Path.GetTempPath(), $"fc_ini_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "FireWall.Ini"),
                "[Config]\r\nlcBaseDatos=firewallcontrol\r\n[Local]\r\nTimbrado=12345678\r\n");

            var tempCfg1 = new ConfigService(tempDir);
            tempCfg1.SaveFastCargaSettings("test-key-123", "gemini-2.5-flash", "gemini-3.6-flash,gemini-2.5-flash");

            var tempCfg2 = new ConfigService(tempDir);
            Assert.Equal("test-key-123", tempCfg2.Config.FastCargaApiKey);
            Assert.Equal("gemini-2.5-flash", tempCfg2.Config.FastCargaApiModel);
            Assert.Equal("12345678", tempCfg2.Config.Timbrado);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Stage1AllNewWindows_InitializeComponent_WithoutExceptions()
    {
        Exception? threadEx = null;
        var staThread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    var app = new FirewallControl.App.App();
                    app.InitializeComponent();
                }

                string root = GetWorkspaceRoot();
                var cfg = new ConfigService(root);
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

                var wm = new WindowManagerService(
                    cfg, conn, dbf, prodRepo, salesRepo, custRepo,
                    purchRepo, cashRepo, sysRepo, printSvc, iaSvc);

                var salesVm = new AbmVentasAutoserviceViewModel(salesRepo, custRepo, cashRepo, sysRepo, printSvc, wm, 1, prodRepo);
                var itemsVm = new ItemsFacturaVentasViewModel(prodRepo, wm);

                var windows = new Window[]
                {
                    new AbmVentasClassicWindow(salesVm),
                    new ItemsFacturaVentasClassicWindow(itemsVm),
                    new CargaRapidaProductosWindow(prodRepo, cfg, iaSvc) { AllowPermanentClose = true },
                    new CalculatorDialog(val => { }),
                    new BuscarPresupuestosImportDialog(salesRepo),
                    new EmisionFacturaLegalWindow(salesRepo, sysRepo, printSvc, new SaleEntity { TotalVenta = 50000m }),
                    new AjusteStockWindow(prodRepo, wm, 1),
                    new CuotaVentaDialog(new SaleEntity { TotalVenta = 120000m, NroVenta = 10 }),
                    new BuscarLotesDialog(prodRepo, 1, 1),
                    new CombinarCompuestoDialog(prodRepo, wm, 1, "PROD TEST"),
                    new QuickCreateCustomerDialog(custRepo),
                    new QuickCreateSellerDialog(sysRepo)
                };

                foreach (var w in windows)
                {
                    if (w is AbmVentasClassicWindow c1) c1.AllowPermanentClose = true;
                    if (w is ItemsFacturaVentasClassicWindow c2) c2.AllowPermanentClose = true;
                    w.Close();
                }
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();
        Assert.Null(threadEx);
    }
}
