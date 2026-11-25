using System.Data;
using FirewallControl.Core.Entities;

namespace FirewallControl.Core.Interfaces;

public interface IConfigService
{
    FirewallConfig Config { get; }
    AppPreferencesEntity Preferences { get; }
    void LoadConfiguration();
    void SavePreferences();
    void ResetToFactoryDefaults();
    void SaveLocalSettings();
    void SaveFastCargaSettings(string apiKey, string apiModel, string modelsCatalog);
    void SaveServerCredentials(string host, string user, string password, int port, string database);
    string GetMySqlConnectionString();
}

public interface ILicenseService
{
    bool IsLicensed { get; }
    string RegisteredCompany { get; }
    string LicenseKeyDisplay { get; }
    bool VerifyLicense();
}

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
    Task<bool> TestConnectionAsync(string? customConnectionString = null);
}

public interface IVfpDbfService
{
    List<MenuPromptItem> LoadMenuStructure(string stylesDirectory, bool includeVeterinaria = false);
    List<GenesButtonItem> LoadGenesButtons(string dbfPath, int genId);
    List<GenesButtonItem> LoadAllGenesProducts(string dbfPath);
    void SaveGenesButton(string dbfPath, GenesButtonItem item);
    void SaveAllGenesProducts(string dbfPath, List<GenesButtonItem> items);
    void DeleteGenesProduct(string dbfPath, int id);
    void ReorderGenesProducts(string dbfPath, int genId, IReadOnlyList<int> orderedIds);
    string GetNextFreeGeneCode(string dbfPath);
}

public interface IProductRepository
{
    Task<ProductEntity?> GetByCodeAsync(string code, int idDeposito = 1);
    Task<ProductEntity?> GetByCodeIncludingInactiveAsync(string code, int idDeposito = 1);
    Task<ProductEntity?> GetByIdAsync(int idProducto, int idDeposito = 1);
    Task<int> GetNextProductIdAsync();
    Task<Dictionary<string, (decimal Precio1, decimal Costo1)>> GetPricesByCodesAsync(IEnumerable<string> codes);
    Task<List<ProductEntity>> SearchProductsAsync(string searchText, int idDeposito = 1, int limit = 100);
    Task<decimal> GetDepositStockAsync(int idProducto, int idDeposito);
    Task<List<CompoundProductComponent>> GetCompoundComponentsAsync(int idProductoCompuesto);
    Task<int> SaveCompoundComponentAsync(int idProductoCompuesto, int idProductoComponente, string codigoComponente, decimal cantidad, int idSucursal = 1, string desSucursal = "");
    Task<bool> DeleteCompoundComponentAsync(int idCompuesto);
    Task<List<ProductLotEntity>> GetProductLotsAsync(int idProducto, int? idDeposito = null);
    Task<int> SaveProductAsync(ProductEntity product, string lcTipo);
    Task<int> SaveFastProductAsync(string codigo, string descripcion, decimal costo, decimal precioVenta, bool fraccionable, bool isEdit, bool noControlaStock = true);
    Task<bool> DeleteProductAsync(int idProducto);
    Task UpdateProductPriceAsync(int idProducto, decimal costo1, decimal precio1, decimal precio2, decimal precio3, decimal stockActual, int idDeposito);
    Task<int> UpdateAllPricesByUtilityAsync(decimal u1, decimal u2, decimal u3, decimal u4, decimal u5, decimal u6, decimal u7);
    Task<List<DepositEntity>> GetDepositsAsync();
    Task<int> GetNextNroAjusteAsync();
    Task<int> SaveStockAdjustmentTransactionAsync(StockAdjustmentEntity adjustment);
    Task AdjustStockAsync(int idProducto, int idDeposito, string codigo, decimal nuevaCantidad, string motivo, string usuario);

    // Stage 2: Full Product Catalog, 72-column ABM, Auxiliary Catalogs, Transfers & Reports
    Task<List<ProductEntity>> SearchProductsAdvancedAsync(ProductSearchFilter filter);
    Task<bool> IsCodeInUseAsync(string codigo, int excludeIdProducto = 0);
    Task<int> SaveProductFullAsync(ProductEntity product, string lcTipo, int idDeposito = 1, string usuario = "ADMINISTRA");
    Task<bool> ReactivateProductAsync(int idProducto, string usuario = "ADMINISTRA");
    Task<List<ProductDepositStockRow>> GetProductStockByDepositsAsync(int idProducto, string codigo);
    Task<List<ProductLocationEntity>> GetProductLocationsAsync(int idProducto);
    Task<List<ProductGroupEntity>> GetProductGroupsAsync();
    Task<int> SaveProductGroupAsync(ProductGroupEntity group, string lcTipo);
    Task<bool> DeleteProductGroupAsync(int id);
    Task<List<ProductSubGroupEntity>> GetProductSubGroupsAsync();
    Task<int> SaveProductSubGroupAsync(ProductSubGroupEntity subGroup, string lcTipo);
    Task<bool> DeleteProductSubGroupAsync(int id);
    Task<List<ProductBrandEntity>> GetProductBrandsAsync();
    Task<int> SaveProductBrandAsync(ProductBrandEntity brand, string lcTipo);
    Task<bool> DeleteProductBrandAsync(int id);
    Task<List<ProductModelEntity>> GetProductModelsAsync();
    Task<int> SaveProductModelAsync(ProductModelEntity model, string lcTipo);
    Task<bool> DeleteProductModelAsync(int id);
    Task<int> SaveDepositAsync(DepositEntity deposit, string lcTipo);
    Task<bool> DeleteDepositAsync(int id);
    Task<int> SaveProductLotAsync(ProductLotEntity lot);
    Task<bool> DeleteProductLotAsync(int idLote);
    Task<List<ProductLotEntity>> GetExpiringLotsReportAsync(DateTime desde, DateTime hasta);
    Task<List<ProductEntity>> GetPriceListReportAsync(int? idGrupo, int? idSubGrupo, int? idMarca, int? idModelo, string orderBy);
    Task<List<ProductDepositStockRow>> GetStockByDepositsReportAsync(int? idDeposito, bool onlyStockPositive, string orderBy);
    Task<int> UpdateFilteredPricesByUtilityAsync(
        decimal u1, decimal u2, decimal u3, decimal u4, decimal u5, decimal u6, decimal u7,
        int? idGrupo, int? idMarca, int? idProveedor, decimal? flatPercentDelta, string usuario);
    Task<List<ProductTransferEntity>> GetDepositTransfersAsync(DateTime desde, DateTime hasta);
    Task<int> SaveDepositTransferTransactionAsync(ProductTransferEntity transfer);
}

public interface ISalesRepository
{
    Task<int> GetNextNroVentaAsync();
    Task<(int MaxIdVenta, int MaxNroVenta, decimal TotalHoy)> GetLiveSyncStatusAsync();
    Task<TimbradoLocalEntity> GetTimbradoLocalAsync(int idSucursal, int idCaja);
    Task<SaleEntity?> GetSaleByIdAsync(int idVenta);
    Task<SaleEntity?> GetSaleByNroVentaAsync(int nroVenta);
    Task<List<SaleEntity>> GetRecentSalesAsync(DateTime? desde = null, DateTime? hasta = null, int limit = 100);
    Task<int> ProcessSaleTransactionAsync(SaleEntity sale, string lcTipo);
    Task<bool> DeleteSalePermanentlyAsync(int idVenta);
    Task<List<ProfitReportItemEntity>> GetInformeLucroAsync(DateTime desde, DateTime hasta, string? rucCliente = null);
    Task<List<ProfitReportItemEntity>> GetInformeLucroProductosAsync(DateTime desde, DateTime hasta, string? codigoProducto = null);
    Task<List<BudgetSummaryEntity>> SearchBudgetsAsync(string? filter = null, int limit = 100);
    Task<(BudgetSummaryEntity Header, List<BudgetItemEntity> Items)?> GetBudgetByNumberOrIdAsync(int nroOrId, bool byNumber = true);
    Task SaveSaleInstallmentsAsync(int idVenta, int nroVenta, string nroFactura, int idCliente, DateTime fechaVenta, decimal entregaInicial, List<SaleInstallmentEntity> installments, int idCaja, int idSucursal, string cidUsuario);
    Task<int> RegisterLegalInvoiceRecordAsync(int n1, int n2, int n3, int idCliente, int idVenta, int idSucursal, int idCaja, bool advanceTimbrado = true);
}

public interface ICustomerRepository
{
    Task<CustomerEntity?> GetByIdAsync(int idCliente);
    Task<List<CustomerEntity>> SearchCustomersAsync(string searchText, int limit = 100);
    Task<int> SaveCustomerAsync(CustomerEntity customer, string lcTipo);
    Task<bool> DeleteCustomerAsync(int idCliente);
    Task<List<CustomerCollectionEntity>> GetPendingCollectionsAsync(int? idCliente = null);
    Task RegisterCollectionPaymentAsync(int idCobro, decimal montoEntrega, int idCaja, string usuario);
}

public interface IPurchaseRepository
{
    Task<int> GetNextNroCompraAsync();
    Task<PurchaseEntity?> GetPurchaseByIdAsync(int idCompra);
    Task<bool> DeletePurchaseAsync(int idCompra);
    Task<List<SupplierEntity>> SearchSuppliersAsync(string searchText, int limit = 100);
    Task<int> SaveSupplierAsync(SupplierEntity supplier, string lcTipo);
    Task<bool> DeleteSupplierAsync(int idProveedor);
    Task<List<PurchaseEntity>> GetRecentPurchasesAsync(DateTime? desde = null, DateTime? hasta = null, int limit = 100);
    Task<int> ProcessPurchaseTransactionAsync(PurchaseEntity purchase);
}

public interface ICashRepository
{
    Task<List<CashRegisterEntity>> GetCashRegistersAsync();
    Task<List<CashMovementEntity>> GetMovementsAsync(DateTime desde, DateTime hasta, int? idCaja = null);
    Task<int> RegisterMovementAsync(CashMovementEntity movement);
    Task<List<CurrencyEntity>> GetCurrenciesAsync();
    Task<ExchangeRateEntity> GetLatestExchangeRateAsync();
    Task SaveExchangeRateAsync(ExchangeRateEntity rate);
}

public interface ISystemRepository
{
    Task<CompanyDataEntity> GetCompanyDataAsync();
    Task SaveCompanyDataAsync(CompanyDataEntity data);
    Task<List<BranchEntity>> GetBranchesAsync();
    Task<List<SellerEntity>> GetSellersAsync();
    Task<List<SupervisorEntity>> GetSupervisorsAsync();
    Task<int> SaveSellerAsync(SellerEntity seller, string lcTipo);
    Task<List<UserEntity>> GetActiveUsersAsync();
    Task<UserEntity?> GetUserByUsernameAsync(string cidUsuario);
    Task<UserGroupEntity?> GetGroupByCodeAsync(string cidGrupo);
    Task<List<UserGroupEntity>> GetAllGroupsAsync();
    Task SaveGroupAsync(UserGroupEntity group, string lcTipo);
    Task SaveUserPasswordAsync(int nlineno, string newPlainPassword);
    Task SaveUserAsync(UserEntity user, string lcTipo);
    Task<bool> DeleteUserAsync(int nlineno);
    Task<List<TimbradoLocalEntity>> GetTimbradosLocalesAsync();
    Task SaveTimbradoLocalAsync(TimbradoLocalEntity timbrado);
    Task<List<FormAccessEntity>> GetFormAccessListAsync(string nameForm);
    Task SaveFormAccessAsync(FormAccessEntity access);
    Task LogUserTransactionAsync(string formTran, string tablaTran, string tipoTran, string sqlTran, int claveTran, string userId);
}

public interface ITicketPrintService
{
    string FormatTicket80mm(SaleEntity sale, CompanyDataEntity company);
    string FormatLegalInvoice(SaleEntity sale, CompanyDataEntity company);
    Task PrintSaleAsync(SaleEntity sale, CompanyDataEntity company, bool showPreview = false);
}
