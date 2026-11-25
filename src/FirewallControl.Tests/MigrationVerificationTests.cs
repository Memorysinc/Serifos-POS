using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FirewallControl.App.Services;
using FirewallControl.App.ViewModels;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;
using FirewallControl.Infrastructure.Database;
using FirewallControl.Infrastructure.Repositories;
using FirewallControl.Infrastructure.Services;
using Xunit;

namespace FirewallControl.Tests;

public class MigrationVerificationTests
{
    private static string GetWorkspaceRoot() => TestPathHelper.GetWorkspaceRootOrTemp();

    [Fact]
    public void VfpCompat_Sys2007_MatchesExactVfpCrc16Ccitt()
    {
        // Verified against VFP 9 COM: SYS(2007, "123456") == "12020"
        Assert.Equal("12020", VfpCompat.Sys2007("123456"));
        Assert.True(VfpCompat.ValidateUserPassword("123456", "12020"));
        Assert.False(VfpCompat.ValidateUserPassword("wrongpass", "12020"));
    }

    [Fact]
    public void VfpCompat_Encriptar_RoundTripsAndDecryptsFirewallSql()
    {
        const string original = "firewallcontrol";
        string encrypted = VfpCompat.Encriptar("ONN", original, "###$$$$");
        string decrypted = VfpCompat.Encriptar("OFF", encrypted, "###$$$$");
        Assert.Equal(original, decrypted);

        string tempDir = Path.Combine(Path.GetTempPath(), $"fc_sql_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "FireWall.Ini"), "[Config]\r\nlcBaseDatos=firewallcontrol\r\n");
            var writerCfg = new ConfigService(tempDir);
            writerCfg.SaveServerCredentials("localhost", "pos_user", "sample_secret_123", 3306, "firewallcontrol");

            var cfgService = new ConfigService(tempDir);
            Assert.Equal("firewallcontrol", cfgService.Config.BaseDatos);
            Assert.Equal("pos_user", cfgService.Config.SqlUser);
            Assert.Equal("sample_secret_123", cfgService.Config.SqlPassword);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void VfpCompat_RedondearMas_ConvFact_And_Letras_WorkExact()
    {
        // redondearmas(1523, 2) -> 1600
        Assert.Equal(1600m, VfpCompat.RedondearMas(1523m, 2));
        Assert.Equal(1500m, VfpCompat.RedondearMas(1500m, 2));

        Assert.Equal("001-001-0000045", VfpCompat.ConvFact("1-1-45"));
        Assert.Contains("GUARANIES", VfpCompat.Letras(25000m));
    }

    [Fact]
    public void SalesCalculationEngine_Validations_ReturnProfessionalMessagesAndRespectLimits()
    {
        // Quantity validations
        var qZero = SalesCalculationEngine.ValidateQuantity(0m, 0, 1, 10m);
        Assert.False(qZero.IsValid);
        Assert.Equal("La cantidad debe ser mayor que cero.", qZero.ErrorMessage);

        var qOver = SalesCalculationEngine.ValidateQuantity(121m, 0, 1, 500m);
        Assert.False(qOver.IsValid);
        Assert.Contains("límite máximo permitido", qOver.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Price validations
        var pBelowCost = SalesCalculationEngine.ValidateUnitPrice(5000m, 6000m, true);
        Assert.False(pBelowCost.IsValid);
        Assert.Contains("costo", pBelowCost.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var pUnder99 = SalesCalculationEngine.ValidateUnitPrice(50m, 10m, true);
        Assert.False(pUnder99.IsValid);
        Assert.Contains("mínimo permitido", pUnder99.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var pOver1M = SalesCalculationEngine.ValidateUnitPrice(1000000m, 500000m, true);
        Assert.False(pOver1M.IsValid);
        Assert.Contains("máximo permitido", pOver1M.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var pValid = SalesCalculationEngine.ValidateUnitPrice(15000m, 10000m, true);
        Assert.True(pValid.IsValid);
    }

    [Fact]
    public void SalesCalculationEngine_ApplyItemOperation_HandlesInsertA_And_UpdateM_And_SumarProductos()
    {
        var cursor = new List<SaleItemEntity>();

        // 1. lctipo = "A" (INSERT INTO)
        var item1 = new SaleItemEntity
        {
            IdProducto = 10,
            Codigo = "P001",
            Descripcion = "COCA COLA 2L",
            Cantidad = 2m,
            PreUnitario = 11000m,
            Iva = 1 // 10%
        };
        var added1 = SalesCalculationEngine.ApplyItemOperation(cursor, item1, "A", 0);
        Assert.Single(cursor);
        Assert.Equal(1, added1.RecNo);
        Assert.Equal(22000m, added1.PrecioTotal);
        Assert.Equal(22000m, added1.PreIva10);

        // 2. Add second item (IVA 5%)
        var item2 = new SaleItemEntity
        {
            IdProducto = 20,
            Codigo = "P002",
            Descripcion = "ARROZ 1KG",
            Cantidad = 1m,
            PreUnitario = 5250m,
            Iva = 2 // 5%
        };
        var added2 = SalesCalculationEngine.ApplyItemOperation(cursor, item2, "A", 0);
        Assert.Equal(2, cursor.Count);
        Assert.Equal(2, added2.RecNo);
        Assert.Equal(5250m, added2.PreIva5);

        // 3. lctipo = "M" (UPDATE ... WHERE RECNO() = 1)
        var modItem1 = item1.Clone();
        modItem1.Cantidad = 3m;
        SalesCalculationEngine.ApplyItemOperation(cursor, modItem1, "M", 1);
        Assert.Equal(2, cursor.Count);
        Assert.Equal(3m, cursor[0].Cantidad);
        Assert.Equal(33000m, cursor[0].PrecioTotal);

        // 4. SumarProductos
        var rate = new ExchangeRateEntity { VentaDolar = 7500m, VentaReal = 1500m };
        var totals = SalesCalculationEngine.SumarProductos(cursor, 0m, 0, rate);
        Assert.Equal(33000m, totals.TotalIva10);
        Assert.Equal(5250m, totals.TotalIva5);
        Assert.Equal(38250m, totals.TotalVenta);
        Assert.Equal(3000m, totals.LiquidacionIva10); // 33000 / 11
        Assert.Equal(250m, totals.LiquidacionIva5);   // 5250 / 21
        Assert.Equal(3250m, totals.TotalImpuesto);
    }

    [Fact]
    public void VfpDbfService_LoadsMenuPromptsAndMenuBars_And_GenesProductos()
    {
        string root = GetWorkspaceRoot();
        var dbfService = new VfpDbfService();

        if (TestPathHelper.HasLocalLegacyDbfs())
        {
            var menus = dbfService.LoadMenuStructure(Path.Combine(root, "Styles"), includeVeterinaria: true);
            Assert.True(menus.Count >= 7);
            Assert.Contains(menus, m => m.CleanTitle.Contains("Archivo", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(menus, m => m.CleanTitle.Contains("Procesos", StringComparison.OrdinalIgnoreCase));

            string genesPath = Path.Combine(root, "genes_productos.dbf");
            var allRealGenes = dbfService.LoadAllGenesProducts(genesPath);
            Assert.True(allRealGenes.Count >= 70);
            Assert.Contains(allRealGenes, g => g.Nombre.Contains("Tomate", StringComparison.OrdinalIgnoreCase) && g.GenId == 1);

            var buttons = dbfService.LoadGenesButtons(genesPath, 1);
            Assert.True(buttons.Count >= 25);
        }

        // Test writing & reading back a temporary genes DBF
        string tempDbf = Path.Combine(Path.GetTempPath(), $"test_genes_{Guid.NewGuid():N}.dbf");
        try
        {
            dbfService.SaveGenesButton(tempDbf, new GenesButtonItem
            {
                GenId = 2,
                BotonId = 3,
                Codigo = "789123",
                Nombre = "COSTILLA VACUNA",
                Tipo = "KG",
                Precio = 45000m,
                Costo = 32000m
            });

            var loaded = dbfService.LoadGenesButtons(tempDbf, 2);
            var btn3 = loaded.First(b => b.BotonId == 3);
            Assert.Equal("789123", btn3.Codigo);
            Assert.Equal("COSTILLA VACUNA", btn3.Nombre);
            Assert.Equal("KG", btn3.Tipo);
            Assert.Equal(45000m, btn3.Precio);
            Assert.Equal(32000m, btn3.Costo);
        }
        finally
        {
            if (File.Exists(tempDbf))
                File.Delete(tempDbf);
        }
    }

    [Fact]
    public async Task LiveMySqlRepositories_And_TransactionalSaleWithRollback_Succeed()
    {
        var licenseService = new VfpsLicenseService();
        Assert.True(licenseService.IsLicensed);
        Assert.True(licenseService.VerifyLicense());

        if (!await TestPathHelper.CanConnectMySqlAsync())
            return;

        string root = GetWorkspaceRoot();
        var configService = new ConfigService(root);
        var connFactory = new MySqlConnectionFactory(configService);
        Assert.True(await connFactory.TestConnectionAsync());

        var productRepo = new ProductRepository(connFactory);
        var salesRepo = new SalesRepository(connFactory);
        var customerRepo = new CustomerRepository(connFactory);
        var cashRepo = new CashRepository(connFactory);
        var systemRepo = new SystemRepository(connFactory);

        // 1. Verify Users & ADMINISTRA password hash (12020)
        var admin = await systemRepo.GetUserByUsernameAsync("ADMINISTRA");
        Assert.NotNull(admin);
        Assert.Equal("12020", admin!.Cpassword.Trim());

        // 2. Verify Products catalog query
        var products = await productRepo.SearchProductsAsync("", 1, 20);
        Assert.NotEmpty(products);
        var sampleProduct = products.First(p => p.Precio1 >= 100m);

        var byCode = await productRepo.GetByCodeAsync(sampleProduct.Codigo, 1);
        Assert.NotNull(byCode);
        Assert.Equal(sampleProduct.IdProducto, byCode!.IdProducto);

        // 3. Verify Customers, Exchange Rates, Company Data, Next NroVenta
        var customers = await customerRepo.SearchCustomersAsync("", 10);
        Assert.NotEmpty(customers);

        var rate = await cashRepo.GetLatestExchangeRateAsync();
        Assert.NotNull(rate);

        var company = await systemRepo.GetCompanyDataAsync();
        Assert.NotNull(company);

        int nextNro = await salesRepo.GetNextNroVentaAsync();
        Assert.True(nextNro > 0);

        // Verify informe_lucro and informe_lucroproductos queries
        var lucroRows = await salesRepo.GetInformeLucroAsync(DateTime.Today.AddYears(-2), DateTime.Today, null);
        Assert.NotNull(lucroRows);

        var lucroProdRows = await salesRepo.GetInformeLucroProductosAsync(DateTime.Today.AddYears(-2), DateTime.Today, null);
        Assert.NotNull(lucroProdRows);

        // Verify loading details of all recent sales (including VFP zero-date sales like #4529)
        var recentSales = await salesRepo.GetRecentSalesAsync(DateTime.Today.AddYears(-2), DateTime.Today.AddYears(1), 50);
        Assert.NotEmpty(recentSales);
        foreach (var s in recentSales)
        {
            var detail = await salesRepo.GetSaleByIdAsync(s.IdVenta);
            Assert.NotNull(detail);
        }

        // 4. Test full sale transaction (lctipo="A" + lctipo="M") inside a rolled-back transaction
        using var conn = connFactory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            var testSale = new SaleEntity
            {
                NroVenta = nextNro + 50000,
                NroFactura = "001-001-9999999",
                FechaVenta = DateTime.Today,
                HoraVenta = "12:00:00",
                FormaPago = 1,
                IdDeposito = 1,
                IdCaja = 1,
                IdCliente = customers[0].IdCliente,
                NombreCliente = customers[0].Nombres,
                RucCliente = customers[0].Ruc,
                IdVendedor = 10,
                TotalIva10 = sampleProduct.Precio1 * 2,
                Subtotal = sampleProduct.Precio1 * 2,
                TotalVenta = sampleProduct.Precio1 * 2,
                LiquidacionIva10 = Math.Round((sampleProduct.Precio1 * 2) / 11m, 0),
                TotalImpuesto = Math.Round((sampleProduct.Precio1 * 2) / 11m, 0),
                EntregaGuarani = sampleProduct.Precio1 * 2,
                Items = new List<SaleItemEntity>
                {
                    new()
                    {
                        IdProducto = sampleProduct.IdProducto,
                        Codigo = sampleProduct.Codigo,
                        Descripcion = sampleProduct.Descripcion,
                        Cantidad = 2m,
                        PreUnitario = sampleProduct.Precio1,
                        Iva = sampleProduct.Iva
                    }
                }
            };

            int newSaleId = await salesRepo.ExecuteSaleInTransactionAsync(conn, tx, testSale, "A");
            Assert.True(newSaleId > 0);

            // Modify same sale with lctipo = "M" (restores stock of 2 and deducts 3)
            testSale.Items[0].Cantidad = 3m;
            testSale.TotalVenta = sampleProduct.Precio1 * 3;
            int updatedSaleId = await salesRepo.ExecuteSaleInTransactionAsync(conn, tx, testSale, "M");
            Assert.Equal(newSaleId, updatedSaleId);
        }
        finally
        {
            tx.Rollback();
        }
    }

    [Fact]
    public async Task PosViewModels_ItemsFacturaVentas_And_AbmVentas_IntegrateSeamlessly()
    {
        if (!await TestPathHelper.CanConnectMySqlAsync())
            return;

        string root = GetWorkspaceRoot();
        var defaultPrefs = new AppPreferencesEntity();
        Assert.False(defaultPrefs.IniciarMovimientoRapidoPorDefecto);
        Assert.False(defaultPrefs.ReiniciarItemAlEquivocarseEnItemFactura);

        var configService = new ConfigService(root);
        configService.Preferences.IniciarMovimientoRapidoPorDefecto = false;
        configService.Preferences.ReiniciarItemAlEquivocarseEnItemFactura = false;
        configService.Preferences.LimiteCantidadPorItem = 120m;
        configService.Preferences.ValidarLimitesPrecioEnItem = true;
        configService.Preferences.PrecioMinimoVenta = 99m;
        configService.Preferences.PrecioMaximoVenta = 1000000m;
        configService.Preferences.PermitirVentaBajoCosto = false;
        var connFactory = new MySqlConnectionFactory(configService);
        var dbfService = new VfpDbfService();
        var productRepo = new ProductRepository(connFactory);
        var salesRepo = new SalesRepository(connFactory);
        var customerRepo = new CustomerRepository(connFactory);
        var purchaseRepo = new PurchaseRepository(connFactory);
        var cashRepo = new CashRepository(connFactory);
        var systemRepo = new SystemRepository(connFactory);
        var printService = new TicketPrintService();

        var windowManager = new WindowManagerService(
            configService, connFactory, dbfService,
            productRepo, salesRepo, customerRepo,
            purchaseRepo, cashRepo, systemRepo, printService);

        var salesVm = new AbmVentasAutoserviceViewModel(
            salesRepo, customerRepo, cashRepo, systemRepo, printService, windowManager, idCaja: 1);
        await salesVm.InitializeNewSaleAsync();

        var selectorVm = new ItemsFacturaVentasViewModel(productRepo, windowManager);
        selectorVm.VincularPadre(salesVm, "A", 0);

        var products = await productRepo.SearchProductsAsync("", 1, 50);
        var prod = products.First(p => p.Precio1 > p.Costo1 && p.Precio1 >= 100m && p.Precio1 < 1000000m && p.Fraccionable == 0);

        // 1A. Default state: IsMovimientoRapido == false; toggle ON with F10:
        Assert.False(selectorVm.IsMovimientoRapido);
        await selectorVm.HandleShortcutKeyAsync("F10");
        Assert.True(selectorVm.IsMovimientoRapido);

        selectorVm.CodProducto = prod.Codigo;
        bool fastAdded1 = await selectorVm.ValidateAndLookupProductAsync();
        Assert.True(fastAdded1);
        Assert.Single(salesVm.Items);
        Assert.Equal(1m, salesVm.Items[0].Cantidad);
        Assert.Equal(prod.Precio1 * 1m, salesVm.TotalVenta);

        // Scan same product a second time with IsMovimientoRapido == true -> MUST MERGE into 1 row with Cantidad == 2m!
        selectorVm.CodProducto = prod.Codigo;
        bool fastAdded2 = await selectorVm.ValidateAndLookupProductAsync();
        Assert.True(fastAdded2);
        Assert.Single(salesVm.Items);
        Assert.Equal(2m, salesVm.Items[0].Cantidad);
        Assert.Equal(prod.Precio1 * 2m, salesVm.TotalVenta);

        // Genes loads MUST NOT merge even when IsMovimientoRapido == true -> separate rows!
        var geneSelection = new GenesSelectionResult(
            new GenesButtonItem
            {
                Id = 999,
                GenId = 1,
                BotonId = 1,
                Nombre = "TOMATE",
                Codigo = "9000000000001",
                Tipo = "KG",
                Precio = 9000m,
                Costo = 7200m
            },
            "KG",
            9000m,
            5000m);
        await selectorVm.ApplyGenesSelectionAsync(geneSelection);
        await selectorVm.ApplyGenesSelectionAsync(geneSelection);
        Assert.Equal(3, salesVm.Items.Count); // 1 merged regular row + 2 separate Genes rows!

        // Toggle OFF with F10 and clear to test manual quantity mode (IsMovimientoRapido = false)
        await selectorVm.HandleShortcutKeyAsync("F10");
        Assert.False(selectorVm.IsMovimientoRapido);
        await salesVm.InitializeNewSaleAsync();

        // 1B. Lookup and Add product manually (lctipo = "A", IsMovimientoRapido = false)
        selectorVm.CodProducto = prod.Codigo;
        bool found = await selectorVm.ValidateAndLookupProductAsync();
        Assert.True(found);
        selectorVm.Cantidad = 2m;
        bool added = await selectorVm.CmdGrabarAsync();
        Assert.True(added);

        Assert.Single(salesVm.Items);
        Assert.Equal(prod.Precio1 * 2m, salesVm.TotalVenta);
        Assert.Equal(salesVm.TotalVenta, selectorVm.TotalVentaEspejo);

        // 2. Modify existing product in row 1 (lctipo = "M", nRecNo = 1)
        var row1 = salesVm.Items[0];
        selectorVm.VincularPadre(salesVm, "M", row1.RecNo, row1);
        Assert.Equal("M", selectorVm.LcTipo);
        Assert.Equal(1, selectorVm.NRecNo);
        selectorVm.Cantidad = 4m;
        bool modified = await selectorVm.CmdGrabarAsync();
        Assert.True(modified);

        Assert.Single(salesVm.Items);
        Assert.Equal(4m, salesVm.Items[0].Cantidad);
        Assert.Equal(prod.Precio1 * 4m, salesVm.TotalVenta);
        Assert.Equal(salesVm.TotalVenta, selectorVm.TotalVentaEspejo);
        Assert.Equal("A", selectorVm.LcTipo); // Automatically resets to "A" after update!

        // 3. Verify Preference: ReiniciarItemAlEquivocarseEnItemFactura (Default: false)
        Assert.False(configService.Preferences.ReiniciarItemAlEquivocarseEnItemFactura);

        // Verify TryParseGuaraniAmount parses "10,000" and "10.000" as 10000m (never 10m!)
        Assert.True(FirewallControl.App.Views.ItemsFacturaVentasWindow.TryParseGuaraniAmount("10,000", out decimal pComma));
        Assert.Equal(10000m, pComma);
        Assert.True(FirewallControl.App.Views.ItemsFacturaVentasWindow.TryParseGuaraniAmount("10.000", out decimal pDot));
        Assert.Equal(10000m, pDot);

        // 3A. With preference FALSE (default): invalid quantity keeps product & description on screen
        selectorVm.CodProducto = prod.Codigo;
        await selectorVm.ValidateAndLookupProductAsync();
        selectorVm.Cantidad = 0m;
        bool invalidWhenFalse = await selectorVm.CmdGrabarAsync();
        Assert.False(invalidWhenFalse);
        Assert.Equal(prod.Codigo, selectorVm.CodProducto);
        Assert.Equal(prod.Descripcion, selectorVm.DesProducto);

        // 3B. With preference TRUE: invalid quantity wipes product, description, quantity & price and resets to CodProducto
        configService.Preferences.ReiniciarItemAlEquivocarseEnItemFactura = true;
        try
        {
            selectorVm.Cantidad = 0m;
            bool invalidWhenTrue = await selectorVm.CmdGrabarAsync();
            Assert.False(invalidWhenTrue);
            Assert.Equal(0, selectorVm.CurrentProductId);
            Assert.Equal(string.Empty, selectorVm.CodProducto);
            Assert.Equal(string.Empty, selectorVm.DesProducto);
            Assert.Equal(0m, selectorVm.Cantidad);
            Assert.Equal(0m, selectorVm.PrecioVenta);
        }
        finally
        {
            configService.Preferences.ReiniciarItemAlEquivocarseEnItemFactura = false;
        }
    }

    [Fact]
    public void GlobalStockControl_PurchaseWeightedAverage_And_CompactSingleTicket_WorkExact()
    {
        var prefs = new AppPreferencesEntity();
        // 1. Default must be false ("viene por defecto desactivado")
        Assert.False(prefs.NoControlarStockGlobal);
        Assert.Equal(1, prefs.CopiasTicket);
        Assert.Equal(2, prefs.LineasAvanceCorte);

        // 2. When NoControlarStockGlobal is false (default), selling 5 units when stock is 2 and VendeSinStock=0 is blocked
        var blocked = SalesCalculationEngine.ValidateQuantity(5m, 0, 0, 2m, noControlarStockGlobal: false);
        Assert.False(blocked.IsValid);

        // 3. When NoControlarStockGlobal is true, selling 5 units on the exact same item is allowed globally
        var allowed = SalesCalculationEngine.ValidateQuantity(5m, 0, 0, 2m, noControlarStockGlobal: true);
        Assert.True(allowed.IsValid);

        // 4. Weighted average cost (promedio() in itemsfacturacompras.scx) and 7 utility prices
        decimal avgCost = SalesCalculationEngine.CalculateWeightedAverageCost(
            stockActual: 10m,
            costoAnterior: 8000m,
            cantidadCompra: 10m,
            costoCompra: 10000m);
        Assert.Equal(9000m, avgCost);

        decimal priceFrom50Pct = SalesCalculationEngine.CalculateSalePriceFromUtility(10000m, 50m);
        Assert.Equal(15000m, priceFrom50Pct);

        decimal utilFromPrice = SalesCalculationEngine.CalculateUtilityFromSalePrice(10000m, 15000m);
        Assert.Equal(50m, utilFromPrice);

        // 5. Compact single-copy thermal ticket formatting
        var printService = new TicketPrintService();
        var sale = new SaleEntity
        {
            NroVenta = 123,
            FechaVenta = DateTime.Today,
            HoraVenta = "10:30:00",
            FormaPago = 1,
            IdCaja = 1,
            NombreCliente = "CLIENTE OCASIONAL",
            RucCliente = "X",
            TotalIva10 = 15000m,
            TotalVenta = 15000m,
            LiquidacionIva10 = 1364m,
            TotalImpuesto = 1364m,
            EntregaGuarani = 20000m,
            VueltoGuarani = 5000m,
            Items = new List<SaleItemEntity>
            {
                new()
                {
                    Codigo = "101",
                    Descripcion = "GASEOSA 2L",
                    Cantidad = 1m,
                    PreUnitario = 15000m,
                    PrecioTotal = 15000m,
                    Iva = 1
                }
            }
        };
        var company = new CompanyDataEntity { Descripcion = "MI SUPERMERCADO", Ruc = "80012345-6" };
        string ticketText = printService.FormatTicket80mm(sale, company);
        Assert.Contains("Numero ticket: 123", ticketText);
        Assert.Contains("Gracias por su preferencia..", ticketText);
        // Ensure it does not contain 3 repeated copies or long trailing blank lines
        int countHeader = ticketText.Split("Numero ticket: 123").Length - 1;
        Assert.Equal(1, countHeader);
    }

    [Fact]
    public void AllWindows_InitializeComponent_WithoutStaticResourceExceptions()
    {
        Exception? threadEx = null;
        var staThread = new System.Threading.Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current == null)
                {
                    var app = new FirewallControl.App.App();
                    app.InitializeComponent();
                }

                string root = GetWorkspaceRoot();
                var configService = new ConfigService(root);
                var licenseService = new VfpsLicenseService();
                var connFactory = new MySqlConnectionFactory(configService);
                var dbfService = new VfpDbfService();
                var productRepo = new ProductRepository(connFactory);
                var salesRepo = new SalesRepository(connFactory);
                var customerRepo = new CustomerRepository(connFactory);
                var purchaseRepo = new PurchaseRepository(connFactory);
                var cashRepo = new CashRepository(connFactory);
                var systemRepo = new SystemRepository(connFactory);
                var printService = new TicketPrintService(configService);

                var wm = new WindowManagerService(
                    configService, connFactory, dbfService,
                    productRepo, salesRepo, customerRepo,
                    purchaseRepo, cashRepo, systemRepo, printService);

                // Instantiate every window to verify all XAML StaticResources resolve cleanly
                var w1 = new FirewallControl.App.MainWindow(configService, licenseService, dbfService, systemRepo, cashRepo, salesRepo, wm);
                var w2 = new FirewallControl.App.Views.ConfiguracionPreferenciasWindow(configService, systemRepo, connFactory, printService, wm);
                var w3 = new FirewallControl.App.Views.PurchasesManagementWindow(purchaseRepo, productRepo, systemRepo, wm, configService);
                var w4 = new FirewallControl.App.Views.ItemsFacturaComprasWindow(productRepo, systemRepo, wm, 1, "A", null);
                var w5 = new FirewallControl.App.Views.ProductsManagementWindow(productRepo, systemRepo);
                var w6 = new FirewallControl.App.Views.CustomersManagementWindow(customerRepo);
                var w7 = new FirewallControl.App.Views.CashManagementWindow(cashRepo);
                var w8 = new FirewallControl.App.Views.ReportsAndConfigWindow(salesRepo, systemRepo, printService, customerRepo, productRepo, wm);
                var w9 = new FirewallControl.App.Views.ReeditarReimprimirDialog(salesRepo, false);
                var w10 = new FirewallControl.App.Views.ColorAtelierDialog("rose");
                var w11 = new FirewallControl.App.Views.GenesSelectorWindow(dbfService, configService, productRepo, wm);
                var w12 = new FirewallControl.App.Views.LoginWindow(systemRepo);
                var w13 = new FirewallControl.App.Views.CobroContadoDialog(
                    new SaleEntity { TotalVenta = 150000m },
                    new ExchangeRateEntity());
                var w14 = new FirewallControl.App.Views.SeleServidorWindow(configService, connFactory);
                var w15 = new FirewallControl.App.Views.ItemsFacturaVentasWindow(
                    new ItemsFacturaVentasViewModel(productRepo, wm));
                var w16 = new FirewallControl.App.Views.AbmVentasAutoserviceWindow(
                    new AbmVentasAutoserviceViewModel(salesRepo, customerRepo, cashRepo, systemRepo, printService, wm, 1));
                w1.Close();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        staThread.SetApartmentState(System.Threading.ApartmentState.STA);
        staThread.Start();
        staThread.Join();

        Assert.Null(threadEx);
    }

    [Fact]
    public void AtelierThemeSystem_SevenPalettesAndPersistence_WorkCleanly()
    {
        Assert.Equal(8, ThemeService.Palettes.Count);
        var keys = ThemeService.Palettes.Select(p => p.Key).ToList();
        Assert.Contains("neutro", keys);
        Assert.Contains("sefiros", keys);
        Assert.Contains("ocean", keys);
        Assert.Contains("emerald", keys);
        Assert.Contains("rose", keys);
        Assert.Contains("indigo", keys);
        Assert.Contains("sand", keys);
        Assert.Contains("atelier", keys);

        string tempDir = Path.Combine(Path.GetTempPath(), $"fc_theme_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var svc1 = new ConfigService(tempDir);
            svc1.Preferences.TemaAtelier = "rose";
            svc1.Preferences.TemaOscuroConfiguracion = true;
            svc1.SavePreferences();

            var svc2 = new ConfigService(tempDir);
            Assert.Equal("rose", svc2.Preferences.TemaAtelier);
            Assert.True(svc2.Preferences.TemaOscuroConfiguracion);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }

        Exception? threadEx = null;
        var staThread = new System.Threading.Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current == null)
                {
                    var app = new FirewallControl.App.App();
                    app.InitializeComponent();
                }

                foreach (var pal in ThemeService.Palettes)
                {
                    ThemeService.ApplyTheme(pal.Key, isDark: false);
                    Assert.Equal(pal.Key, ThemeService.CurrentThemeKey);
                    Assert.False(ThemeService.CurrentIsDark);
                    Assert.NotNull(System.Windows.Application.Current!.TryFindResource("ScreenBgBrush"));
                    Assert.NotNull(System.Windows.Application.Current!.TryFindResource("HeroGradientBrush"));

                    ThemeService.ApplyTheme(pal.Key, isDark: true);
                    Assert.True(ThemeService.CurrentIsDark);
                }

                ThemeService.ApplyTheme("neutro", isDark: false);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        staThread.SetApartmentState(System.Threading.ApartmentState.STA);
        staThread.Start();
        staThread.Join();

        Assert.Null(threadEx);
    }
}
