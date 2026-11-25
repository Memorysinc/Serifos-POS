using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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

public class Stage3VerificationTests
{
    private static string GetWorkspaceRoot() => TestPathHelper.GetWorkspaceRootOrTemp();

    [Fact]
    public void VfpDbfService_FixedMenuTree_PreservesAllPromptsSubmenusAndCaptions()
    {
        if (!TestPathHelper.HasLocalLegacyDbfs())
            return;

        var dbf = new VfpDbfService();
        string stylesDir = Path.Combine(GetWorkspaceRoot(), "Styles");

        var withoutVet = dbf.LoadMenuStructure(stylesDir, includeVeterinaria: false);
        var withVet = dbf.LoadMenuStructure(stylesDir, includeVeterinaria: true);

        Assert.Equal(7, withoutVet.Count);
        Assert.Equal(8, withVet.Count);

        var archivo = withVet.FirstOrDefault(p => p.OpcNumero == "100");
        Assert.NotNull(archivo);
        // Items with separador='1' must preserve their caption instead of becoming "-"
        Assert.Contains(archivo!.Children, b => b.CleanTitle.Contains("Configurar Pagina", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(archivo.Children, b => b.CleanTitle.Contains("Agenda", StringComparison.OrdinalIgnoreCase));

        var catalogos = withVet.FirstOrDefault(p => p.OpcNumero == "101");
        Assert.NotNull(catalogos);
        var prodSubmenu = catalogos!.Children.FirstOrDefault(b => b.CleanTitle.Contains("Productos", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(prodSubmenu);
        Assert.True(prodSubmenu!.SubMenu);
        Assert.Contains(prodSubmenu.SubItems, s => s.Comando.Contains("Crear_productos", StringComparison.OrdinalIgnoreCase));

        var informes = withVet.FirstOrDefault(p => p.OpcNumero == "105");
        Assert.NotNull(informes);
        Assert.Contains(informes!.Children, b => b.SubMenu && b.CleanTitle.Contains("Ventas", StringComparison.OrdinalIgnoreCase) && b.SubItems.Count >= 6);
        Assert.Contains(informes.Children, b => b.SubMenu && b.CleanTitle.Contains("Compras", StringComparison.OrdinalIgnoreCase) && b.SubItems.Count >= 2);

        var personales = withVet.FirstOrDefault(p => p.OpcNumero == "106");
        Assert.NotNull(personales);
        Assert.Contains(personales!.Children, b => b.SubMenu && b.CleanTitle.Contains("Fichas", StringComparison.OrdinalIgnoreCase) && b.SubItems.Count >= 6);
        Assert.Contains(personales.Children, b => b.SubMenu && b.CleanTitle.Contains("Movimientos", StringComparison.OrdinalIgnoreCase) && b.SubItems.Count >= 5);
    }

    [Fact]
    public void VfpPermissionHelper_EvaluatesCaccesosMatrixAndVendorRestrictions()
    {
        var adminUser = new UserEntity { Cidusuario = "ADMINISTRA", Cidgrupo = "ADMIN" };
        var restrictedGroup = new UserGroupEntity
        {
            Cidgrupo = "CAJA",
            Caccesos = "1.101\n0.000\n1.11",
            BtnVenta = 1,
            BtnCompra = 0
        };
        var cajaUser = new UserEntity { Cidusuario = "CAJERO1", Cidgrupo = "CAJA", TipoVendedor = 0 };
        var vendorStrictUser = new UserEntity { Cidusuario = "VEND2", Cidgrupo = "VENDEDORES", TipoVendedor = 2 };

        Assert.True(VfpPermissionHelper.IsAdmin(adminUser, restrictedGroup));
        Assert.True(VfpPermissionHelper.IsMenuPromptAllowed(adminUser, restrictedGroup, 2));
        Assert.True(VfpPermissionHelper.IsMenuBarAllowed(adminUser, restrictedGroup, 1, 2));

        Assert.True(VfpPermissionHelper.IsMenuPromptAllowed(cajaUser, restrictedGroup, 1));
        Assert.True(VfpPermissionHelper.IsMenuBarAllowed(cajaUser, restrictedGroup, 1, 1));
        Assert.False(VfpPermissionHelper.IsMenuBarAllowed(cajaUser, restrictedGroup, 1, 2));
        Assert.True(VfpPermissionHelper.IsMenuBarAllowed(cajaUser, restrictedGroup, 1, 3));
        Assert.False(VfpPermissionHelper.IsMenuPromptAllowed(cajaUser, restrictedGroup, 2));
        Assert.False(VfpPermissionHelper.IsMenuBarAllowed(cajaUser, restrictedGroup, 2, 1));

        Assert.True(VfpPermissionHelper.IsVendorToolbarRestricted(vendorStrictUser, restrictedGroup));
        Assert.False(VfpPermissionHelper.IsVendorToolbarRestricted(adminUser, restrictedGroup));

        var matrix = VfpPermissionHelper.BuildDefaultAccessMatrix(restrictedGroup);
        Assert.Equal(8, VfpPermissionHelper.SplitAccessLines(matrix).Length);
    }

    [Fact]
    public async Task LiveMySql_Stage3DashboardKpis_Feeds_And_ExtendedReports_WorkCleanly()
    {
        if (!await TestPathHelper.CanConnectMySqlAsync())
            return;

        var cfg = new ConfigService(GetWorkspaceRoot());
        var connFactory = new MySqlConnectionFactory(cfg);
        var repo = new Stage3Repository(connFactory);

        var dash = await repo.GetFondoDashboardDataAsync("ADMINISTRA");
        Assert.NotNull(dash);
        Assert.True(dash.TotalProductosActivos >= 0);
        Assert.NotNull(dash.UltimasVentas);
        Assert.NotNull(dash.TopVendidos);

        DateTime from = DateTime.Today.AddMonths(-6);
        DateTime to = DateTime.Today;

        Assert.NotNull(await repo.GetSalesBySellerReportAsync(from, to));
        Assert.NotNull(await repo.GetSalesBySupervisorReportAsync(from, to));
        Assert.NotNull(await repo.GetSalesByCustomerReportAsync(from, to));
        Assert.NotNull(await repo.GetSalesByDepositReportAsync(from, to));
        Assert.NotNull(await repo.GetSoldProductsReportAsync(from, to));
        Assert.NotNull(await repo.GetPurchasesBySupplierReportAsync(from, to));
        Assert.NotNull(await repo.GetPurchasedProductsReportAsync(from, to));
        Assert.NotNull(await repo.GetUserTransactionLogsAsync(from, to, null));
        Assert.NotNull(await repo.GetRestaurantOrdersBySectorAsync(null));
        Assert.True(await repo.TableExistsAsync("productos"));
    }

    [Fact]
    public async Task LiveMySql_Stage3CatalogsAndHrCrud_RoundTripWithCleanup()
    {
        if (!await TestPathHelper.CanConnectMySqlAsync())
            return;

        var cfg = new ConfigService(GetWorkspaceRoot());
        var connFactory = new MySqlConnectionFactory(cfg);
        var repo = new Stage3Repository(connFactory);
        string tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        int cityId = 0;
        int rubroId = 0;
        string agendaId = string.Empty;
        int empId = 0;
        int lookupId = 0;

        try
        {
            cityId = await repo.SaveCityAsync(new CityEntity { Ciudad = $"CIU_{tag}", Departamento = "CENTRAL" }, "A");
            Assert.True(cityId > 0);
            Assert.Contains(await repo.GetCitiesAsync(), c => c.IdCiudad == cityId && c.Ciudad == $"CIU_{tag}");

            rubroId = await repo.SaveCashRubroAsync(new CashRubroEntity { NroRubro = 9800 + Random.Shared.Next(1, 90), NombreRubro = $"RUB_{tag}", TipoCuenta = 1, Proceso = 1 }, "A");
            Assert.True(rubroId > 0);
            Assert.Contains(await repo.GetCashRubrosAsync(), r => r.IdRubros == rubroId);

            agendaId = await repo.SaveAgendaItemAsync(new AgendaItemEntity
            {
                Fecha = DateTime.Today,
                Asunto = $"AG_TST_{tag}",
                Notas = "Nota test Stage3",
                Aviso = 1,
                TipoReg = 1,
                UserId = "ADMINISTRA",
                Registro = "TST"
            }, "A");
            Assert.False(string.IsNullOrWhiteSpace(agendaId));
            Assert.Contains(await repo.GetAgendaItemsAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1), null), a => a.Id == agendaId);

            empId = await repo.SaveEmployeeAsync(new EmployeeLegajoEntity
            {
                Apellidos = "TEST_APE",
                Nombres = $"EMP_{tag}",
                Documento = $"99{tag}",
                Salario = 2800000m,
                FechaIngreso = DateTime.Today,
                FechaNacimiento = DateTime.Today.AddYears(-28)
            }, "A");
            Assert.True(empId > 0);
            Assert.Contains(await repo.GetEmployeesAsync(), e => e.IdLegajo == empId);

            lookupId = await repo.SaveHrLookupAsync("cargos", new HrLookupEntity { Codigo = $"C{tag[..4]}", Descripcion = $"CARGO_{tag}" }, "A");
            Assert.True(lookupId > 0);
            Assert.Contains(await repo.GetHrLookupAsync("cargos"), l => l.Id == lookupId);
        }
        finally
        {
            if (lookupId > 0) await repo.DeleteHrLookupAsync("cargos", lookupId);
            if (empId > 0) await repo.DeleteEmployeeAsync(empId);
            if (!string.IsNullOrWhiteSpace(agendaId)) await repo.DeleteAgendaItemAsync(agendaId);
            if (rubroId > 0) await repo.DeleteCashRubroAsync(rubroId);
            if (cityId > 0) await repo.DeleteCityAsync(cityId);
        }
    }

    [Fact]
    public void Stage3AllNewWindows_InitializeComponent_WithoutExceptions()
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
                var lic = new VfpsLicenseService();
                var conn = new MySqlConnectionFactory(cfg);
                var dbf = new VfpDbfService();
                var prodRepo = new ProductRepository(conn);
                var salesRepo = new SalesRepository(conn);
                var custRepo = new CustomerRepository(conn);
                var purchRepo = new PurchaseRepository(conn);
                var cashRepo = new CashRepository(conn);
                var sysRepo = new SystemRepository(conn);
                var stage3Repo = new Stage3Repository(conn);
                var printSvc = new TicketPrintService(cfg);
                var iaSvc = new CargaRapidaIaService();

                var wm = new WindowManagerService(
                    cfg, conn, dbf, prodRepo, salesRepo, custRepo,
                    purchRepo, cashRepo, sysRepo, printSvc, iaSvc, stage3Repo);

                var main = new MainWindow(cfg, lic, dbf, sysRepo, cashRepo, salesRepo, wm, stage3Repo);
                var cash = new CashManagementWindow(cashRepo, stage3Repo);
                cash.Configure(2);
                var prefs = new ConfiguracionPreferenciasWindow(cfg, sysRepo, conn, printSvc, wm);
                prefs.Configure(6);

                var master = new MasterCatalogsWindow(stage3Repo, cashRepo, sysRepo);
                master.Configure(3);
                var agenda = new AgendaAndOrganizerWindow(stage3Repo, "ADMINISTRA");
                agenda.Configure(1);
                var team = new CommercialTeamWindow(stage3Repo, sysRepo, purchRepo, cashRepo);
                team.Configure(3);
                var reports = new ExtendedReportsWindow(stage3Repo);
                reports.Configure(6);
                var ops = new CommercialOperationsWindow(stage3Repo, salesRepo, purchRepo, prodRepo, custRepo, cashRepo, sysRepo, wm);
                ops.Configure(6, adminCreditMode: true, onlyOverdue: true);
                var hr = new HumanResourcesWindow(stage3Repo);
                hr.Configure(2, lookupIndex: 1, calcPreset: "AGUINALDO");
                var spec = new SpecialtyModulesWindow(stage3Repo, sysRepo, salesRepo, wm);
                spec.Configure(0, "Cocina");

                foreach (var w in new Window[] { main, cash, prefs, master, agenda, team, reports, ops, hr, spec })
                {
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
