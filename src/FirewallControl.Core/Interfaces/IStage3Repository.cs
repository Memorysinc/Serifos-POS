using FirewallControl.Core.Entities;

namespace FirewallControl.Core.Interfaces;

public interface IStage3Repository
{
    // _FONDO Real-Time KPIs & Feeds
    Task<FondoDashboardData> GetFondoDashboardDataAsync(string currentUser);

    // Master Catalogs: Currency, City, Barrio, Cash Registers, Cash Rubros, Branches
    Task<int> SaveCurrencyAsync(CurrencyEntity currency, string lcTipo);
    Task<bool> DeleteCurrencyAsync(int idMoneda);
    Task<List<CityEntity>> GetCitiesAsync();
    Task<int> SaveCityAsync(CityEntity city, string lcTipo);
    Task<bool> DeleteCityAsync(int idCiudad);
    Task<List<BarrioEntity>> GetBarriosAsync(int? idCiudad = null);
    Task<int> SaveBarrioAsync(BarrioEntity barrio, string lcTipo);
    Task<bool> DeleteBarrioAsync(int idBarrio);
    Task<int> SaveCashRegisterAsync(CashRegisterEntity cashRegister, string lcTipo);
    Task<bool> DeleteCashRegisterAsync(int idCaja);
    Task<List<CashRubroEntity>> GetCashRubrosAsync();
    Task<int> SaveCashRubroAsync(CashRubroEntity rubro, string lcTipo);
    Task<bool> DeleteCashRubroAsync(int idRubros);
    Task<int> SaveBranchAsync(BranchEntity branch, string lcTipo);

    // Agenda, Organizer, SMS
    Task<List<AgendaItemEntity>> GetAgendaItemsAsync(DateTime? desde, DateTime? hasta, string? filterUser = null);
    Task<string> SaveAgendaItemAsync(AgendaItemEntity item, string lcTipo);
    Task<bool> DeleteAgendaItemAsync(string id);
    Task<List<OrganizerEntity>> GetOrganizerContactsAsync(string? filter = null);
    Task<int> SaveOrganizerContactAsync(OrganizerEntity contact, string lcTipo);
    Task<bool> DeleteOrganizerContactAsync(int idAgenda);
    Task<List<SmsConnectionEntity>> GetSmsConnectionsAsync();
    Task<int> SaveSmsConnectionAsync(SmsConnectionEntity sms, string lcTipo);

    // Commercial Team: Vendedores, Supervisores, Cobradores, Metas, Comisiones, Adelantos/Recibos
    Task<bool> DeleteSellerAsync(int idVendedor);
    Task<List<SupervisorEntity>> GetAllSupervisorsAsync();
    Task<int> SaveSupervisorAsync(SupervisorEntity sup, string name, string doc, string tel, string cel, string dir, decimal comision, int estado);
    Task<bool> DeleteSupervisorAsync(int idSupervisor);
    Task<List<CollectorEntity>> GetCollectorsAsync();
    Task<int> SaveCollectorAsync(CollectorEntity collector, string lcTipo);
    Task<bool> DeleteCollectorAsync(int idCobrador);
    Task<List<SalesGoalEntity>> GetSalesGoalsAsync();
    Task<int> SaveSalesGoalAsync(SalesGoalEntity goal, string lcTipo);
    Task<bool> DeleteSalesGoalAsync(int idMeta);
    Task<List<SalesGoalTableEntity>> GetSalesGoalTableAsync();
    Task<int> SaveSalesGoalRowAsync(SalesGoalTableEntity row, string lcTipo);
    Task<bool> DeleteSalesGoalRowAsync(int idTablaMeta);
    Task<List<SupplierCommissionEntity>> GetSupplierCommissionsAsync();
    Task<int> SaveSupplierCommissionAsync(SupplierCommissionEntity comm, string lcTipo);
    Task<bool> DeleteSupplierCommissionAsync(int idComision);
    Task<List<ReceiptAdvanceEntity>> GetReceiptsAdvancesAsync(DateTime desde, DateTime hasta);
    Task<int> SaveReceiptAdvanceAsync(ReceiptAdvanceEntity rec);

    // Commercial Operations: Presupuestos CRUD, Pedidos, Devoluciones, Cuentas, Cuotas/Morosos, Solicitud Crédito
    Task<int> SaveBudgetFullAsync(BudgetSummaryEntity header, List<BudgetItemEntity> items, string usuario = "ADMINISTRA");
    Task<bool> DeleteBudgetAsync(int idPresupuesto);
    Task<List<PurchaseOrderEntity>> GetPurchaseOrdersAsync(DateTime desde, DateTime hasta);
    Task<int> SavePurchaseOrderTransactionAsync(PurchaseOrderEntity order, string usuario = "ADMINISTRA");
    Task<int> SaveSalesReturnTransactionAsync(ReturnHeaderEntity ret);
    Task<List<ReturnHeaderEntity>> GetSalesReturnsAsync(DateTime desde, DateTime hasta);
    Task<int> SavePurchaseReturnTransactionAsync(ReturnHeaderEntity ret);
    Task<List<ReturnHeaderEntity>> GetPurchaseReturnsAsync(DateTime desde, DateTime hasta);
    Task<List<SupplierDebtEntity>> GetSupplierDebtsAsync(int? idProveedor = null);
    Task<List<SupplierPaymentEntity>> GetSupplierPaymentsAsync(DateTime desde, DateTime hasta);
    Task RegisterSupplierPaymentAsync(int idDeuda, decimal monto, int idCaja, string obs, string usuario);
    Task<List<PayableAccountEntity>> GetPayableAccountsAsync();
    Task<int> SavePayableAccountAsync(PayableAccountEntity acc, string lcTipo);
    Task<List<ReceivableAccountEntity>> GetReceivableAccountsAsync();
    Task<int> SaveReceivableAccountAsync(ReceivableAccountEntity acc, string lcTipo);
    Task<List<CreditInstallmentRow>> GetCreditInstallmentsAsync(bool onlyOverdue = false, string? search = null);
    Task RegisterCreditInstallmentPaymentAsync(int idCredito, decimal entrega, int idCaja, string usuario);
    Task<List<CreditApplicationEntity>> GetCreditApplicationsAsync(int? estadoFilter = null);
    Task<int> SaveCreditApplicationAsync(CreditApplicationEntity app, string lcTipo);
    Task<bool> UpdateCreditApplicationStatusAsync(int idSolicitud, int estado, string obsEstado);

    // Extended Informes & Auditoría
    Task<List<ExtendedReportRow>> GetSalesBySellerReportAsync(DateTime desde, DateTime hasta);
    Task<List<ExtendedReportRow>> GetSalesBySupervisorReportAsync(DateTime desde, DateTime hasta);
    Task<List<ExtendedReportRow>> GetSalesByCustomerReportAsync(DateTime desde, DateTime hasta);
    Task<List<ExtendedReportRow>> GetSalesByDepositReportAsync(DateTime desde, DateTime hasta);
    Task<List<ExtendedReportRow>> GetSoldProductsReportAsync(DateTime desde, DateTime hasta);
    Task<List<ExtendedReportRow>> GetPurchasesBySupplierReportAsync(DateTime desde, DateTime hasta);
    Task<List<ExtendedReportRow>> GetPurchasedProductsReportAsync(DateTime desde, DateTime hasta);
    Task<List<UserTransactionLogEntity>> GetUserTransactionLogsAsync(DateTime desde, DateTime hasta, string? userFilter = null);

    // RRHH / Personales
    Task<SalaryParametersEntity> GetSalaryParametersAsync();
    Task SaveSalaryParametersAsync(SalaryParametersEntity p);
    Task<List<EmployeeLegajoEntity>> GetEmployeesAsync();
    Task<int> SaveEmployeeAsync(EmployeeLegajoEntity emp, string lcTipo);
    Task<bool> DeleteEmployeeAsync(int idLegajo);
    Task<List<EmployeeDiscountEntity>> GetEmployeeDiscountsAsync();
    Task<int> SaveEmployeeDiscountAsync(EmployeeDiscountEntity disc, string lcTipo);
    Task<List<SalaryPayrollEntity>> GetSalaryPayrollsAsync(string? mes = null, int? anio = null);
    Task<int> ProcessAndSaveSalaryPayrollAsync(SalaryPayrollEntity payroll);
    Task<List<HrLookupEntity>> GetHrLookupAsync(string tableName);
    Task<int> SaveHrLookupAsync(string tableName, HrLookupEntity row, string lcTipo);
    Task<bool> DeleteHrLookupAsync(string tableName, int id);

    // Restaurant & Veterinaria helpers
    Task<List<RestaurantOrderItemRow>> GetRestaurantOrdersBySectorAsync(string? sector = null);
    Task<bool> TableExistsAsync(string tableName);
}
