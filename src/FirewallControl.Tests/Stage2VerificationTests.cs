using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Dapper;
using FirewallControl.App.Services;
using FirewallControl.App.Views;
using FirewallControl.Core.Entities;
using FirewallControl.Infrastructure.Database;
using FirewallControl.Infrastructure.Repositories;
using FirewallControl.Infrastructure.Services;
using Xunit;

namespace FirewallControl.Tests;

public class Stage2VerificationTests
{
    private static string GetWorkspaceRoot() => TestPathHelper.GetWorkspaceRootOrTemp();

    [Fact]
    public async Task LiveMySql_SaveProductFull_72Columns_RoundTrip_DuplicateGuard_And_SoftDelete()
    {
        if (!await TestPathHelper.CanConnectMySqlAsync())
            return;

        var cfg = new ConfigService(GetWorkspaceRoot());
        var connFactory = new MySqlConnectionFactory(cfg);
        var productRepo = new ProductRepository(connFactory);

        string uniqueCode = $"TST2_{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        int createdId = 0;

        try
        {
            var prod = new ProductEntity
            {
                Codigo = uniqueCode,
                CodigoBarra = "7840000999888",
                Descripcion = "PRODUCTO PRUEBA ETAPA 2 72 CAMPOS",
                IdMoneda = 1,
                Cambio = 7350m,
                CostoMedio = 10500m,
                CostoMonedaExtranjera = 1.45m,
                Costo1 = 10000m, P1 = 35m, Precio1 = 13500m,
                Costo2 = 10100m, P2 = 40m, Precio2 = 14140m,
                Costo3 = 10200m, P3 = 45m, Precio3 = 14790m,
                Costo4 = 10300m, P4 = 50m, Precio4 = 15450m,
                Costo5 = 10400m, P5 = 55m, Precio5 = 16120m,
                Costo6 = 10500m, P6 = 60m, Precio6 = 16800m,
                Costo7 = 10600m, P7 = 65m, Precio7 = 17490m,
                IdProveedor = 1,
                Referencia = "REF-72",
                IdGrupoProducto = 1,
                IdSubGrupoProducto = 2,
                IdMarcaProducto = 3,
                IdModeloProducto = 4,
                Familia = "FAMILIA-TEST",
                Aplicacion = "APLICACION DE PRUEBA ETAPA 2",
                Observacion = "OBSERVACION COMPLETA ETAPA 2",
                StockActual = 12.5m,
                StockMinimo = 3,
                StockMaximo = 50,
                Estado = 1,
                Iva = 2, // 5%
                NoControlaStock = 1,
                UsaSerial = 1,
                ImprimeFactura = 1,
                AutoCantidad = 1,
                AutoPrecio = 1,
                UsaVencimiento = 1,
                DescripcionEditable = 1,
                VendeSinStock = 1,
                AutoSuma = 1,
                PublicarWeb = 1,
                Fraccionable = 1,
                Compuesto = 1,
                Imagen = "test_etapa2.png",
                ControlaGarantia = 1,
                FechaInicioGarantia = new DateTime(2026, 11, 22),
                DiasGarantias = 90,
                FechaFinGarantia = new DateTime(2027, 2, 20),
                Guarnicion = 1,
                Menu = 1,
                Comanda = true,
                Cocina = 1,
                Otros = 1,
                Churrasqueria = 1,
                Barra = 1
            };

            createdId = await productRepo.SaveProductFullAsync(prod, "A", 1, "TESTUSER");
            Assert.True(createdId > 0);

            var fetched = await productRepo.GetByIdAsync(createdId, 1);
            Assert.NotNull(fetched);
            Assert.Equal(uniqueCode, fetched!.Codigo);
            Assert.Equal("7840000999888", fetched.CodigoBarra);
            Assert.Equal(7350m, fetched.Cambio);
            Assert.Equal(1.45m, fetched.CostoMonedaExtranjera);
            Assert.Equal(4, fetched.IdModeloProducto);
            Assert.Equal(2, fetched.Iva);
            Assert.Equal(5, fetched.Impuesto);
            Assert.Equal(1, fetched.UsaSerial);
            Assert.Equal(1, fetched.AutoSuma);
            Assert.Equal(1, fetched.PublicarWeb);
            Assert.Equal(1, fetched.ControlaGarantia);
            Assert.Equal(90, fetched.DiasGarantias);
            Assert.Equal(new DateTime(2026, 11, 22), fetched.FechaInicioGarantia);
            Assert.Equal(new DateTime(2027, 2, 20), fetched.FechaFinGarantia);
            Assert.Equal(1, fetched.Guarnicion);
            Assert.Equal(1, fetched.Menu);
            Assert.True(fetched.Comanda);
            Assert.Equal(1, fetched.Cocina);
            Assert.Equal(1, fetched.Otros);
            Assert.Equal(1, fetched.Churrasqueria);
            Assert.Equal(1, fetched.Barra);
            Assert.Equal(17490m, fetched.Precio7);
            Assert.Equal(12.5m, fetched.StockActual);

            Assert.True(await productRepo.IsCodeInUseAsync(uniqueCode, 0));
            Assert.False(await productRepo.IsCodeInUseAsync(uniqueCode, createdId));

            // Duplicate code on 'A' must throw
            var dup = new ProductEntity { Codigo = uniqueCode, Descripcion = "DUPLICADO", Costo1 = 100, Precio1 = 150 };
            await Assert.ThrowsAsync<InvalidOperationException>(() => productRepo.SaveProductFullAsync(dup, "A"));

            // Modify ('M') updating fields previously omitted by old C# SaveProductAsync
            fetched.IdModeloProducto = 5;
            fetched.CostoMonedaExtranjera = 2.75m;
            fetched.Barra = 0;
            fetched.Comanda = false;
            fetched.Descripcion = "PRODUCTO PRUEBA ETAPA 2 MODIFICADO";
            await productRepo.SaveProductFullAsync(fetched, "M", 1, "TESTUSER");

            var updated = await productRepo.GetByIdAsync(createdId, 1);
            Assert.NotNull(updated);
            Assert.Equal(5, updated!.IdModeloProducto);
            Assert.Equal(2.75m, updated.CostoMonedaExtranjera);
            Assert.Equal(0, updated.Barra);
            Assert.False(updated.Comanda);
            Assert.Equal("PRODUCTO PRUEBA ETAPA 2 MODIFICADO", updated.Descripcion);

            // Soft delete & reactivate
            Assert.True(await productRepo.DeleteProductAsync(createdId));
            Assert.Null(await productRepo.GetByCodeAsync(uniqueCode, 1));
            var inactiveFetch = await productRepo.GetByCodeIncludingInactiveAsync(uniqueCode, 1);
            Assert.NotNull(inactiveFetch);
            Assert.Equal(0, inactiveFetch!.Estado);

            Assert.True(await productRepo.ReactivateProductAsync(createdId, "TESTUSER"));
            Assert.NotNull(await productRepo.GetByCodeAsync(uniqueCode, 1));
        }
        finally
        {
            if (createdId > 0)
            {
                using var conn = connFactory.CreateConnection();
                await conn.ExecuteAsync("DELETE FROM itemsdepositos WHERE idproducto = @Id;", new { Id = createdId });
                await conn.ExecuteAsync("DELETE FROM tbl_tranusu WHERE clave_tran = @Id;", new { Id = createdId });
                await conn.ExecuteAsync("DELETE FROM productos WHERE idproducto = @Id;", new { Id = createdId });
            }
        }
    }

    [Fact]
    public async Task LiveMySql_AdvancedSearch_CatalogsCrud_Reports_And_SafeLocations()
    {
        if (!await TestPathHelper.CanConnectMySqlAsync())
            return;

        var cfg = new ConfigService(GetWorkspaceRoot());
        var connFactory = new MySqlConnectionFactory(cfg);
        var productRepo = new ProductRepository(connFactory);

        int tempGroupId = 0;
        int tempBrandId = 0;

        try
        {
            var azList = await productRepo.SearchProductsAdvancedAsync(new ProductSearchFilter
            {
                AzTab = "A-C",
                Criteria = ProductSearchCriteria.Descripcion,
                IdDeposito = 1,
                Limit = 50
            });
            Assert.All(azList, p =>
            {
                char first = char.ToUpperInvariant(p.Descripcion.Trim()[0]);
                Assert.True(first is >= 'A' and <= 'C', $"Producto fuera de rango A-C: {p.Descripcion}");
            });

            var byCode = await productRepo.SearchProductsAdvancedAsync(new ProductSearchFilter
            {
                Criteria = ProductSearchCriteria.Codigo,
                SearchText = "1",
                Limit = 25
            });
            Assert.NotNull(byCode);

            // Must not throw even if ubicacion_prod is missing in DB
            var locs = await productRepo.GetProductLocationsAsync(1);
            Assert.NotNull(locs);

            var lp = await productRepo.GetPriceListReportAsync(null, null, null, null, "descripcion");
            Assert.NotEmpty(lp);

            var sd = await productRepo.GetStockByDepositsReportAsync(null, false, "codigo");
            Assert.NotNull(sd);

            var lots = await productRepo.GetExpiringLotsReportAsync(DateTime.Today.AddYears(-1), DateTime.Today.AddYears(2));
            Assert.NotNull(lots);

            string tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var grp = new ProductGroupEntity
            {
                Descripcion = $"GRP_TST_{tag}",
                Descuento = 12.5m,
                FechaInicio = DateTime.Today,
                FechaFinal = DateTime.Today.AddDays(15),
                Impresora = "CAJA1"
            };
            tempGroupId = await productRepo.SaveProductGroupAsync(grp, "A");
            Assert.True(tempGroupId > 0);
            var groups = await productRepo.GetProductGroupsAsync();
            var fetchedGrp = groups.FirstOrDefault(g => g.IdGrupoProducto == tempGroupId);
            Assert.NotNull(fetchedGrp);
            Assert.Equal(12.5m, fetchedGrp!.Descuento);

            var brand = new ProductBrandEntity { Descripcion = $"MRC_TST_{tag}" };
            tempBrandId = await productRepo.SaveProductBrandAsync(brand, "A");
            Assert.True(tempBrandId > 0);
        }
        finally
        {
            if (tempGroupId > 0) await productRepo.DeleteProductGroupAsync(tempGroupId);
            if (tempBrandId > 0) await productRepo.DeleteProductBrandAsync(tempBrandId);
        }
    }

    [Fact]
    public void BarcodeLabelPrintHelper_BuildsCode39AndLabelsFlowDocument_Cleanly()
    {
        Exception? threadEx = null;
        var staThread = new Thread(() =>
        {
            try
            {
                var prod = new ProductEntity
                {
                    IdProducto = 99,
                    Codigo = "ABC123",
                    Descripcion = "PRODUCTO ETIQUETA TEST",
                    Precio1 = 25000m
                };

                var priceDoc = BarcodeLabelPrintHelper.BuildLabelsDocument(prod, 2, isBarcodeMode: false, threePerRow: false);
                Assert.NotEmpty(priceDoc.Blocks);

                var barcodeDoc = BarcodeLabelPrintHelper.BuildLabelsDocument(prod, 4, isBarcodeMode: true, threePerRow: true);
                Assert.NotEmpty(barcodeDoc.Blocks);
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
    public void Stage2AllWindows_InitializeComponent_WithoutExceptions()
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

                var windows = new Window[]
                {
                    new ProductsManagementWindow(prodRepo, sysRepo, wm, purchRepo, cashRepo),
                    new AjustePreciosMasivoWindow(prodRepo, sysRepo, purchRepo, wm),
                    new ProductSubReportsWindow(prodRepo, cfg),
                    new TrasladoDepositosWindow(prodRepo, "ADMINISTRA"),
                    new ProductImageBrowserWindow(prodRepo, cfg),
                    new CatalogQuickPickDialog(),
                    new QuickCatalogAddDialog(),
                    new FormAccessDialog(sysRepo)
                };

                foreach (var w in windows)
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
